CREATE TABLE public.vehicle_snapshot(
   vehicle_snapshot_uuid uuid PRIMARY KEY,
   payload jsonb NOT NULL,
   metadata_payload jsonb NOT NULL
);

CREATE TABLE public.vehicle(
    vehicle_uuid uuid PRIMARY KEY,
    payload jsonb NOT NULL,
    metadata_payload jsonb NOT NULL
);