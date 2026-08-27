BEGIN;

CREATE SCHEMA IF NOT EXISTS fault_management;
SET search_path TO fault_management, public;

-- 1) Yetkilendirme
CREATE TABLE roles (
    id                  bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    name                varchar(80) NOT NULL UNIQUE,
    description         varchar(500),
    is_active           boolean NOT NULL DEFAULT true,
    created_at          timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE permissions (
    id                  bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    code                varchar(120) NOT NULL UNIQUE,
    name                varchar(120) NOT NULL,
    description         varchar(500)
);

CREATE TABLE role_permissions (
    role_id             bigint NOT NULL REFERENCES roles(id) ON DELETE CASCADE,
    permission_id       bigint NOT NULL REFERENCES permissions(id) ON DELETE CASCADE,
    PRIMARY KEY (role_id, permission_id)
);

-- 2) Organizasyon ve kullanıcılar
CREATE TABLE garages (
    id                  bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    code                varchar(30) NOT NULL UNIQUE,
    name                varchar(150) NOT NULL UNIQUE,
    address             varchar(500),
    vehicle_capacity    integer NOT NULL DEFAULT 0,
    is_active           boolean NOT NULL DEFAULT true,
    created_at          timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_garages_vehicle_capacity CHECK (vehicle_capacity >= 0)
);

CREATE TABLE app_users (
    id                      bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    personnel_number        varchar(30) NOT NULL UNIQUE,
    first_name              varchar(100) NOT NULL,
    last_name               varchar(100) NOT NULL,
    gender_code             varchar(10) NOT NULL,
    password_hash           text NOT NULL,
    role_id                 bigint NOT NULL REFERENCES roles(id) ON DELETE RESTRICT,
    garage_id               bigint REFERENCES garages(id) ON DELETE RESTRICT,
    is_active               boolean NOT NULL DEFAULT true,
    created_at              timestamptz NOT NULL DEFAULT now(),
    deactivated_at          timestamptz,
    deactivated_by_user_id  bigint REFERENCES app_users(id) ON DELETE RESTRICT,
    deactivation_reason     varchar(500),
    CONSTRAINT ck_app_users_gender CHECK (gender_code IN ('MALE', 'FEMALE')),
    CONSTRAINT ck_app_users_deactivation CHECK (
        (is_active = true AND deactivated_at IS NULL AND deactivation_reason IS NULL)
        OR
        (is_active = false AND deactivated_at IS NOT NULL AND deactivation_reason IS NOT NULL)
    )
);

CREATE TABLE drivers (
    id                  bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    personnel_number    varchar(30) NOT NULL UNIQUE,
    first_name          varchar(100) NOT NULL,
    last_name           varchar(100) NOT NULL,
    gender_code         varchar(10) NOT NULL,
    is_active           boolean NOT NULL DEFAULT true,
    created_at          timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_drivers_gender CHECK (gender_code IN ('MALE', 'FEMALE'))
);

-- 3) Araç tanımları
CREATE TABLE vehicle_types (
    id                  bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    name                varchar(80) NOT NULL UNIQUE,
    is_active           boolean NOT NULL DEFAULT true
);

CREATE TABLE fuel_types (
    id                  bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    name                varchar(80) NOT NULL UNIQUE,
    is_active           boolean NOT NULL DEFAULT true
);

CREATE TABLE vehicle_statuses (
    id                  bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    code                varchar(50) NOT NULL UNIQUE,
    name                varchar(80) NOT NULL UNIQUE,
    display_order       integer NOT NULL DEFAULT 0,
    is_active           boolean NOT NULL DEFAULT true
);

CREATE TABLE vehicles (
    id                  bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    door_number         varchar(30) NOT NULL UNIQUE,
    plate               varchar(20) NOT NULL UNIQUE,
    brand               varchar(100) NOT NULL,
    model               varchar(120) NOT NULL,
    model_year          smallint NOT NULL,
    vehicle_type_id     bigint NOT NULL REFERENCES vehicle_types(id) ON DELETE RESTRICT,
    fuel_type_id        bigint NOT NULL REFERENCES fuel_types(id) ON DELETE RESTRICT,
    current_mileage     integer NOT NULL DEFAULT 0,
    garage_id           bigint NOT NULL REFERENCES garages(id) ON DELETE RESTRICT,
    vehicle_status_id   bigint NOT NULL REFERENCES vehicle_statuses(id) ON DELETE RESTRICT,
    duty_type           varchar(100),
    capacity            integer,
    is_active           boolean NOT NULL DEFAULT true,
    created_at          timestamptz NOT NULL DEFAULT now(),
    deactivated_at      timestamptz,
    deactivation_reason varchar(500),
    CONSTRAINT ck_vehicles_model_year CHECK (model_year BETWEEN 1950 AND 2100),
    CONSTRAINT ck_vehicles_mileage CHECK (current_mileage >= 0),
    CONSTRAINT ck_vehicles_capacity CHECK (capacity IS NULL OR capacity > 0)
);

CREATE TABLE vehicle_garage_histories (
    id                  bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    vehicle_id          bigint NOT NULL REFERENCES vehicles(id) ON DELETE RESTRICT,
    old_garage_id       bigint REFERENCES garages(id) ON DELETE RESTRICT,
    new_garage_id       bigint NOT NULL REFERENCES garages(id) ON DELETE RESTRICT,
    changed_by_user_id  bigint NOT NULL REFERENCES app_users(id) ON DELETE RESTRICT,
    changed_at          timestamptz NOT NULL DEFAULT now(),
    description         varchar(500) NOT NULL,
    CONSTRAINT ck_vehicle_garage_change CHECK (old_garage_id IS NULL OR old_garage_id <> new_garage_id)
);

CREATE TABLE vehicle_status_histories (
    id                  bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    vehicle_id          bigint NOT NULL REFERENCES vehicles(id) ON DELETE RESTRICT,
    old_status_id       bigint REFERENCES vehicle_statuses(id) ON DELETE RESTRICT,
    new_status_id       bigint NOT NULL REFERENCES vehicle_statuses(id) ON DELETE RESTRICT,
    changed_by_user_id  bigint NOT NULL REFERENCES app_users(id) ON DELETE RESTRICT,
    changed_at          timestamptz NOT NULL DEFAULT now(),
    description         varchar(500) NOT NULL
);

-- 4) Arıza tanımları ve kayıtları
CREATE TABLE fault_categories (
    id                  bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    name                varchar(120) NOT NULL,
    parent_category_id  bigint REFERENCES fault_categories(id) ON DELETE RESTRICT,
    is_active           boolean NOT NULL DEFAULT true,
    created_at          timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_fault_categories_not_self_parent CHECK (parent_category_id IS NULL OR parent_category_id <> id)
);

CREATE UNIQUE INDEX uq_fault_categories_root_name
    ON fault_categories(name)
    WHERE parent_category_id IS NULL;

CREATE UNIQUE INDEX uq_fault_categories_child_name
    ON fault_categories(parent_category_id, name)
    WHERE parent_category_id IS NOT NULL;

CREATE TABLE fault_statuses (
    id                  bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    code                varchar(50) NOT NULL UNIQUE,
    name                varchar(80) NOT NULL UNIQUE,
    is_closed_status    boolean NOT NULL DEFAULT false,
    display_order       integer NOT NULL DEFAULT 0,
    is_active           boolean NOT NULL DEFAULT true
);

CREATE TABLE faults (
    id                      bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    fault_number            varchar(40) NOT NULL UNIQUE,
    vehicle_id              bigint NOT NULL REFERENCES vehicles(id) ON DELETE RESTRICT,
    driver_id               bigint NOT NULL REFERENCES drivers(id) ON DELETE RESTRICT,
    created_by_user_id      bigint NOT NULL REFERENCES app_users(id) ON DELETE RESTRICT,
    garage_id               bigint NOT NULL REFERENCES garages(id) ON DELETE RESTRICT,
    fault_category_id       bigint NOT NULL REFERENCES fault_categories(id) ON DELETE RESTRICT,
    fault_status_id         bigint NOT NULL REFERENCES fault_statuses(id) ON DELETE RESTRICT,
    description             text NOT NULL,
    mileage_at_failure      integer NOT NULL,
    latitude                numeric(9,6) NOT NULL,
    longitude               numeric(9,6) NOT NULL,
    location_description    varchar(500),
    occurred_at             timestamptz NOT NULL,
    created_at              timestamptz NOT NULL DEFAULT now(),
    closed_at               timestamptz,
    is_active               boolean NOT NULL DEFAULT true,
    deactivated_at          timestamptz,
    deactivated_by_user_id  bigint REFERENCES app_users(id) ON DELETE RESTRICT,
    deactivation_reason     varchar(500),
    CONSTRAINT ck_faults_mileage CHECK (mileage_at_failure >= 0),
    CONSTRAINT ck_faults_latitude CHECK (latitude BETWEEN -90 AND 90),
    CONSTRAINT ck_faults_longitude CHECK (longitude BETWEEN -180 AND 180),
    CONSTRAINT ck_faults_closed_at CHECK (closed_at IS NULL OR closed_at >= occurred_at),
    CONSTRAINT ck_faults_deactivation CHECK (
        (is_active = true AND deactivated_at IS NULL AND deactivation_reason IS NULL)
        OR
        (is_active = false AND deactivated_at IS NOT NULL AND deactivation_reason IS NOT NULL)
    )
);

CREATE TABLE fault_attachments (
    id                  bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    fault_id            bigint NOT NULL REFERENCES faults(id) ON DELETE RESTRICT,
    original_file_name  varchar(255) NOT NULL,
    stored_file_name    varchar(255) NOT NULL UNIQUE,
    file_path           varchar(1000) NOT NULL,
    content_type        varchar(150) NOT NULL,
    file_size           bigint NOT NULL,
    uploaded_by_user_id bigint NOT NULL REFERENCES app_users(id) ON DELETE RESTRICT,
    uploaded_at         timestamptz NOT NULL DEFAULT now(),
    is_active           boolean NOT NULL DEFAULT true,
    CONSTRAINT ck_fault_attachments_file_size CHECK (file_size > 0)
);

CREATE TABLE fault_status_histories (
    id                  bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    fault_id            bigint NOT NULL REFERENCES faults(id) ON DELETE RESTRICT,
    old_status_id       bigint REFERENCES fault_statuses(id) ON DELETE RESTRICT,
    new_status_id       bigint NOT NULL REFERENCES fault_statuses(id) ON DELETE RESTRICT,
    changed_by_user_id  bigint NOT NULL REFERENCES app_users(id) ON DELETE RESTRICT,
    changed_by_role_id  bigint NOT NULL REFERENCES roles(id) ON DELETE RESTRICT,
    description         varchar(1000) NOT NULL,
    is_system_action    boolean NOT NULL DEFAULT false,
    changed_at          timestamptz NOT NULL DEFAULT now()
);

-- Arıza kaydı tanımlandıktan sonra durum geçmişine arıza bağlantısı eklenir.
ALTER TABLE vehicle_status_histories
    ADD COLUMN fault_id bigint REFERENCES faults(id) ON DELETE RESTRICT;

-- 5) Teknisyen ekipleri, atamalar ve raporlar
CREATE TABLE technician_teams (
    id                  bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    name                varchar(120) NOT NULL,
    garage_id           bigint NOT NULL REFERENCES garages(id) ON DELETE RESTRICT,
    is_available        boolean NOT NULL DEFAULT true,
    last_assigned_at    timestamptz,
    is_active           boolean NOT NULL DEFAULT true,
    created_at          timestamptz NOT NULL DEFAULT now(),
    UNIQUE (garage_id, name)
);

CREATE TABLE team_members (
    id                  bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    team_id             bigint NOT NULL REFERENCES technician_teams(id) ON DELETE RESTRICT,
    user_id             bigint NOT NULL REFERENCES app_users(id) ON DELETE RESTRICT,
    is_team_leader      boolean NOT NULL DEFAULT false,
    joined_at           timestamptz NOT NULL DEFAULT now(),
    left_at             timestamptz,
    is_active           boolean NOT NULL DEFAULT true,
    CONSTRAINT ck_team_members_dates CHECK (left_at IS NULL OR left_at >= joined_at)
);

-- Bir kullanıcı aynı anda yalnızca bir aktif ekipte olabilir.
CREATE UNIQUE INDEX uq_team_members_active_user
    ON team_members(user_id)
    WHERE is_active = true;

CREATE TABLE fault_assignments (
    id                  bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    fault_id            bigint NOT NULL REFERENCES faults(id) ON DELETE RESTRICT,
    team_id             bigint NOT NULL REFERENCES technician_teams(id) ON DELETE RESTRICT,
    assigned_by_user_id bigint REFERENCES app_users(id) ON DELETE RESTRICT,
    is_automatic        boolean NOT NULL DEFAULT true,
    assigned_at         timestamptz NOT NULL DEFAULT now(),
    started_at          timestamptz,
    completed_at        timestamptz,
    is_active           boolean NOT NULL DEFAULT true,
    CONSTRAINT ck_fault_assignments_dates CHECK (
        (started_at IS NULL OR started_at >= assigned_at)
        AND (completed_at IS NULL OR (started_at IS NOT NULL AND completed_at >= started_at))
    )
);

-- Aynı arızanın aynı anda yalnızca bir aktif ekip ataması olabilir.
CREATE UNIQUE INDEX uq_fault_assignments_active_fault
    ON fault_assignments(fault_id)
    WHERE is_active = true;

CREATE TABLE repair_reports (
    id                  bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    fault_assignment_id bigint NOT NULL REFERENCES fault_assignments(id) ON DELETE RESTRICT,
    created_by_user_id  bigint NOT NULL REFERENCES app_users(id) ON DELETE RESTRICT,
    result              varchar(30) NOT NULL,
    description         text NOT NULL,
    started_at          timestamptz NOT NULL,
    completed_at        timestamptz NOT NULL,
    submitted_at        timestamptz,
    is_submitted        boolean NOT NULL DEFAULT false,
    is_active           boolean NOT NULL DEFAULT true,
    created_at          timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_repair_reports_result CHECK (result IN ('RESOLVED', 'UNRESOLVED')),
    CONSTRAINT ck_repair_reports_dates CHECK (completed_at >= started_at),
    CONSTRAINT ck_repair_reports_submission CHECK (
        (is_submitted = false AND submitted_at IS NULL)
        OR
        (is_submitted = true AND submitted_at IS NOT NULL)
    )
);

CREATE TABLE repair_report_actions (
    id                  bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    repair_report_id    bigint NOT NULL REFERENCES repair_reports(id) ON DELETE RESTRICT,
    description         text NOT NULL,
    performed_at        timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE repair_report_parts (
    id                  bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    repair_report_id    bigint NOT NULL REFERENCES repair_reports(id) ON DELETE RESTRICT,
    part_name           varchar(200) NOT NULL,
    quantity            numeric(12,3) NOT NULL,
    description         varchar(500),
    CONSTRAINT ck_repair_report_parts_quantity CHECK (quantity > 0)
);

-- 6) Bildirim ve denetim
CREATE TABLE notifications (
    id                  bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    user_id             bigint NOT NULL REFERENCES app_users(id) ON DELETE RESTRICT,
    fault_id            bigint REFERENCES faults(id) ON DELETE RESTRICT,
    title               varchar(200) NOT NULL,
    message             varchar(1000) NOT NULL,
    is_read             boolean NOT NULL DEFAULT false,
    created_at          timestamptz NOT NULL DEFAULT now(),
    read_at             timestamptz,
    CONSTRAINT ck_notifications_read CHECK (
        (is_read = false AND read_at IS NULL)
        OR
        (is_read = true AND read_at IS NOT NULL)
    )
);

CREATE TABLE audit_logs (
    id                  bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    user_id             bigint REFERENCES app_users(id) ON DELETE RESTRICT,
    role_id             bigint REFERENCES roles(id) ON DELETE RESTRICT,
    action              varchar(50) NOT NULL,
    entity_type         varchar(120) NOT NULL,
    entity_id           bigint,
    old_values          jsonb,
    new_values          jsonb,
    description         varchar(1000),
    ip_address          inet,
    created_at          timestamptz NOT NULL DEFAULT now()
);

-- 7) Sık kullanılacak sorgular için indeksler
CREATE INDEX ix_app_users_role_id ON app_users(role_id);
CREATE INDEX ix_app_users_garage_id ON app_users(garage_id);
CREATE INDEX ix_vehicles_garage_status ON vehicles(garage_id, vehicle_status_id) WHERE is_active = true;
CREATE INDEX ix_faults_vehicle_id ON faults(vehicle_id);
CREATE INDEX ix_faults_driver_id ON faults(driver_id);
CREATE INDEX ix_faults_garage_status_created ON faults(garage_id, fault_status_id, created_at DESC) WHERE is_active = true;
CREATE INDEX ix_faults_category_id ON faults(fault_category_id);
CREATE INDEX ix_faults_occurred_at ON faults(occurred_at DESC);
CREATE INDEX ix_fault_status_histories_fault_changed ON fault_status_histories(fault_id, changed_at DESC);
CREATE INDEX ix_fault_assignments_team_active ON fault_assignments(team_id) WHERE is_active = true;
CREATE INDEX ix_notifications_user_unread ON notifications(user_id, created_at DESC) WHERE is_read = false;
CREATE INDEX ix_audit_logs_entity ON audit_logs(entity_type, entity_id, created_at DESC);
CREATE INDEX ix_audit_logs_user_created ON audit_logs(user_id, created_at DESC);

-- 8) Başlangıç verileri
INSERT INTO roles (name, description) VALUES
    ('Admin', 'Sistemin tüm yönetim ve denetim yetkilerine sahiptir.'),
    ('Merkez Yetkilisi', 'Arıza açar, raporları inceler ve arıza durumunu yönetir.'),
    ('Garaj Yetkilisi', 'Yalnızca kendi garajındaki araç ve arızaları takip eder.'),
    ('Teknisyen', 'Ekibine atanan arızalar için tamir raporu oluşturur.');

INSERT INTO permissions (code, name) VALUES
    ('users.manage', 'Kullanıcıları yönet'),
    ('roles.manage', 'Rol ve yetkileri yönet'),
    ('garages.manage', 'Garajları yönet'),
    ('vehicles.view.all', 'Tüm araçları görüntüle'),
    ('vehicles.view.own_garage', 'Kendi garajındaki araçları görüntüle'),
    ('vehicles.manage', 'Araçları yönet'),
    ('faults.create', 'Arıza oluştur'),
    ('faults.view.all', 'Tüm arızaları görüntüle'),
    ('faults.view.own_garage', 'Kendi garajındaki arızaları görüntüle'),
    ('faults.view.assigned', 'Atanan arızaları görüntüle'),
    ('faults.update', 'Arızayı güncelle'),
    ('faults.change_status', 'Arıza durumunu değiştir'),
    ('faults.deactivate', 'Arızayı pasife al'),
    ('reports.create', 'Tamir raporu oluştur'),
    ('reports.view', 'Tamir raporlarını görüntüle'),
    ('audit.view', 'Denetim kayıtlarını görüntüle'),
    ('categories.manage', 'Arıza kategorilerini yönet'),
    ('dashboard.view', 'Dashboard görüntüle');

-- Admin bütün izinleri alır.
INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM roles r
CROSS JOIN permissions p
WHERE r.name = 'Admin';

-- Merkez yetkilisi arıza sürecini baştan sona yönetir; audit loglarını göremez.
INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM roles r
JOIN permissions p ON p.code IN (
    'vehicles.view.all', 'faults.create', 'faults.view.all', 'faults.update',
    'faults.change_status', 'faults.deactivate', 'reports.view', 'dashboard.view'
)
WHERE r.name = 'Merkez Yetkilisi';

-- Garaj yetkilisi yalnızca kendi garajının kayıtlarını izler.
INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM roles r
JOIN permissions p ON p.code IN (
    'vehicles.view.own_garage', 'faults.view.own_garage', 'reports.view', 'dashboard.view'
)
WHERE r.name = 'Garaj Yetkilisi';

-- Teknisyen yalnızca ekibine atanmış işi görür ve raporlar.
INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM roles r
JOIN permissions p ON p.code IN ('faults.view.assigned', 'reports.create')
WHERE r.name = 'Teknisyen';

INSERT INTO vehicle_statuses (code, name, display_order) VALUES
    ('IN_SERVICE', 'Kullanımda', 1),
    ('FAULTY', 'Arızalı', 2),
    ('WAITING_REPAIR', 'Tamir Bekliyor', 3),
    ('UNDER_REPAIR', 'Tamirde', 4),
    ('OUT_OF_SERVICE', 'Servis Dışı', 5);

INSERT INTO fault_statuses (code, name, is_closed_status, display_order) VALUES
    ('OPEN', 'Açık', false, 1),
    ('SENT_TO_GARAGE', 'Garaja İletildi', false, 2),
    ('ASSIGNED_TO_TEAM', 'Ekibe Atandı', false, 3),
    ('WAITING_REPAIR', 'Tamir Bekliyor', false, 4),
    ('REPAIR_IN_PROGRESS', 'Tamir Devam Ediyor', false, 5),
    ('REPORT_SUBMITTED', 'Rapor Gönderildi', false, 6),
    ('RESOLVED', 'Çözüldü', false, 7),
    ('UNRESOLVED', 'Çözülemedi', false, 8),
    ('CLOSED', 'Kapatıldı', true, 9),
    ('REOPENED', 'Yeniden Açıldı', false, 10),
    ('CANCELLED', 'İptal Edildi', true, 11);

INSERT INTO vehicle_types (name) VALUES
    ('Otobüs'), ('Metrobüs'), ('Hizmet Aracı'), ('Çekici');

INSERT INTO fuel_types (name) VALUES
    ('Dizel'), ('Elektrik'), ('Hibrit'), ('CNG');

COMMIT;
-- ANA ŞEMA: Arıza yönetim sisteminin schema, tablo, PK/FK, constraint, index ve temel referans verilerini kurar.
-- Bu dosya yeni bir PostgreSQL veritabanında sistemin veri modelini baştan oluşturmak için kullanılır.
