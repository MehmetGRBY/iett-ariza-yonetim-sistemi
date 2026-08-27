BEGIN;

ALTER TABLE fault_management.app_users
    ADD COLUMN IF NOT EXISTS gender_code varchar(10);

ALTER TABLE fault_management.drivers
    ADD COLUMN IF NOT EXISTS gender_code varchar(10);

UPDATE fault_management.app_users SET gender_code = 'MALE' WHERE gender_code IS NULL;
UPDATE fault_management.drivers SET gender_code = 'MALE' WHERE gender_code IS NULL;

ALTER TABLE fault_management.app_users
    ALTER COLUMN gender_code SET NOT NULL,
    DROP CONSTRAINT IF EXISTS ck_app_users_gender,
    ADD CONSTRAINT ck_app_users_gender CHECK (gender_code IN ('MALE', 'FEMALE'));

ALTER TABLE fault_management.drivers
    ALTER COLUMN gender_code SET NOT NULL,
    DROP CONSTRAINT IF EXISTS ck_drivers_gender,
    ADD CONSTRAINT ck_drivers_gender CHECK (gender_code IN ('MALE', 'FEMALE'));

COMMIT;
-- Demo personel kayıtlarında ihtiyaç duyulan cinsiyet kodu kolonlarını eksikse ekler.
