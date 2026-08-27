-- AMAÇ: Personel olayına işe gelememe başlangıcı, beklenen dönüş ve sağlık raporu numarası ekler.
-- Önce nullable kolonlar eklenir, eski kayıtlar doldurulur, sonra NOT NULL uygulanır; böylece migration eski veride kırılmaz.
ALTER TABLE fault_management.personnel_incidents
    ADD COLUMN IF NOT EXISTS absence_start_at timestamptz,
    ADD COLUMN IF NOT EXISTS expected_return_at timestamptz,
    ADD COLUMN IF NOT EXISTS medical_report_number varchar(100);
UPDATE fault_management.personnel_incidents
SET absence_start_at=COALESCE(absence_start_at,occurred_at),
    expected_return_at=COALESCE(expected_return_at,occurred_at+interval '1 day');
ALTER TABLE fault_management.personnel_incidents
    ALTER COLUMN absence_start_at SET NOT NULL,
    ALTER COLUMN expected_return_at SET NOT NULL;
-- Bir sürücünün belirli tarihte raporlu/izinli olup olmadığını hızlı kontrol eder.
CREATE INDEX IF NOT EXISTS ix_personnel_incidents_driver_absence
ON fault_management.personnel_incidents(driver_id,absence_start_at,expected_return_at)
WHERE is_active AND status<>'CANCELLED';
