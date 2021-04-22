-- This table is used to store work items for publishing outgoing events. This is a part of Transactional Outbox implementation
-- see https://microservices.io/patterns/data/transactional-outbox.html for details
CREATE TABLE public.vehicle_outbox_work_item (
    work_item_uuid uuid NOT NULL PRIMARY KEY DEFAULT(uuid_generate_v4()),
    aggregate_uuid uuid NOT NULL,
    payload jsonb NOT NULL,
    outbox_unix_utc_timestamp bigint NOT NULL,
    processed_at_unix_utc_timestamp bigint NULL,

    -- Two columns below are used for pseudo dead-letter queue (in fact it's just a delayed processing).
    -- Depending on retry counter, the next retry (of outbox work item that failed to process) will grow based on backoff policy.
    retry_counter integer NOT NULL,
    next_retry_unix_utc_timestamp bigint NULL
);

-- This table is used in order to do a distributed aggregate locking whenever projection is applied.
-- Justification - we need to arrange a "queue" per aggregate, advisory locks won't work with uuids due to size limits.
-- (uuids are larger than bigint)
CREATE TABLE public.vehicle_outbox_locks (
    aggregate_uuid uuid NOT NULL PRIMARY KEY,
    lock_id SERIAL
);

-- Function to "bind" to a trigger for vehicle_outbox_locks maintainability.
CREATE OR REPLACE FUNCTION public.maintain_vehicle_outbox_locks()
  RETURNS TRIGGER
  LANGUAGE PLPGSQL
  AS
    $$
BEGIN

    IF NEW.aggregate_uuid IS NOT NULL THEN
        INSERT INTO public.vehicle_outbox_locks (aggregate_uuid)
        VALUES(NEW.aggregate_uuid)
        ON CONFLICT DO NOTHING;
END IF;

RETURN NEW;
END;
    $$;

-- Trigger to maintain outbox locks availability.
-- Does not fire on DELETE because we don't want to drain serial on the locks table just for the fun of it.
CREATE TRIGGER maintain_vehicle_outbox_locks
    AFTER INSERT OR UPDATE ON public.vehicle_outbox_work_item
                        FOR EACH ROW
                        EXECUTE PROCEDURE public.maintain_vehicle_outbox_locks();

-- Select function for retrieving outbox work item batches with distributed locks per aggregate.
-- Takes top x (func parameter) outbox work items ordered by outbox_unix_utc_timestamp (to maintain the order of the events per aggregate)
-- that are:
-- 1. associated with the aggregates which are not locked at the moment with advisory locks;
-- 2. new (don't fall under delayed processing category) or delayed with next retry planned for now or earlier;
-- And locks the related aggregates with advisory locks.
--
-- NOTE: if there is an event with the earliest time stamp which falls under delayed processing category and re-processing
-- is planned for later, than any related work items, that are associated with the same aggregates with later timestamps
-- will not be a part of the query result. This ensures that we're not getting out of order processing.

-- This function allows to avoid lock contention on inserting outbox payloads
-- and yet avoid duplicate processing for the same event in the scaled out environment.
CREATE OR REPLACE FUNCTION vehicle_pick_outbox_batch(integer, integer) -- $1 = max batch size $2 = scale factor
    RETURNS TABLE (
    work_item_uuid uuid,
    aggregate_uuid uuid,
    payload jsonb,
    outbox_unix_utc_timestamp bigint,
    processed_at_unix_utc_timestamp bigint,
    retry_counter integer,
    next_retry_unix_utc_timestamp bigint,
    lock_id int)
    AS
    $$
BEGIN
RETURN QUERY
    with rows as
        (SELECT lock_candidates.*, ol.lock_id FROM
                 (SELECT outbox.* FROM public.vehicle_outbox_work_item outbox
                  JOIN
                      -- get first outbox event that has to be published per aggregate
                       (SELECT not_processed_with_row_numbers.aggregate_uuid FROM (SELECT outbox2.aggregate_uuid,
                                              (row_number() over (partition by outbox2.aggregate_uuid order by outbox2.outbox_unix_utc_timestamp, outbox2.aggregate_uuid)) as row_num,
                                              COALESCE(outbox2.next_retry_unix_utc_timestamp, 0) as next_retry_unix_utc_timestamp
                                       FROM public.vehicle_outbox_work_item outbox2
                                       WHERE outbox2.processed_at_unix_utc_timestamp IS NULL) not_processed_with_row_numbers
                                        -- get locked aggregates
                                       LEFT JOIN
                                           (SELECT DISTINCT ol2.aggregate_uuid FROM public.vehicle_outbox_locks ol2
                                           JOIN pg_locks pgl ON pgl.classid = 'vehicle_outbox_locks'::regclass AND pgl.objid = ol2.lock_id AND pgl.pid <> pg_backend_pid()) as locked_aggregates
                                       ON locked_aggregates.aggregate_uuid = not_processed_with_row_numbers.aggregate_uuid
                        WHERE not_processed_with_row_numbers.row_num = 1
                            AND not_processed_with_row_numbers.next_retry_unix_utc_timestamp <= (extract(epoch from now()) * 1000) -- filter out aggregates with delayed processing
                            AND locked_aggregates.aggregate_uuid IS NULL -- filter out locked aggregates
                        LIMIT $2 * $1) as aggregates_available_for_processing
                       ON aggregates_available_for_processing.aggregate_uuid = outbox.aggregate_uuid
                  WHERE outbox.processed_at_unix_utc_timestamp IS NULL
                  ORDER BY outbox.outbox_unix_utc_timestamp, outbox.aggregate_uuid
                  LIMIT $2 * $1) as lock_candidates
            JOIN public.vehicle_outbox_locks ol ON ol.aggregate_uuid = lock_candidates.aggregate_uuid
            ORDER BY lock_candidates.outbox_unix_utc_timestamp, lock_candidates.aggregate_uuid)
SELECT rows.*
FROM rows
WHERE pg_try_advisory_xact_lock('vehicle_outbox_locks'::regclass::integer, rows.lock_id)
    LIMIT $1;
END;
$$ LANGUAGE plpgsql;

CREATE INDEX vehicle_outbox_ordering_idx ON public.vehicle_outbox_work_item (outbox_unix_utc_timestamp, aggregate_uuid);
CREATE INDEX vehicle_outbox_processed_idx ON public.vehicle_outbox_work_item (processed_at_unix_utc_timestamp ASC NULLS FIRST);