-- This table is used to store so called "work items". They serve as a "unit of work" for projection.
-- Records in this table support the simplest scenario when one event is being mapped to one aggregate per unit of work.
-- This means that you still can create multiple work items linked to the same aggregate, however projection based on
-- multiple work items won't be atomic (at least as of now it's not).
CREATE TABLE public.vehicle_cdc_work_item (
     cdc_work_item_uuid uuid NOT NULL PRIMARY KEY DEFAULT(uuid_generate_v4()),
     event_uuid uuid REFERENCES public.cdc_inbound_event (event_uuid),
     aggregate_uuid uuid NOT NULL,
     change_vlf_sequence_number bigint NOT NULL, -- vlf stands for "virtual log file"
     change_log_block_offset bigint NOT NULL,
     change_log_block_slot_number bigint NOT NULL,
     commit_vlf_sequence_number bigint NOT NULL,
     commit_log_block_offset bigint NOT NULL,
     commit_log_block_slot_number bigint NOT NULL,
     event_unix_utc_timestamp bigint NOT NULL,
     processed_at_unix_utc_timestamp bigint NULL,

    -- Two columns below are used for pseudo dead-letter queue (in fact it's just a delayed processing).
    -- Depending on retry counter, the next retry on work items that failed to process will grow based on backoff policy.
     retry_counter integer NOT NULL,
     next_retry_unix_utc_timestamp bigint NULL
);

-- This table is used in order to do a distributed aggregate locking whenever projection is applied.
-- Justification - we need to arrange a "queue" per aggregate, advisory locks won't work with uuids due to size limits.
-- (uuids are larger than bigint)
CREATE TABLE public.vehicle_aggregate_projection_locks (
      aggregate_uuid uuid NOT NULL PRIMARY KEY,
      lock_id SERIAL
);

-- Function to "bind" to a trigger for vehicle_aggregate_projection_locks maintainability.
CREATE OR REPLACE FUNCTION public.maintain_vehicle_projection_locks()
  RETURNS TRIGGER
  LANGUAGE PLPGSQL
  AS
    $$
BEGIN

    IF NEW.aggregate_uuid IS NOT NULL THEN
        INSERT INTO public.vehicle_aggregate_projection_locks (aggregate_uuid)
        VALUES(NEW.aggregate_uuid)
        ON CONFLICT DO NOTHING;
END IF;

RETURN NEW;
END;
    $$;

-- Trigger to maintain aggregate projection locks availability.
-- Does not fire on DELETE because we don't want to drain serial on the locks table just for the fun of it.
CREATE TRIGGER maintain_locks
    AFTER INSERT OR UPDATE ON public.vehicle_cdc_work_item
                        FOR EACH ROW
                        EXECUTE PROCEDURE public.maintain_vehicle_projection_locks();

-- Select function for retrieving work items batches with distributed locks per aggregate.
-- Takes top x (func parameter) work items ordered by log sequence number [vlf, log block offset, log block slot number]
-- (to maintain the order of the events per aggregate)
-- that are:
-- 1. associated with the aggregates which are not locked at the moment with advisory locks;
-- 2. new (don't fall under delayed processing category) or delayed with next retry planned for now or earlier;
-- And locks the related aggregates with advisory locks.
--
-- NOTE: if there is an event with the earliest time stamp which falls under delayed processing category and re-processing
-- is planned for later, than any related work items, that are associated with the same aggregates with later timestamps
-- will not be a part of the query result. This ensures that we're not getting out of order processing.

-- This function allows to avoid lock contention on inserting work items
-- and yet avoid duplicate processing for the same event in the scaled out environment.
CREATE OR REPLACE FUNCTION vehicle_pick_work_items_batch(integer, integer) -- $1 = max batch size $2 = scale factor
    RETURNS TABLE (
    cdc_work_item_uuid uuid,
    event_uuid uuid,
    aggregate_uuid uuid,
    change_vlf_sequence_number bigint,
    change_log_block_offset bigint,
    change_log_block_slot_number bigint,
    commit_vlf_sequence_number bigint,
    commit_log_block_offset bigint,
    commit_log_block_slot_number bigint,
    event_unix_utc_timestamp bigint,
    processed_at_unix_utc_timestamp bigint,
    retry_counter integer,
    next_retry_unix_utc_timestamp bigint,
    lock_id int)
    AS
    $$
BEGIN
RETURN QUERY
    with rows as (
        SELECT lock_candidates.*, apl.lock_id FROM
            (SELECT cwi.* FROM public.vehicle_cdc_work_item cwi

              -- filter out delayed processing
              JOIN
                  -- get first work item that has to be projected onto aggregate (per aggregate)
                   (SELECT not_processed_with_row_numbers.aggregate_uuid FROM (SELECT cwi2.aggregate_uuid,
                                              (row_number() over (partition by cwi2.aggregate_uuid order by
                                              cwi2.commit_vlf_sequence_number, cwi2.commit_log_block_offset, cwi2.commit_log_block_slot_number,
                                              cwi2.change_vlf_sequence_number, cwi2.change_log_block_offset, cwi2.change_log_block_slot_number,
                                              cwi2.aggregate_uuid)) as row_num,
                                        COALESCE(cwi2.next_retry_unix_utc_timestamp, 0) as next_retry_unix_utc_timestamp
                                        FROM public.vehicle_cdc_work_item cwi2
                                        WHERE cwi2.processed_at_unix_utc_timestamp IS NULL
                                        ) as not_processed_with_row_numbers
                    -- get locked aggregates
                    LEFT JOIN
                       (SELECT DISTINCT apl2.aggregate_uuid FROM public.vehicle_aggregate_projection_locks apl2
                       JOIN pg_locks pgl ON pgl.classid = 'vehicle_aggregate_projection_locks'::regclass AND pgl.objid = apl2.lock_id AND pgl.pid <> pg_backend_pid()) as locked_aggregates
                    ON locked_aggregates.aggregate_uuid = not_processed_with_row_numbers.aggregate_uuid
                    WHERE not_processed_with_row_numbers.row_num = 1
                        AND not_processed_with_row_numbers.next_retry_unix_utc_timestamp <= (extract(epoch from now()) * 1000) -- filter out aggregates with delayed processing
                        AND locked_aggregates.aggregate_uuid IS NULL -- filter out locked aggregates
                    LIMIT $2 * $1) as aggregates_available_for_processing
                   ON aggregates_available_for_processing.aggregate_uuid = cwi.aggregate_uuid
                   WHERE cwi.processed_at_unix_utc_timestamp IS NULL
                   ORDER BY cwi.commit_vlf_sequence_number, cwi.commit_log_block_offset, cwi.commit_log_block_slot_number,
                    cwi.change_vlf_sequence_number, cwi.change_log_block_offset, cwi.change_log_block_slot_number,
                    cwi.aggregate_uuid
                   LIMIT $1 * $2
                ) as lock_candidates
            JOIN public.vehicle_aggregate_projection_locks apl ON apl.aggregate_uuid = lock_candidates.aggregate_uuid
            ORDER BY
                  lock_candidates.commit_vlf_sequence_number, lock_candidates.commit_log_block_offset, lock_candidates.commit_log_block_slot_number,
                  lock_candidates.change_vlf_sequence_number, lock_candidates.change_log_block_offset, lock_candidates.change_log_block_slot_number,
                  lock_candidates.aggregate_uuid)
SELECT rows.*
FROM rows
WHERE pg_try_advisory_xact_lock('vehicle_aggregate_projection_locks'::regclass::integer, rows.lock_id)
    LIMIT $1;
END;
    $$ LANGUAGE plpgsql;

CREATE INDEX vehicle_cdc_work_item_ordering_idx ON public.vehicle_cdc_work_item (
        commit_vlf_sequence_number, commit_log_block_offset, commit_log_block_slot_number,
        change_vlf_sequence_number, change_log_block_offset, change_log_block_slot_number,
        aggregate_uuid);

CREATE INDEX vehicle_cdc_processed_idx ON public.vehicle_cdc_work_item (processed_at_unix_utc_timestamp ASC NULLS FIRST);