BEGIN;

-- Garaj kontrolü ve servis öncesi kontrolde arıza, aracı kullanan bir sürücü
-- olmadan da tespit edilebilir. Test sürüşü ve transfer zorunluluğu API'de korunur.
ALTER TABLE fault_management.faults
    ALTER COLUMN driver_id DROP NOT NULL;

-- Sunum modu yalnızca bekleme sürelerini hızlandırır. Ekip/kaynak seçimi,
-- kontrol kaydı ve başarılı arızanın kapatılması kullanıcı kararı olarak kalır.
ALTER TABLE fault_management.fault_response_plans
    ADD COLUMN IF NOT EXISTS operation_mode varchar(20) NOT NULL DEFAULT 'MANUAL',
    ADD COLUMN IF NOT EXISTS inspection_attempt_count integer NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS max_inspection_attempts integer NOT NULL DEFAULT 3,
    ADD COLUMN IF NOT EXISTS ready_to_close boolean NOT NULL DEFAULT false;

ALTER TABLE fault_management.fault_response_plans
    DROP CONSTRAINT IF EXISTS ck_fault_response_plans_operation_mode;
ALTER TABLE fault_management.fault_response_plans
    ADD CONSTRAINT ck_fault_response_plans_operation_mode
    CHECK (operation_mode IN ('MANUAL', 'PRESENTATION'));

-- Eski constraint yalnızca tam otomatik durumları kabul ediyordu. Hibrit akışın
-- manuel, kontrol bekleme ve kapatma onayı adımları da geçerli olmalıdır.
ALTER TABLE fault_management.fault_response_plans
    DROP CONSTRAINT IF EXISTS ck_fault_response_plans_automation_status;
ALTER TABLE fault_management.fault_response_plans
    ADD CONSTRAINT ck_fault_response_plans_automation_status CHECK
    (automation_status IN
        ('PENDING', 'DISPATCHED', 'REPAIRING', 'WAITING_INSPECTION',
         'READY_TO_CLOSE', 'MANUAL', 'MANUAL_REPAIR_REQUIRED', 'COMPLETED', 'FAILED'));

-- Tamir ile kapanış arasındaki kontrol adımları ayrı durumlar olarak izlenir.
INSERT INTO fault_management.fault_statuses (code, name, is_closed_status, display_order, is_active)
VALUES
    ('WAITING_INSPECTION', 'Kontrol Bekliyor', false, 75, true),
    ('INSPECTION_FAILED', 'Kontrol Başarısız', false, 76, true)
ON CONFLICT (code) DO UPDATE SET
    name = EXCLUDED.name,
    is_closed_status = EXCLUDED.is_closed_status,
    display_order = EXCLUDED.display_order,
    is_active = true;

-- Ayarlar JSON olarak saklanır; yönetim ekranı mevcut genel ayar API'siyle
-- bu değerleri kod değişikliği yapmadan güncelleyebilir.
INSERT INTO fault_management.system_settings
    (setting_key, setting_value, description, is_active, created_at, updated_at)
VALUES
    ('fault_operation_mode', '"MANUAL"', 'Arıza akışı: MANUAL veya hızlandırılmış PRESENTATION.', true, now(), now()),
    ('presentation_dispatch_seconds', '30', 'Sunum modunda kaynağın olay yerine ulaşma süresi.', true, now(), now()),
    ('presentation_repair_seconds', '60', 'Sunum modunda tamir simülasyonu süresi.', true, now(), now()),
    ('max_post_repair_inspection_attempts', '3', 'Araç servis dışı bırakılmadan önce izin verilen başarısız kontrol sayısı.', true, now(), now())
ON CONFLICT (setting_key) DO NOTHING;

COMMIT;
