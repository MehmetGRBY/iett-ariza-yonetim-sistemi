BEGIN;

SET search_path TO fault_management, public;

-- Şoförün bağlı olduğu garaj, çalışma türü ve anlık uygunluk durumu.
ALTER TABLE drivers
    ADD COLUMN IF NOT EXISTS garage_id bigint
        REFERENCES garages(id) ON DELETE RESTRICT,
    ADD COLUMN IF NOT EXISTS driver_type varchar(20) NOT NULL DEFAULT 'NORMAL',
    ADD COLUMN IF NOT EXISTS availability_status varchar(20) NOT NULL DEFAULT 'AVAILABLE';

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'ck_drivers_driver_type'
          AND conrelid = 'fault_management.drivers'::regclass
    ) THEN
        ALTER TABLE drivers
            ADD CONSTRAINT ck_drivers_driver_type
            CHECK (driver_type IN ('NORMAL', 'RESERVE'));
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'ck_drivers_availability_status'
          AND conrelid = 'fault_management.drivers'::regclass
    ) THEN
        ALTER TABLE drivers
            ADD CONSTRAINT ck_drivers_availability_status
            CHECK (availability_status IN ('AVAILABLE', 'ON_DUTY', 'ON_LEAVE', 'PASSIVE'));
    END IF;
END $$;

-- Bir şoförün kullanmaya yetkili olduğu araç türleri.
CREATE TABLE IF NOT EXISTS driver_vehicle_type_authorizations (
    id                  bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    driver_id           bigint NOT NULL REFERENCES drivers(id) ON DELETE RESTRICT,
    vehicle_type_id     bigint NOT NULL REFERENCES vehicle_types(id) ON DELETE RESTRICT,
    authorized_at       timestamptz NOT NULL DEFAULT now(),
    authorized_by_user_id bigint REFERENCES app_users(id) ON DELETE RESTRICT,
    is_active           boolean NOT NULL DEFAULT true,
    deactivated_at      timestamptz,
    description         varchar(500),
    CONSTRAINT uq_driver_vehicle_type_authorization UNIQUE (driver_id, vehicle_type_id),
    CONSTRAINT ck_driver_vehicle_type_authorization_dates CHECK (
        (is_active AND deactivated_at IS NULL)
        OR
        (NOT is_active AND deactivated_at IS NOT NULL)
    )
);

CREATE INDEX IF NOT EXISTS ix_drivers_garage_type_status
    ON drivers(garage_id, driver_type, availability_status)
    WHERE is_active;

CREATE INDEX IF NOT EXISTS ix_driver_vehicle_authorizations_type_active
    ON driver_vehicle_type_authorizations(vehicle_type_id, driver_id)
    WHERE is_active;

-- Müsait şoförleri, garajları ve araç yetkileriyle gösterir.
CREATE OR REPLACE VIEW vw_available_drivers AS
SELECT
    d.id AS driver_id,
    d.personnel_number,
    d.first_name,
    d.last_name,
    d.driver_type,
    d.garage_id,
    g.code AS garage_code,
    g.name AS garage_name,
    string_agg(vt.name, ', ' ORDER BY vt.name) AS authorized_vehicle_types
FROM drivers d
JOIN garages g ON g.id = d.garage_id
JOIN driver_vehicle_type_authorizations dva
  ON dva.driver_id = d.id
 AND dva.is_active
JOIN vehicle_types vt
  ON vt.id = dva.vehicle_type_id
 AND vt.is_active
WHERE d.is_active
  AND d.availability_status = 'AVAILABLE'
  AND g.is_active
  AND NOT EXISTS (
      SELECT 1
      FROM task_assignments ta
      WHERE ta.driver_id = d.id
        AND ta.is_active
  )
GROUP BY
    d.id, d.personnel_number, d.first_name, d.last_name,
    d.driver_type, d.garage_id, g.code, g.name;

COMMIT;

-- Kontrol: Yeni kolonlar, yetki tablosu ve view görünmelidir.
SELECT column_name, data_type
FROM information_schema.columns
WHERE table_schema = 'fault_management'
  AND table_name = 'drivers'
  AND column_name IN ('garage_id', 'driver_type', 'availability_status')
ORDER BY ordinal_position;
-- Sürücü garajı, normal/yedek türü, müsaitlik ve araç tipi yetkilendirme yapısını ekler.
