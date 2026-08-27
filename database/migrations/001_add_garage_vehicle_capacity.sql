BEGIN;

ALTER TABLE fault_management.garages
    ADD COLUMN IF NOT EXISTS vehicle_capacity integer NOT NULL DEFAULT 0;

ALTER TABLE fault_management.garages
    DROP CONSTRAINT IF EXISTS ck_garages_vehicle_capacity;

ALTER TABLE fault_management.garages
    ADD CONSTRAINT ck_garages_vehicle_capacity
    CHECK (vehicle_capacity >= 0);

COMMIT;
-- Garajlara toplam araç kapasitesi alanı ekler; doluluk ve kalan kapasite hesabının temelidir.
