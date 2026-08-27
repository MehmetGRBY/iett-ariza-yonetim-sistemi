-- Bu dosyayı yalnızca marka-model katalog değişikliğini daha önce
-- veritabanında çalıştırdıysanız kullanın.
BEGIN;

ALTER TABLE fault_management.vehicles
    ADD COLUMN IF NOT EXISTS brand varchar(100),
    ADD COLUMN IF NOT EXISTS model varchar(120);

UPDATE fault_management.vehicles v
SET
    brand = b.name,
    model = m.name
FROM fault_management.vehicle_models m
JOIN fault_management.vehicle_brands b ON b.id = m.brand_id
WHERE v.vehicle_model_id = m.id
  AND (v.brand IS NULL OR v.model IS NULL);

ALTER TABLE fault_management.vehicles
    ALTER COLUMN brand SET NOT NULL,
    ALTER COLUMN model SET NOT NULL;

DROP INDEX IF EXISTS fault_management.ix_vehicles_model_id;
DROP INDEX IF EXISTS fault_management.ix_vehicle_models_brand_id;

ALTER TABLE fault_management.vehicles
    DROP CONSTRAINT IF EXISTS fk_vehicles_vehicle_model,
    DROP CONSTRAINT IF EXISTS vehicles_vehicle_model_id_fkey,
    DROP COLUMN IF EXISTS vehicle_model_id;

DROP TABLE IF EXISTS fault_management.vehicle_models;
DROP TABLE IF EXISTS fault_management.vehicle_brands;

COMMIT;
-- Ayrı katalog yerine araç tablosunda doğrudan marka/model metni tutulacak veri modeline geri döner.
