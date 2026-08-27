-- Teknik rapor ekranında kullanılan sonuç değerlerini veritabanı kuralıyla eşitler.
-- Eski RESOLVED değeri geçmiş kayıtlarla uyumluluk için korunur.
SET search_path TO fault_management, public;

ALTER TABLE repair_reports
    DROP CONSTRAINT IF EXISTS ck_repair_reports_result;

ALTER TABLE repair_reports
    ADD CONSTRAINT ck_repair_reports_result
    CHECK (result IN ('RESOLVED', 'REPAIRED', 'TEMPORARY_REPAIR', 'UNRESOLVED'));
