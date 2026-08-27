BEGIN;

SET search_path TO fault_management, public;

-- ============================================================
-- 1) MEVCUT TABLOLARA EK ALANLAR
-- ============================================================

-- Arızanın meydana geldiği sefer görevi biliniyorsa bağlantısını saklar.
ALTER TABLE faults
    ADD COLUMN IF NOT EXISTS service_task_id bigint
        REFERENCES service_tasks(id) ON DELETE RESTRICT;

-- Planlanan saatlerin yanında gerçekleşen saatleri de saklar.
ALTER TABLE service_tasks
    ADD COLUMN IF NOT EXISTS actual_departure_at timestamptz,
    ADD COLUMN IF NOT EXISTS actual_arrival_at timestamptz,
    ADD COLUMN IF NOT EXISTS completed_at timestamptz;

-- Bildirimin hangi işlemden doğduğunu belirtir.
ALTER TABLE notifications
    ADD COLUMN IF NOT EXISTS service_task_id bigint
        REFERENCES service_tasks(id) ON DELETE RESTRICT,
    ADD COLUMN IF NOT EXISTS task_transfer_batch_id bigint
        REFERENCES task_transfer_batches(id) ON DELETE RESTRICT,
    ADD COLUMN IF NOT EXISTS notification_type varchar(50) NOT NULL DEFAULT 'SYSTEM';

-- Kullanıcı hesabı ve giriş güvenliği alanları.
ALTER TABLE app_users
    ADD COLUMN IF NOT EXISTS normalized_personnel_number varchar(30),
    ADD COLUMN IF NOT EXISTS must_change_password boolean NOT NULL DEFAULT true,
    ADD COLUMN IF NOT EXISTS failed_login_count integer NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS locked_until timestamptz,
    ADD COLUMN IF NOT EXISTS last_login_at timestamptz,
    ADD COLUMN IF NOT EXISTS password_changed_at timestamptz;

UPDATE app_users
SET normalized_personnel_number = upper(btrim(personnel_number))
WHERE normalized_personnel_number IS NULL
   OR normalized_personnel_number <> upper(btrim(personnel_number));

ALTER TABLE app_users
    ALTER COLUMN normalized_personnel_number SET NOT NULL;

-- Aynı kontrolün migration tekrar çalıştırıldığında yeniden eklenmesini önler.
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'ck_app_users_failed_login_count'
          AND conrelid = 'fault_management.app_users'::regclass
    ) THEN
        ALTER TABLE app_users
            ADD CONSTRAINT ck_app_users_failed_login_count
            CHECK (failed_login_count >= 0);
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'ck_service_tasks_actual_times'
          AND conrelid = 'fault_management.service_tasks'::regclass
    ) THEN
        ALTER TABLE service_tasks
            ADD CONSTRAINT ck_service_tasks_actual_times CHECK (
                actual_arrival_at IS NULL
                OR (actual_departure_at IS NOT NULL AND actual_arrival_at >= actual_departure_at)
            );
    END IF;
END $$;

-- ============================================================
-- 2) YENİ TABLOLAR
-- ============================================================

-- Bir aracın önemli olaylarını tek zaman çizelgesinde toplar.
CREATE TABLE IF NOT EXISTS vehicle_event_logs (
    id                  bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    vehicle_id          bigint NOT NULL REFERENCES vehicles(id) ON DELETE RESTRICT,
    fault_id            bigint REFERENCES faults(id) ON DELETE RESTRICT,
    service_task_id     bigint REFERENCES service_tasks(id) ON DELETE RESTRICT,
    event_type          varchar(50) NOT NULL,
    title               varchar(200) NOT NULL,
    description         varchar(1000),
    old_values          jsonb,
    new_values          jsonb,
    performed_by_user_id bigint REFERENCES app_users(id) ON DELETE RESTRICT,
    is_system_action    boolean NOT NULL DEFAULT false,
    occurred_at         timestamptz NOT NULL DEFAULT now(),
    created_at          timestamptz NOT NULL DEFAULT now()
);

-- Açık arıza süresi, geciken rapor vb. operasyonel uyarılar.
CREATE TABLE IF NOT EXISTS fault_alerts (
    id                  bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    fault_id            bigint NOT NULL REFERENCES faults(id) ON DELETE RESTRICT,
    alert_type          varchar(50) NOT NULL,
    title               varchar(200) NOT NULL,
    message             varchar(1000) NOT NULL,
    alert_status        varchar(20) NOT NULL DEFAULT 'OPEN',
    triggered_at        timestamptz NOT NULL DEFAULT now(),
    resolved_at         timestamptz,
    resolved_by_user_id bigint REFERENCES app_users(id) ON DELETE RESTRICT,
    resolution_note     varchar(1000),
    created_at          timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_fault_alerts_status
        CHECK (alert_status IN ('OPEN', 'ACKNOWLEDGED', 'RESOLVED')),
    CONSTRAINT ck_fault_alerts_resolution CHECK (
        (alert_status <> 'RESOLVED' AND resolved_at IS NULL AND resolved_by_user_id IS NULL)
        OR
        (alert_status = 'RESOLVED' AND resolved_at IS NOT NULL AND resolved_by_user_id IS NOT NULL)
    )
);

-- Teknisyenin tamir raporuna eklediği fotoğraf, video veya belgeler.
CREATE TABLE IF NOT EXISTS repair_report_attachments (
    id                  bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    repair_report_id    bigint NOT NULL REFERENCES repair_reports(id) ON DELETE RESTRICT,
    original_file_name  varchar(255) NOT NULL,
    stored_file_name    varchar(255) NOT NULL UNIQUE,
    file_path           varchar(1000) NOT NULL,
    content_type        varchar(150) NOT NULL,
    file_size           bigint NOT NULL,
    uploaded_by_user_id bigint NOT NULL REFERENCES app_users(id) ON DELETE RESTRICT,
    uploaded_at         timestamptz NOT NULL DEFAULT now(),
    is_active           boolean NOT NULL DEFAULT true,
    CONSTRAINT ck_repair_report_attachments_file_size CHECK (file_size > 0)
);

-- Kod değiştirmeden yönetilebilecek sistem ayarları.
CREATE TABLE IF NOT EXISTS system_settings (
    id                  bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    setting_key         varchar(120) NOT NULL UNIQUE,
    setting_value       jsonb NOT NULL,
    description         varchar(500),
    is_active           boolean NOT NULL DEFAULT true,
    updated_by_user_id  bigint REFERENCES app_users(id) ON DELETE RESTRICT,
    created_at          timestamptz NOT NULL DEFAULT now(),
    updated_at          timestamptz NOT NULL DEFAULT now()
);

-- Başlangıç ayarları. Aynı anahtar varsa mevcut değer değiştirilmez.
INSERT INTO system_settings (setting_key, setting_value, description)
VALUES
    ('automatic_team_assignment', 'true'::jsonb,
     'Arıza açıldığında uygun teknisyen ekibinin otomatik atanması.'),
    ('automatic_replacement_vehicle_assignment', 'true'::jsonb,
     'Araç arızalandığında kalan seferlere yedek aracın otomatik atanması.'),
    ('open_fault_alert_hours', '4'::jsonb,
     'Açık arıza için uyarı oluşturulmadan önce beklenecek saat.'),
    ('failed_login_limit', '5'::jsonb,
     'Kullanıcı hesabı geçici olarak kilitlenmeden önce izin verilen hatalı giriş sayısı.'),
    ('account_lock_minutes', '15'::jsonb,
     'Hatalı giriş sınırı aşıldığında uygulanacak kilit süresi.')
ON CONFLICT (setting_key) DO NOTHING;

-- ============================================================
-- 3) İNDEKSLER
-- ============================================================

CREATE UNIQUE INDEX IF NOT EXISTS uq_app_users_normalized_personnel_number
    ON app_users(normalized_personnel_number);

CREATE INDEX IF NOT EXISTS ix_faults_service_task_id
    ON faults(service_task_id)
    WHERE service_task_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS ix_vehicles_brand_model
    ON vehicles(brand, model);

CREATE INDEX IF NOT EXISTS ix_faults_vehicle_created
    ON faults(vehicle_id, created_at DESC);

CREATE INDEX IF NOT EXISTS ix_service_tasks_status_date
    ON service_tasks(status, service_date)
    WHERE is_active = true;

CREATE INDEX IF NOT EXISTS ix_notifications_service_task
    ON notifications(service_task_id)
    WHERE service_task_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS ix_notifications_transfer_batch
    ON notifications(task_transfer_batch_id)
    WHERE task_transfer_batch_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS ix_vehicle_event_logs_vehicle_date
    ON vehicle_event_logs(vehicle_id, occurred_at DESC);

CREATE INDEX IF NOT EXISTS ix_vehicle_event_logs_fault
    ON vehicle_event_logs(fault_id)
    WHERE fault_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS ix_fault_alerts_open
    ON fault_alerts(fault_id, triggered_at DESC)
    WHERE alert_status <> 'RESOLVED';

CREATE INDEX IF NOT EXISTS ix_repair_report_attachments_report
    ON repair_report_attachments(repair_report_id, uploaded_at DESC)
    WHERE is_active = true;

-- ============================================================
-- 4) RAPORLAMA GÖRÜNÜMLERİ (VIEW)
-- ============================================================

CREATE OR REPLACE VIEW vw_garage_occupancy AS
SELECT
    g.id AS garage_id,
    g.code AS garage_code,
    g.name AS garage_name,
    g.vehicle_capacity,
    COUNT(v.id) FILTER (WHERE v.is_active) AS active_vehicle_count,
    g.vehicle_capacity - COUNT(v.id) FILTER (WHERE v.is_active) AS remaining_capacity,
    CASE
        WHEN g.vehicle_capacity = 0 THEN 0::numeric
        ELSE round(
            COUNT(v.id) FILTER (WHERE v.is_active)::numeric * 100
            / g.vehicle_capacity,
            2
        )
    END AS occupancy_rate
FROM garages g
LEFT JOIN vehicles v ON v.garage_id = g.id
GROUP BY g.id, g.code, g.name, g.vehicle_capacity;

CREATE OR REPLACE VIEW vw_vehicle_fault_summary AS
SELECT
    v.id AS vehicle_id,
    v.door_number,
    v.plate,
    v.brand,
    v.model,
    g.name AS garage_name,
    COUNT(f.id) AS total_fault_count,
    COUNT(f.id) FILTER (WHERE f.closed_at IS NULL AND f.is_active) AS open_fault_count,
    COUNT(f.id) FILTER (WHERE f.closed_at IS NOT NULL AND f.is_active) AS closed_fault_count,
    MAX(f.occurred_at) AS last_fault_at
FROM vehicles v
JOIN garages g ON g.id = v.garage_id
LEFT JOIN faults f ON f.vehicle_id = v.id
GROUP BY v.id, v.door_number, v.plate, v.brand, v.model, g.name;

CREATE OR REPLACE VIEW vw_fault_resolution_times AS
SELECT
    f.id AS fault_id,
    f.fault_number,
    f.vehicle_id,
    v.door_number,
    f.garage_id,
    g.name AS garage_name,
    f.occurred_at,
    f.closed_at,
    CASE
        WHEN f.closed_at IS NULL THEN now() - f.occurred_at
        ELSE f.closed_at - f.occurred_at
    END AS fault_duration,
    (f.closed_at IS NOT NULL) AS is_closed
FROM faults f
JOIN vehicles v ON v.id = f.vehicle_id
JOIN garages g ON g.id = f.garage_id
WHERE f.is_active;

CREATE OR REPLACE VIEW vw_team_workload AS
SELECT
    t.id AS team_id,
    t.name AS team_name,
    t.garage_id,
    g.name AS garage_name,
    t.is_available,
    COUNT(fa.id) FILTER (WHERE fa.is_active) AS active_assignment_count,
    COUNT(fa.id) AS total_assignment_count,
    MAX(fa.assigned_at) AS last_assignment_at
FROM technician_teams t
JOIN garages g ON g.id = t.garage_id
LEFT JOIN fault_assignments fa ON fa.team_id = t.id
GROUP BY t.id, t.name, t.garage_id, g.name, t.is_available;

CREATE OR REPLACE VIEW vw_task_transfer_summary AS
SELECT
    tb.id AS transfer_batch_id,
    tb.fault_id,
    f.fault_number,
    tb.old_vehicle_id,
    old_v.door_number AS old_vehicle_door_number,
    tb.new_vehicle_id,
    new_v.door_number AS new_vehicle_door_number,
    tb.garage_id,
    g.name AS garage_name,
    tb.transfer_type,
    tb.transferred_task_count,
    tb.is_automatic,
    tb.transferred_at
FROM task_transfer_batches tb
JOIN faults f ON f.id = tb.fault_id
JOIN vehicles old_v ON old_v.id = tb.old_vehicle_id
JOIN vehicles new_v ON new_v.id = tb.new_vehicle_id
JOIN garages g ON g.id = tb.garage_id;

COMMIT;

-- ============================================================
-- 5) MIGRATION SONRASI KONTROL SORGULARI
-- ============================================================

SELECT table_name
FROM information_schema.tables
WHERE table_schema = 'fault_management'
  AND table_name IN (
      'vehicle_event_logs',
      'fault_alerts',
      'repair_report_attachments',
      'system_settings'
  )
ORDER BY table_name;

SELECT table_name
FROM information_schema.views
WHERE table_schema = 'fault_management'
  AND table_name LIKE 'vw_%'
ORDER BY table_name;

SELECT setting_key, setting_value
FROM fault_management.system_settings
ORDER BY setting_key;
-- Operasyon için araç/geçmiş/bildirim gibi tamamlayıcı alan, tablo ve kuralları ekler.
