BEGIN;

SET search_path TO fault_management, public;

-- 1) Hat tanımları
CREATE TABLE IF NOT EXISTS routes (
    id                  bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    code                varchar(30) NOT NULL UNIQUE,
    name                varchar(200) NOT NULL,
    start_point         varchar(200) NOT NULL,
    end_point           varchar(200) NOT NULL,
    is_active           boolean NOT NULL DEFAULT true,
    created_at          timestamptz NOT NULL DEFAULT now()
);

-- 2) Her satır bir sefer görevidir.
CREATE TABLE IF NOT EXISTS service_tasks (
    id                      bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    task_number             varchar(50) NOT NULL UNIQUE,
    route_id                bigint NOT NULL REFERENCES routes(id) ON DELETE RESTRICT,
    service_date            date NOT NULL,
    sequence_number         integer NOT NULL,
    planned_departure_at    timestamptz NOT NULL,
    planned_arrival_at      timestamptz NOT NULL,
    status                  varchar(30) NOT NULL DEFAULT 'PLANNED',
    is_active               boolean NOT NULL DEFAULT true,
    created_by_user_id      bigint NOT NULL REFERENCES app_users(id) ON DELETE RESTRICT,
    created_at              timestamptz NOT NULL DEFAULT now(),
    deactivated_at          timestamptz,
    deactivated_by_user_id  bigint REFERENCES app_users(id) ON DELETE RESTRICT,
    deactivation_reason     varchar(500),
    CONSTRAINT ck_service_tasks_sequence CHECK (sequence_number > 0),
    CONSTRAINT ck_service_tasks_time CHECK (planned_arrival_at > planned_departure_at),
    CONSTRAINT ck_service_tasks_status CHECK (
        status IN ('PLANNED', 'ASSIGNED', 'IN_PROGRESS', 'COMPLETED',
                   'TRANSFER_PENDING', 'CANCELLED')
    ),
    CONSTRAINT ck_service_tasks_deactivation CHECK (
        (is_active = true AND deactivated_at IS NULL AND deactivation_reason IS NULL)
        OR
        (is_active = false AND deactivated_at IS NOT NULL AND deactivation_reason IS NOT NULL)
    ),
    UNIQUE (route_id, service_date, sequence_number)
);

-- 3) Aynı arıza nedeniyle yapılan toplu görev devrinin üst kaydı
CREATE TABLE IF NOT EXISTS task_transfer_batches (
    id                      bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    fault_id                bigint NOT NULL REFERENCES faults(id) ON DELETE RESTRICT,
    old_vehicle_id          bigint NOT NULL REFERENCES vehicles(id) ON DELETE RESTRICT,
    new_vehicle_id          bigint NOT NULL REFERENCES vehicles(id) ON DELETE RESTRICT,
    driver_id               bigint REFERENCES drivers(id) ON DELETE RESTRICT,
    garage_id               bigint NOT NULL REFERENCES garages(id) ON DELETE RESTRICT,
    transfer_type           varchar(20) NOT NULL,
    transferred_task_count  integer NOT NULL,
    driver_can_continue     boolean NOT NULL DEFAULT true,
    is_automatic            boolean NOT NULL DEFAULT true,
    transferred_by_user_id  bigint REFERENCES app_users(id) ON DELETE RESTRICT,
    transferred_at          timestamptz NOT NULL DEFAULT now(),
    description             varchar(1000) NOT NULL,
    CONSTRAINT ck_task_transfer_batches_type CHECK (
        transfer_type IN ('REPLACEMENT', 'RETURN')
    ),
    CONSTRAINT ck_task_transfer_batches_count CHECK (transferred_task_count > 0),
    CONSTRAINT ck_task_transfer_batches_different_vehicle CHECK (old_vehicle_id <> new_vehicle_id)
);

-- 4) Bir görevin zaman içinde hangi araç ve şoförlere atandığının geçmişi
CREATE TABLE IF NOT EXISTS task_assignments (
    id                      bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    service_task_id         bigint NOT NULL REFERENCES service_tasks(id) ON DELETE RESTRICT,
    vehicle_id              bigint NOT NULL REFERENCES vehicles(id) ON DELETE RESTRICT,
    driver_id               bigint NOT NULL REFERENCES drivers(id) ON DELETE RESTRICT,
    transfer_batch_id       bigint REFERENCES task_transfer_batches(id) ON DELETE RESTRICT,
    assignment_type         varchar(20) NOT NULL DEFAULT 'ORIGINAL',
    assigned_by_user_id     bigint REFERENCES app_users(id) ON DELETE RESTRICT,
    assigned_at             timestamptz NOT NULL DEFAULT now(),
    ended_at                timestamptz,
    is_active               boolean NOT NULL DEFAULT true,
    description             varchar(1000),
    CONSTRAINT ck_task_assignments_type CHECK (
        assignment_type IN ('ORIGINAL', 'REPLACEMENT', 'RETURN')
    ),
    CONSTRAINT ck_task_assignments_dates CHECK (ended_at IS NULL OR ended_at >= assigned_at),
    CONSTRAINT ck_task_assignments_active_dates CHECK (
        (is_active = true AND ended_at IS NULL)
        OR
        (is_active = false AND ended_at IS NOT NULL)
    )
);

-- Bir görevin aynı anda yalnızca bir aktif araç/şoför ataması olabilir.
CREATE UNIQUE INDEX IF NOT EXISTS uq_task_assignments_active_task
    ON task_assignments(service_task_id)
    WHERE is_active = true;

CREATE INDEX IF NOT EXISTS ix_service_tasks_date_status
    ON service_tasks(service_date, status)
    WHERE is_active = true;

CREATE INDEX IF NOT EXISTS ix_service_tasks_route_date
    ON service_tasks(route_id, service_date, sequence_number);

CREATE INDEX IF NOT EXISTS ix_task_assignments_vehicle_active
    ON task_assignments(vehicle_id)
    WHERE is_active = true;

CREATE INDEX IF NOT EXISTS ix_task_assignments_driver_active
    ON task_assignments(driver_id)
    WHERE is_active = true;

CREATE INDEX IF NOT EXISTS ix_task_transfer_batches_fault
    ON task_transfer_batches(fault_id, transferred_at DESC);

-- Yeni araç durumları. Mevcut kayıtları bozmaz.
INSERT INTO vehicle_statuses (code, name, display_order)
VALUES
    ('AVAILABLE', 'Göreve Hazır', 1),
    ('ON_DUTY', 'Görevde', 2)
ON CONFLICT (code) DO UPDATE SET
    name = EXCLUDED.name,
    is_active = true;

-- Henüz görev ataması bulunmayan mevcut kullanımda araçları göreve hazır yapar.
UPDATE vehicles
SET vehicle_status_id = (
    SELECT id FROM vehicle_statuses WHERE code = 'AVAILABLE'
)
WHERE vehicle_status_id = (
    SELECT id FROM vehicle_statuses WHERE code = 'IN_SERVICE'
)
AND NOT EXISTS (
    SELECT 1
    FROM task_assignments ta
    WHERE ta.vehicle_id = vehicles.id AND ta.is_active = true
);

-- Görev yönetimi için rol yetkileri
INSERT INTO permissions (code, name, description)
VALUES
    ('tasks.view', 'Görevleri görüntüle', 'Sefer görevlerini ve atamalarını görüntüler.'),
    ('tasks.manage', 'Görevleri yönet', 'Sefer görevlerini oluşturur ve günceller.'),
    ('tasks.transfer', 'Görevleri aktar', 'Kalan görevleri yedek araca aktarır.')
ON CONFLICT (code) DO NOTHING;

INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM roles r
JOIN permissions p ON p.code IN ('tasks.view', 'tasks.manage', 'tasks.transfer')
WHERE r.name IN ('Admin', 'Merkez Yetkilisi')
ON CONFLICT DO NOTHING;

INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM roles r
JOIN permissions p ON p.code = 'tasks.view'
WHERE r.name IN ('Garaj Yetkilisi', 'Teknisyen')
ON CONFLICT DO NOTHING;

COMMIT;

-- Kurulum kontrolü
SELECT table_name
FROM information_schema.tables
WHERE table_schema = 'fault_management'
  AND table_name IN ('routes', 'service_tasks', 'task_transfer_batches', 'task_assignments')
ORDER BY table_name;
-- Hat, vardiya, servis görevi, araç/sürücü ataması ve görev devri veri modelini kurar.
