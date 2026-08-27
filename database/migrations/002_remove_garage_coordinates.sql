BEGIN;

ALTER TABLE fault_management.garages
    DROP COLUMN IF EXISTS latitude,
    DROP COLUMN IF EXISTS longitude;

COMMIT;
-- Proje kapsamında kullanılmayan garaj enlem/boylam alanlarını şemadan kaldırır.
