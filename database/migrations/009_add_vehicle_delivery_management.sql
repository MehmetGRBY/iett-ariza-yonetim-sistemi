BEGIN;

SET search_path TO fault_management, public;

-- Yedek aracın olay yerine götürülmesi ve teslim/işe devam süreci.
CREATE TABLE IF NOT EXISTS vehicle_delivery_assignments (
    id                      bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    delivery_number         varchar(50) NOT NULL UNIQUE,
    fault_id                bigint NOT NULL REFERENCES faults(id) ON DELETE RESTRICT,
    transfer_batch_id       bigint REFERENCES task_transfer_batches(id) ON DELETE RESTRICT,
    garage_id               bigint NOT NULL REFERENCES garages(id) ON DELETE RESTRICT,

    broken_vehicle_id       bigint NOT NULL REFERENCES vehicles(id) ON DELETE RESTRICT,
    replacement_vehicle_id  bigint NOT NULL REFERENCES vehicles(id) ON DELETE RESTRICT,
    support_vehicle_id      bigint REFERENCES vehicles(id) ON DELETE RESTRICT,

    delivery_driver_id      bigint NOT NULL REFERENCES drivers(id) ON DELETE RESTRICT,
    receiving_driver_id     bigint REFERENCES drivers(id) ON DELETE RESTRICT,

    delivery_mode           varchar(30) NOT NULL,
    delivery_status         varchar(30) NOT NULL DEFAULT 'PLANNED',
    planned_at              timestamptz NOT NULL DEFAULT now(),
    started_at              timestamptz,
    arrived_at              timestamptz,
    handed_over_at          timestamptz,
    completed_at            timestamptz,

    created_by_user_id      bigint NOT NULL REFERENCES app_users(id) ON DELETE RESTRICT,
    completed_by_user_id    bigint REFERENCES app_users(id) ON DELETE RESTRICT,
    description             varchar(1000) NOT NULL,
    completion_note         varchar(1000),
    is_automatic            boolean NOT NULL DEFAULT true,
    is_active               boolean NOT NULL DEFAULT true,
    created_at              timestamptz NOT NULL DEFAULT now(),

    CONSTRAINT ck_vehicle_delivery_mode CHECK (
        delivery_mode IN ('HANDOVER_TO_ORIGINAL_DRIVER', 'RESERVE_DRIVER_CONTINUES')
    ),
    CONSTRAINT ck_vehicle_delivery_status CHECK (
        delivery_status IN ('PLANNED', 'IN_PROGRESS', 'ARRIVED', 'COMPLETED', 'CANCELLED')
    ),
    CONSTRAINT ck_vehicle_delivery_different_vehicles CHECK (
        broken_vehicle_id <> replacement_vehicle_id
        AND (support_vehicle_id IS NULL OR support_vehicle_id <> broken_vehicle_id)
        AND (support_vehicle_id IS NULL OR support_vehicle_id <> replacement_vehicle_id)
    ),
    CONSTRAINT ck_vehicle_delivery_different_drivers CHECK (
        receiving_driver_id IS NULL OR receiving_driver_id <> delivery_driver_id
    ),
    CONSTRAINT ck_vehicle_delivery_handover_driver CHECK (
        delivery_mode <> 'HANDOVER_TO_ORIGINAL_DRIVER'
        OR receiving_driver_id IS NOT NULL
    ),
    CONSTRAINT ck_vehicle_delivery_continue_driver CHECK (
        delivery_mode <> 'RESERVE_DRIVER_CONTINUES'
        OR receiving_driver_id IS NULL
    ),
    CONSTRAINT ck_vehicle_delivery_times CHECK (
        (started_at IS NULL OR started_at >= planned_at)
        AND (arrived_at IS NULL OR (started_at IS NOT NULL AND arrived_at >= started_at))
        AND (handed_over_at IS NULL OR (arrived_at IS NOT NULL AND handed_over_at >= arrived_at))
        AND (completed_at IS NULL OR (started_at IS NOT NULL AND completed_at >= started_at))
    ),
    CONSTRAINT ck_vehicle_delivery_completion CHECK (
        (delivery_status = 'COMPLETED' AND completed_at IS NOT NULL AND completed_by_user_id IS NOT NULL)
        OR
        (delivery_status <> 'COMPLETED' AND completed_at IS NULL)
    )
);

-- Aynı arıza için aynı anda yalnızca bir aktif teslimat süreci olabilir.
CREATE UNIQUE INDEX IF NOT EXISTS uq_vehicle_delivery_active_fault
    ON vehicle_delivery_assignments(fault_id)
    WHERE is_active
      AND delivery_status IN ('PLANNED', 'IN_PROGRESS', 'ARRIVED');

CREATE INDEX IF NOT EXISTS ix_vehicle_delivery_garage_status
    ON vehicle_delivery_assignments(garage_id, delivery_status, planned_at DESC)
    WHERE is_active;

CREATE INDEX IF NOT EXISTS ix_vehicle_delivery_driver_status
    ON vehicle_delivery_assignments(delivery_driver_id, delivery_status)
    WHERE is_active;

CREATE INDEX IF NOT EXISTS ix_vehicle_delivery_replacement_vehicle
    ON vehicle_delivery_assignments(replacement_vehicle_id, created_at DESC);

CREATE INDEX IF NOT EXISTS ix_vehicle_delivery_transfer_batch
    ON vehicle_delivery_assignments(transfer_batch_id)
    WHERE transfer_batch_id IS NOT NULL;

-- Aktif teslimatların uygulamada kolay listelenmesi için view.
CREATE OR REPLACE VIEW vw_active_vehicle_deliveries AS
SELECT
    vda.id AS delivery_id,
    vda.delivery_number,
    vda.fault_id,
    f.fault_number,
    vda.transfer_batch_id,
    vda.garage_id,
    g.code AS garage_code,
    g.name AS garage_name,

    vda.broken_vehicle_id,
    broken_v.door_number AS broken_vehicle_door_number,
    broken_v.plate AS broken_vehicle_plate,

    vda.replacement_vehicle_id,
    replacement_v.door_number AS replacement_vehicle_door_number,
    replacement_v.plate AS replacement_vehicle_plate,

    vda.support_vehicle_id,
    support_v.door_number AS support_vehicle_door_number,
    support_v.plate AS support_vehicle_plate,

    vda.delivery_driver_id,
    delivery_d.personnel_number AS delivery_driver_personnel_number,
    concat_ws(' ', delivery_d.first_name, delivery_d.last_name) AS delivery_driver_name,

    vda.receiving_driver_id,
    receiving_d.personnel_number AS receiving_driver_personnel_number,
    concat_ws(' ', receiving_d.first_name, receiving_d.last_name) AS receiving_driver_name,

    vda.delivery_mode,
    vda.delivery_status,
    vda.planned_at,
    vda.started_at,
    vda.arrived_at,
    vda.description,
    vda.is_automatic,
    now() - COALESCE(vda.started_at, vda.planned_at) AS elapsed_time
FROM vehicle_delivery_assignments vda
JOIN faults f ON f.id = vda.fault_id
JOIN garages g ON g.id = vda.garage_id
JOIN vehicles broken_v ON broken_v.id = vda.broken_vehicle_id
JOIN vehicles replacement_v ON replacement_v.id = vda.replacement_vehicle_id
LEFT JOIN vehicles support_v ON support_v.id = vda.support_vehicle_id
JOIN drivers delivery_d ON delivery_d.id = vda.delivery_driver_id
LEFT JOIN drivers receiving_d ON receiving_d.id = vda.receiving_driver_id
WHERE vda.is_active
  AND vda.delivery_status IN ('PLANNED', 'IN_PROGRESS', 'ARRIVED');

-- Tamamlanmış ve iptal edilmiş kayıtlar dahil teslimat geçmişi özeti.
CREATE OR REPLACE VIEW vw_vehicle_delivery_history AS
SELECT
    vda.id AS delivery_id,
    vda.delivery_number,
    f.fault_number,
    g.name AS garage_name,
    broken_v.door_number AS broken_vehicle_door_number,
    replacement_v.door_number AS replacement_vehicle_door_number,
    delivery_d.personnel_number AS delivery_driver_personnel_number,
    concat_ws(' ', delivery_d.first_name, delivery_d.last_name) AS delivery_driver_name,
    receiving_d.personnel_number AS receiving_driver_personnel_number,
    concat_ws(' ', receiving_d.first_name, receiving_d.last_name) AS receiving_driver_name,
    vda.delivery_mode,
    vda.delivery_status,
    vda.planned_at,
    vda.started_at,
    vda.arrived_at,
    vda.handed_over_at,
    vda.completed_at,
    CASE
        WHEN vda.completed_at IS NOT NULL AND vda.started_at IS NOT NULL
        THEN vda.completed_at - vda.started_at
    END AS total_delivery_duration,
    vda.description,
    vda.completion_note,
    vda.is_automatic
FROM vehicle_delivery_assignments vda
JOIN faults f ON f.id = vda.fault_id
JOIN garages g ON g.id = vda.garage_id
JOIN vehicles broken_v ON broken_v.id = vda.broken_vehicle_id
JOIN vehicles replacement_v ON replacement_v.id = vda.replacement_vehicle_id
JOIN drivers delivery_d ON delivery_d.id = vda.delivery_driver_id
LEFT JOIN drivers receiving_d ON receiving_d.id = vda.receiving_driver_id;

-- Uygulama rolleri için teslimat izinleri.
INSERT INTO permissions (code, name, description)
VALUES
    ('deliveries.view', 'Araç teslimatlarını görüntüle',
     'Yedek araç teslimatlarını ve geçmişini görüntüler.'),
    ('deliveries.manage', 'Araç teslimatlarını yönet',
     'Yedek araç teslimatını oluşturur ve durumunu günceller.')
ON CONFLICT (code) DO NOTHING;

INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM roles r
JOIN permissions p ON p.code IN ('deliveries.view', 'deliveries.manage')
WHERE r.name IN ('Admin', 'Merkez Yetkilisi')
ON CONFLICT DO NOTHING;

INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM roles r
JOIN permissions p ON p.code = 'deliveries.view'
WHERE r.name = 'Garaj Yetkilisi'
ON CONFLICT DO NOTHING;

COMMIT;

-- Kurulum kontrolü: 1 tablo ve 2 view görünmelidir.
SELECT table_name, table_type
FROM information_schema.tables
WHERE table_schema = 'fault_management'
  AND table_name IN (
      'vehicle_delivery_assignments',
      'vw_active_vehicle_deliveries',
      'vw_vehicle_delivery_history'
  )
ORDER BY table_name;
-- Hizmet/teslim araçlarının sürücü, hedef ve durum geçmişini takip edecek yapıyı kurar.
