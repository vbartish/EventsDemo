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