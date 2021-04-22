-- this table is serves as a fact storage. All consumed events will end up store here until processed and killed
-- by retention policy worker
CREATE TABLE public.cdc_inbound_event (
    event_uuid uuid NOT NULL PRIMARY KEY DEFAULT(uuid_generate_v4()),
    source_offset bigint NOT NULL,
    source varchar(255) NOT NULL,
    event_key uuid NOT NULL,
    event_message jsonb NULL,
    event_unix_utc_timestamp bigint NOT NULL
);

-- supplementary table to hold metadata for inbound events.
-- As of now this includes any kafka message headers, topic`s partition and flag indicating as end of partition.
CREATE TABLE public.cdc_inbound_event_header (
    event_uuid uuid REFERENCES public.cdc_inbound_event (event_uuid),
    header_key text NOT NULL,
    header_value bytea NULL
);

CREATE INDEX cdc_inbound_event_header_event_uuid_idx ON public.cdc_inbound_event_header (event_uuid);