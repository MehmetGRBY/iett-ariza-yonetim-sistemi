-- AMAÇ: Doktor raporu henüz alınmamış olaylar için PENDING/SUBMITTED tabanlı rapor iş akışı kurar.
-- search_path, aşağıdaki nesne adlarında fault_management şemasını tekrar yazma ihtiyacını kaldırır.
SET search_path TO fault_management, public;

ALTER TABLE personnel_incidents
    ALTER COLUMN expected_return_at DROP NOT NULL,
    ADD COLUMN IF NOT EXISTS report_status varchar(20) NOT NULL DEFAULT 'PENDING',
    ADD COLUMN IF NOT EXISTS report_submitted_at timestamptz;

-- Mevcut olaylar, rapor numarası bulunup bulunmamasına göre yeni iş akışına uyarlanır.
UPDATE personnel_incidents
SET report_status = CASE
    WHEN medical_report_number IS NOT NULL AND btrim(medical_report_number) <> '' THEN 'SUBMITTED'
    ELSE 'PENDING'
END;

ALTER TABLE personnel_incidents DROP CONSTRAINT IF EXISTS ck_personnel_incidents_report_status;
ALTER TABLE personnel_incidents ADD CONSTRAINT ck_personnel_incidents_report_status
    CHECK (report_status IN ('PENDING', 'SUBMITTED', 'NOT_REQUIRED'));

CREATE INDEX IF NOT EXISTS ix_personnel_incidents_report_planning
    ON personnel_incidents(driver_id, report_status, expected_return_at)
    WHERE is_active AND status <> 'CANCELLED';
