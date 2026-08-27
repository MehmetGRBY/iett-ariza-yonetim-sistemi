BEGIN;

-- Sunum akışında kullanıcının ekrandan izleyeceği saha adımları.
INSERT INTO fault_management.fault_statuses
    (code, name, is_closed_status, display_order, is_active)
VALUES
    ('RESOURCES_DEPARTING', 'Kaynaklar Yola Çıkıyor', false, 31, true),
    ('RESOURCES_EN_ROUTE', 'Kaynaklar Yolda', false, 32, true),
    ('RESOURCES_ARRIVED', 'Kaynaklar Ulaştı', false, 33, true),
    ('VEHICLE_DELIVERED', 'Araç Garaja Getirildi', false, 34, true)
ON CONFLICT (code) DO UPDATE SET
    name = EXCLUDED.name,
    is_closed_status = false,
    display_order = EXCLUDED.display_order,
    is_active = true;

-- Her ara adım ayrı saklanır; worker uygulama yeniden başlasa bile
-- kaldığı noktadan devam eder.
ALTER TABLE fault_management.fault_response_plans
    DROP CONSTRAINT IF EXISTS ck_fault_response_plans_automation_status;
ALTER TABLE fault_management.fault_response_plans
    ADD CONSTRAINT ck_fault_response_plans_automation_status CHECK
    (automation_status IN
        ('PENDING', 'RESOURCE_DEPARTING', 'RESOURCE_EN_ROUTE', 'RESOURCE_ARRIVED',
         'VEHICLE_DELIVERED', 'TEAM_ASSIGNED', 'DISPATCHED', 'REPAIRING',
         'WAITING_INSPECTION', 'READY_TO_CLOSE', 'MANUAL',
         'MANUAL_REPAIR_REQUIRED', 'COMPLETED', 'FAILED'));

-- Kaynak/ekip seçimi yine kullanıcı tarafından yapılır. PRESENTATION
-- yalnızca kayıt sonrası operasyon adımlarını 10 saniyelik simüle eder.
UPDATE fault_management.system_settings
SET setting_value = '"PRESENTATION"', updated_at = now()
WHERE setting_key = 'fault_operation_mode';

UPDATE fault_management.system_settings
SET setting_value = '10', updated_at = now()
WHERE setting_key IN ('presentation_dispatch_seconds', 'presentation_repair_seconds');

COMMIT;
