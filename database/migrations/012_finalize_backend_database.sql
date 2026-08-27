BEGIN;

SET search_path TO fault_management, public;

-- ============================================================
-- 1) GÜNLÜK GÖREV / SEFER GRUBU
-- Örnek: Bir aracın aynı gün yapacağı 6 sefer tek service_duty altında tutulur.
-- ============================================================
CREATE TABLE IF NOT EXISTS service_duties (
    id                      bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    duty_number             varchar(50) NOT NULL UNIQUE,
    service_date            date NOT NULL,
    garage_id               bigint NOT NULL REFERENCES garages(id) ON DELETE RESTRICT,
    route_id                bigint NOT NULL REFERENCES routes(id) ON DELETE RESTRICT,
    original_vehicle_id     bigint REFERENCES vehicles(id) ON DELETE RESTRICT,
    original_driver_id      bigint REFERENCES drivers(id) ON DELETE RESTRICT,
    status                  varchar(30) NOT NULL DEFAULT 'PLANNED',
    description             varchar(1000),
    created_by_user_id      bigint NOT NULL REFERENCES app_users(id) ON DELETE RESTRICT,
    created_at              timestamptz NOT NULL DEFAULT now(),
    completed_at            timestamptz,
    is_active               boolean NOT NULL DEFAULT true,
    deactivated_at          timestamptz,
    deactivated_by_user_id  bigint REFERENCES app_users(id) ON DELETE RESTRICT,
    deactivation_reason     varchar(500),
    CONSTRAINT ck_service_duties_status CHECK (
        status IN ('PLANNED', 'ACTIVE', 'INTERRUPTED', 'COMPLETED', 'CANCELLED')
    ),
    CONSTRAINT ck_service_duties_completion CHECK (
        (status = 'COMPLETED' AND completed_at IS NOT NULL)
        OR
        (status <> 'COMPLETED' AND completed_at IS NULL)
    ),
    CONSTRAINT ck_service_duties_deactivation CHECK (
        (is_active AND deactivated_at IS NULL AND deactivation_reason IS NULL)
        OR
        (NOT is_active AND deactivated_at IS NOT NULL AND deactivation_reason IS NOT NULL)
    )
);

ALTER TABLE service_tasks
    ADD COLUMN IF NOT EXISTS service_duty_id bigint
        REFERENCES service_duties(id) ON DELETE RESTRICT;

-- Tablo şu anda boş olduğu için günlük görev bağlantısını zorunlu yapabiliriz.
ALTER TABLE service_tasks
    ALTER COLUMN service_duty_id SET NOT NULL;

-- Eski kural aynı hatta aynı sıra numarasıyla birden fazla araç çalışmasını
-- engelliyordu. Doğru kural: sıra numarası günlük görev içinde benzersizdir.
ALTER TABLE service_tasks
    DROP CONSTRAINT IF EXISTS service_tasks_route_id_service_date_sequence_number_key;

CREATE UNIQUE INDEX IF NOT EXISTS uq_service_tasks_duty_sequence
    ON service_tasks(service_duty_id, sequence_number);

ALTER TABLE task_transfer_batches
    ADD COLUMN IF NOT EXISTS service_duty_id bigint
        REFERENCES service_duties(id) ON DELETE RESTRICT;

CREATE INDEX IF NOT EXISTS ix_service_duties_date_garage_status
    ON service_duties(service_date, garage_id, status)
    WHERE is_active;

CREATE INDEX IF NOT EXISTS ix_service_duties_vehicle_date
    ON service_duties(original_vehicle_id, service_date)
    WHERE is_active AND original_vehicle_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS ix_service_duties_driver_date
    ON service_duties(original_driver_id, service_date)
    WHERE is_active AND original_driver_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS ix_task_transfer_batches_duty
    ON task_transfer_batches(service_duty_id, transferred_at DESC)
    WHERE service_duty_id IS NOT NULL;

-- ============================================================
-- 2) GÜVENLİ ŞİFRE SIFIRLAMA ALTYAPISI
-- Veritabanında açık token değil, yalnızca token hash'i saklanır.
-- ============================================================
ALTER TABLE app_users
    ADD COLUMN IF NOT EXISTS security_stamp uuid NOT NULL DEFAULT gen_random_uuid();

CREATE TABLE IF NOT EXISTS password_reset_requests (
    id                      bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    user_id                 bigint NOT NULL REFERENCES app_users(id) ON DELETE RESTRICT,
    request_type            varchar(30) NOT NULL DEFAULT 'SELF_SERVICE',
    token_hash              varchar(128) NOT NULL UNIQUE,
    requested_by_user_id    bigint REFERENCES app_users(id) ON DELETE RESTRICT,
    requested_ip_address    inet,
    requested_at            timestamptz NOT NULL DEFAULT now(),
    expires_at              timestamptz NOT NULL,
    used_at                 timestamptz,
    revoked_at              timestamptz,
    revoke_reason           varchar(500),
    CONSTRAINT ck_password_reset_request_type CHECK (
        request_type IN ('SELF_SERVICE', 'ADMIN_RESET')
    ),
    CONSTRAINT ck_password_reset_expiry CHECK (
        expires_at > requested_at
    ),
    CONSTRAINT ck_password_reset_final_state CHECK (
        NOT (used_at IS NOT NULL AND revoked_at IS NOT NULL)
    ),
    CONSTRAINT ck_password_reset_admin_request CHECK (
        request_type <> 'ADMIN_RESET' OR requested_by_user_id IS NOT NULL
    )
);

CREATE INDEX IF NOT EXISTS ix_password_reset_user_requested
    ON password_reset_requests(user_id, requested_at DESC);

CREATE INDEX IF NOT EXISTS ix_password_reset_open_expiry
    ON password_reset_requests(expires_at)
    WHERE used_at IS NULL AND revoked_at IS NULL;

-- ============================================================
-- 3) BACKEND İÇİN HAZIR RAPOR VIEW'LARI
-- ============================================================
CREATE OR REPLACE VIEW vw_service_duty_summary AS
SELECT
    sd.id AS service_duty_id,
    sd.duty_number,
    sd.service_date,
    sd.status AS duty_status,
    sd.garage_id,
    g.code AS garage_code,
    g.name AS garage_name,
    sd.route_id,
    r.code AS route_code,
    r.name AS route_name,
    sd.original_vehicle_id,
    v.door_number AS original_vehicle_door_number,
    sd.original_driver_id,
    d.personnel_number AS original_driver_personnel_number,
    concat_ws(' ', d.first_name, d.last_name) AS original_driver_name,
    COUNT(st.id) FILTER (WHERE st.is_active) AS total_task_count,
    COUNT(st.id) FILTER (WHERE st.is_active AND st.status = 'COMPLETED') AS completed_task_count,
    COUNT(st.id) FILTER (
        WHERE st.is_active
          AND st.status NOT IN ('COMPLETED', 'CANCELLED')
    ) AS remaining_task_count,
    COUNT(st.id) FILTER (WHERE st.is_active AND st.status = 'TRANSFER_PENDING') AS transfer_pending_count,
    MIN(st.planned_departure_at) FILTER (WHERE st.is_active) AS first_planned_departure,
    MAX(st.planned_arrival_at) FILTER (WHERE st.is_active) AS last_planned_arrival
FROM service_duties sd
JOIN garages g ON g.id = sd.garage_id
JOIN routes r ON r.id = sd.route_id
LEFT JOIN vehicles v ON v.id = sd.original_vehicle_id
LEFT JOIN drivers d ON d.id = sd.original_driver_id
LEFT JOIN service_tasks st ON st.service_duty_id = sd.id
GROUP BY
    sd.id, sd.duty_number, sd.service_date, sd.status,
    sd.garage_id, g.code, g.name,
    sd.route_id, r.code, r.name,
    sd.original_vehicle_id, v.door_number,
    sd.original_driver_id, d.personnel_number, d.first_name, d.last_name;

-- Token hash'i özellikle view'a dahil edilmez.
CREATE OR REPLACE VIEW vw_pending_password_resets AS
SELECT
    pr.id AS password_reset_request_id,
    pr.user_id,
    u.personnel_number,
    u.first_name,
    u.last_name,
    pr.request_type,
    pr.requested_by_user_id,
    pr.requested_at,
    pr.expires_at,
    (pr.expires_at <= now()) AS is_expired
FROM password_reset_requests pr
JOIN app_users u ON u.id = pr.user_id
WHERE pr.used_at IS NULL
  AND pr.revoked_at IS NULL;

-- ============================================================
-- 4) UYGULAMA YETKİLERİ
-- ============================================================
INSERT INTO permissions (code, name, description)
VALUES
    ('duties.view', 'Günlük görevleri görüntüle',
     'Günlük görevleri ve bağlı seferleri görüntüler.'),
    ('duties.manage', 'Günlük görevleri yönet',
     'Günlük görev oluşturur, günceller ve seferlerini planlar.'),
    ('users.reset_password', 'Kullanıcı şifresi sıfırla',
     'Kullanıcı için güvenli şifre sıfırlama işlemi başlatır.')
ON CONFLICT (code) DO NOTHING;

INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM roles r
JOIN permissions p ON p.code IN ('duties.view', 'duties.manage', 'users.reset_password')
WHERE r.name = 'Admin'
ON CONFLICT DO NOTHING;

INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM roles r
JOIN permissions p ON p.code IN ('duties.view', 'duties.manage')
WHERE r.name = 'Merkez Yetkilisi'
ON CONFLICT DO NOTHING;

INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM roles r
JOIN permissions p ON p.code = 'duties.view'
WHERE r.name = 'Garaj Yetkilisi'
ON CONFLICT DO NOTHING;

INSERT INTO audit_logs (
    user_id,
    role_id,
    action,
    entity_type,
    new_values,
    description
)
SELECT
    u.id,
    u.role_id,
    'DATABASE_BACKEND_FINALIZATION',
    'database_schema',
    jsonb_build_object(
        'tablesAdded', jsonb_build_array('service_duties', 'password_reset_requests'),
        'viewsAdded', jsonb_build_array('vw_service_duty_summary', 'vw_pending_password_resets'),
        'executedAt', now()
    ),
    'Backend geliştirmesi öncesi günlük görev ve şifre sıfırlama şeması tamamlandı.'
FROM app_users u
WHERE u.personnel_number = 'ADM-0001';

COMMIT;

-- ============================================================
-- 5) SON KONTROL
-- ============================================================
SELECT table_name, table_type
FROM information_schema.tables
WHERE table_schema = 'fault_management'
  AND table_name IN (
      'service_duties',
      'password_reset_requests',
      'vw_service_duty_summary',
      'vw_pending_password_resets'
  )
ORDER BY table_name;

SELECT
    (SELECT COUNT(*) FROM information_schema.tables
     WHERE table_schema = 'fault_management' AND table_type = 'BASE TABLE') AS total_tables,
    (SELECT COUNT(*) FROM information_schema.views
     WHERE table_schema = 'fault_management') AS total_views,
    (SELECT COUNT(*) FROM permissions) AS total_permissions;
-- Backend başlamadan önce eksik constraint, index, kolon ve veri düzeltmelerini tek migration'da tamamlar.
