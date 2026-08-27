-- AMAÇ: SLA, tekrar eden arıza, araç sağlığı, çözüm bankası, kontrol ve operasyon olayı altyapısını kurar.
-- Transaction sayesinde adımlardan biri hata verirse migration'ın tamamı geri alınır ve yarım şema oluşmaz.
BEGIN;

SET search_path TO fault_management, public;

-- 1) SLA: kategori bazlı ilk müdahale ve çözüm hedefleri.
ALTER TABLE fault_categories
    ADD COLUMN IF NOT EXISTS response_sla_minutes integer NOT NULL DEFAULT 15,
    ADD COLUMN IF NOT EXISTS resolution_sla_minutes integer NOT NULL DEFAULT 240,
    ADD COLUMN IF NOT EXISTS recurrence_window_days integer NOT NULL DEFAULT 30,
    ADD COLUMN IF NOT EXISTS recurrence_alert_count integer NOT NULL DEFAULT 3;

ALTER TABLE fault_categories DROP CONSTRAINT IF EXISTS ck_fault_categories_sla;
ALTER TABLE fault_categories ADD CONSTRAINT ck_fault_categories_sla CHECK
    (response_sla_minutes > 0 AND resolution_sla_minutes >= response_sla_minutes
     AND recurrence_window_days > 0 AND recurrence_alert_count > 1);

ALTER TABLE faults
    ADD COLUMN IF NOT EXISTS response_due_at timestamptz,
    ADD COLUMN IF NOT EXISTS resolution_due_at timestamptz,
    ADD COLUMN IF NOT EXISTS first_response_at timestamptz;

UPDATE faults f
SET response_due_at = COALESCE(f.response_due_at,
        f.created_at + make_interval(mins => c.response_sla_minutes)),
    resolution_due_at = COALESCE(f.resolution_due_at,
        f.created_at + make_interval(mins => c.resolution_sla_minutes))
FROM fault_categories c
WHERE c.id = f.fault_category_id;

CREATE INDEX IF NOT EXISTS ix_faults_open_sla
    ON faults (resolution_due_at) WHERE is_active AND closed_at IS NULL;

-- 2) Kök neden kataloğu ve tamir raporuna bağlanması.
CREATE TABLE IF NOT EXISTS root_causes (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    code varchar(30) NOT NULL UNIQUE,
    name varchar(150) NOT NULL,
    description varchar(1000),
    is_active boolean NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL DEFAULT now()
);

INSERT INTO root_causes (code, name) VALUES
 ('WEAR', 'Normal aşınma'), ('MISUSE', 'Hatalı kullanım'),
 ('MATERIAL', 'Malzeme veya parça kaynaklı'), ('ELECTRICAL', 'Elektrik-elektronik kaynaklı'),
 ('MAINTENANCE', 'Bakım kaynaklı'), ('EXTERNAL', 'Dış etken'), ('UNKNOWN', 'Henüz belirlenemedi')
ON CONFLICT (code) DO NOTHING;

ALTER TABLE repair_reports
    ADD COLUMN IF NOT EXISTS root_cause_id bigint REFERENCES root_causes(id),
    ADD COLUMN IF NOT EXISTS solution_summary varchar(1000),
    ADD COLUMN IF NOT EXISTS recurrence_prevention varchar(1000),
    ADD COLUMN IF NOT EXISTS requires_follow_up boolean NOT NULL DEFAULT false;

CREATE INDEX IF NOT EXISTS ix_repair_reports_root_cause
    ON repair_reports(root_cause_id) WHERE root_cause_id IS NOT NULL;

-- 3) Onaylanmış çözüm bilgi bankası.
CREATE TABLE IF NOT EXISTS solution_articles (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    fault_category_id bigint NOT NULL REFERENCES fault_categories(id),
    root_cause_id bigint REFERENCES root_causes(id),
    source_repair_report_id bigint REFERENCES repair_reports(id),
    title varchar(200) NOT NULL,
    symptoms varchar(1500) NOT NULL,
    solution_steps text NOT NULL,
    safety_notes varchar(1500),
    estimated_minutes integer,
    approval_status varchar(20) NOT NULL DEFAULT 'DRAFT',
    created_by_user_id bigint NOT NULL REFERENCES app_users(id),
    approved_by_user_id bigint REFERENCES app_users(id),
    approved_at timestamptz,
    is_active boolean NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_solution_articles_status CHECK
      (approval_status IN ('DRAFT','APPROVED','REJECTED')),
    CONSTRAINT ck_solution_articles_minutes CHECK
      (estimated_minutes IS NULL OR estimated_minutes > 0)
);
CREATE INDEX IF NOT EXISTS ix_solution_articles_lookup
    ON solution_articles(fault_category_id, root_cause_id)
    WHERE is_active AND approval_status = 'APPROVED';

-- 4) Tamir sonrası kontrol / test sürüşü.
CREATE TABLE IF NOT EXISTS vehicle_inspections (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    vehicle_id bigint NOT NULL REFERENCES vehicles(id),
    fault_id bigint REFERENCES faults(id),
    inspection_type varchar(30) NOT NULL,
    result varchar(20) NOT NULL DEFAULT 'PENDING',
    odometer integer,
    notes varchar(2000),
    inspected_by_user_id bigint REFERENCES app_users(id),
    inspected_at timestamptz,
    next_action varchar(1000),
    created_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_vehicle_inspections_type CHECK
      (inspection_type IN ('POST_REPAIR','TEST_DRIVE','RETURN_TO_SERVICE')),
    CONSTRAINT ck_vehicle_inspections_result CHECK
      (result IN ('PENDING','PASSED','FAILED','CONDITIONAL'))
);
CREATE INDEX IF NOT EXISTS ix_vehicle_inspections_vehicle_date
    ON vehicle_inspections(vehicle_id, created_at DESC);

-- 5) Kurumsal operasyon olayları.
CREATE TABLE IF NOT EXISTS operational_events (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    event_number varchar(40) NOT NULL UNIQUE,
    event_type varchar(30) NOT NULL,
    title varchar(200) NOT NULL,
    description varchar(2000) NOT NULL,
    garage_id bigint REFERENCES garages(id),
    route_id bigint REFERENCES routes(id),
    starts_at timestamptz NOT NULL,
    ends_at timestamptz,
    status varchar(20) NOT NULL DEFAULT 'OPEN',
    created_by_user_id bigint NOT NULL REFERENCES app_users(id),
    created_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_operational_events_dates CHECK (ends_at IS NULL OR ends_at >= starts_at),
    CONSTRAINT ck_operational_events_status CHECK (status IN ('OPEN','ACTIVE','RESOLVED','CANCELLED'))
);
CREATE INDEX IF NOT EXISTS ix_operational_events_active
    ON operational_events(starts_at, ends_at) WHERE status IN ('OPEN','ACTIVE');

-- 6) Garajlar arası destek talepleri.
CREATE TABLE IF NOT EXISTS intergarage_support_requests (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    request_number varchar(40) NOT NULL UNIQUE,
    requesting_garage_id bigint NOT NULL REFERENCES garages(id),
    supporting_garage_id bigint REFERENCES garages(id),
    resource_type varchar(30) NOT NULL,
    requested_quantity integer NOT NULL DEFAULT 1,
    reason varchar(1500) NOT NULL,
    needed_from timestamptz NOT NULL,
    needed_until timestamptz,
    status varchar(20) NOT NULL DEFAULT 'PENDING',
    requested_by_user_id bigint NOT NULL REFERENCES app_users(id),
    decided_by_user_id bigint REFERENCES app_users(id),
    decided_at timestamptz,
    created_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_support_request_quantity CHECK (requested_quantity > 0),
    CONSTRAINT ck_support_request_dates CHECK (needed_until IS NULL OR needed_until > needed_from),
    CONSTRAINT ck_support_request_status CHECK
      (status IN ('PENDING','APPROVED','REJECTED','FULFILLED','CANCELLED'))
);

-- 7) Araç, ekip ve şoförün aynı zaman aralığında iki iş için ayrılmasını önleyen rezervasyon kaydı.
CREATE TABLE IF NOT EXISTS resource_reservations (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    resource_type varchar(30) NOT NULL,
    vehicle_id bigint REFERENCES vehicles(id),
    driver_id bigint REFERENCES drivers(id),
    team_id bigint REFERENCES technician_teams(id),
    fault_id bigint REFERENCES faults(id),
    service_task_id bigint REFERENCES service_tasks(id),
    support_request_id bigint REFERENCES intergarage_support_requests(id),
    starts_at timestamptz NOT NULL,
    ends_at timestamptz NOT NULL,
    status varchar(20) NOT NULL DEFAULT 'RESERVED',
    created_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_resource_reservations_dates CHECK (ends_at > starts_at),
    CONSTRAINT ck_resource_reservations_target CHECK
      (num_nonnulls(vehicle_id, driver_id, team_id) = 1),
    CONSTRAINT ck_resource_reservations_status CHECK
      (status IN ('RESERVED','ACTIVE','COMPLETED','CANCELLED'))
);
CREATE INDEX IF NOT EXISTS ix_resource_reservations_vehicle
    ON resource_reservations(vehicle_id, starts_at, ends_at) WHERE vehicle_id IS NOT NULL AND status IN ('RESERVED','ACTIVE');
CREATE INDEX IF NOT EXISTS ix_resource_reservations_driver
    ON resource_reservations(driver_id, starts_at, ends_at) WHERE driver_id IS NOT NULL AND status IN ('RESERVED','ACTIVE');
CREATE INDEX IF NOT EXISTS ix_resource_reservations_team
    ON resource_reservations(team_id, starts_at, ends_at) WHERE team_id IS NOT NULL AND status IN ('RESERVED','ACTIVE');

-- 8) Karar destek görünümleri.
CREATE OR REPLACE VIEW vw_fault_sla_status AS
SELECT f.id AS fault_id, f.fault_number, f.garage_id, f.vehicle_id,
       f.created_at, f.first_response_at, f.closed_at,
       f.response_due_at, f.resolution_due_at,
       CASE
         WHEN f.closed_at IS NOT NULL AND f.closed_at <= f.resolution_due_at THEN 'COMPLETED_ON_TIME'
         WHEN f.closed_at IS NOT NULL THEN 'COMPLETED_LATE'
         WHEN now() > f.resolution_due_at THEN 'BREACHED'
         WHEN now() > f.response_due_at AND f.first_response_at IS NULL THEN 'RESPONSE_BREACHED'
         ELSE 'ON_TRACK'
       END AS sla_status
FROM faults f WHERE f.is_active;

CREATE OR REPLACE VIEW vw_recurring_vehicle_faults AS
SELECT f.vehicle_id, f.fault_category_id, count(*)::bigint AS fault_count,
       min(f.occurred_at) AS first_fault_at, max(f.occurred_at) AS last_fault_at
FROM faults f
JOIN fault_categories c ON c.id = f.fault_category_id
WHERE f.is_active
  AND f.occurred_at >= now() - make_interval(days => c.recurrence_window_days)
GROUP BY f.vehicle_id, f.fault_category_id
HAVING count(*) >= max(c.recurrence_alert_count);

CREATE OR REPLACE VIEW vw_vehicle_health_scores AS
WITH fault_stats AS (
  SELECT vehicle_id,
         count(*) FILTER (WHERE occurred_at >= now() - interval '90 days') AS faults_90d,
         count(*) FILTER (WHERE occurred_at >= now() - interval '30 days') AS faults_30d
  FROM faults WHERE is_active GROUP BY vehicle_id
), latest_fault_checks AS (
  SELECT DISTINCT ON (vi.fault_id)
         vi.fault_id, vi.vehicle_id, vi.result, vi.created_at
  FROM vehicle_inspections vi
  WHERE vi.fault_id IS NOT NULL
    AND vi.created_at >= now() - interval '90 days'
  ORDER BY vi.fault_id, coalesce(vi.inspected_at, vi.created_at) DESC, vi.id DESC
), failed_checks AS (
  -- Önce başarısız olup daha sonra başarılı geçen kontroller kalıcı ceza oluşturmaz.
  -- Yalnızca açık arızanın en güncel kontrolü başarısızsa sağlık puanı etkilenir.
  SELECT lfc.vehicle_id, count(*) AS failed_count
  FROM latest_fault_checks lfc
  JOIN faults f ON f.id = lfc.fault_id
  WHERE lfc.result = 'FAILED' AND f.is_active AND f.closed_at IS NULL
  GROUP BY lfc.vehicle_id
)
SELECT v.id AS vehicle_id, v.door_number, v.garage_id, v.vehicle_status_id,
       greatest(0, 100 - coalesce(fs.faults_90d,0) * 5 - coalesce(fs.faults_30d,0) * 5
                    - coalesce(fc.failed_count,0) * 10)::integer AS health_score,
       coalesce(fs.faults_90d,0)::bigint AS faults_90d,
       coalesce(fs.faults_30d,0)::bigint AS faults_30d,
       coalesce(fc.failed_count,0)::bigint AS failed_inspections_90d
FROM vehicles v
LEFT JOIN fault_stats fs ON fs.vehicle_id = v.id
LEFT JOIN failed_checks fc ON fc.vehicle_id = v.id;

CREATE OR REPLACE VIEW vw_task_readiness AS
SELECT st.id AS service_task_id, st.task_number, st.planned_departure_at,
       ta.vehicle_id, ta.driver_id,
       (v.is_active AND vs.code = 'IN_USE') AS vehicle_ready,
       (d.is_active AND d.availability_status = 'AVAILABLE') AS driver_ready,
       CASE
         WHEN NOT v.is_active OR vs.code <> 'IN_USE' THEN 'VEHICLE_NOT_READY'
         WHEN NOT d.is_active OR d.availability_status <> 'AVAILABLE' THEN 'DRIVER_NOT_READY'
         ELSE 'READY'
       END AS readiness_status
FROM service_tasks st
JOIN task_assignments ta ON ta.service_task_id = st.id AND ta.is_active
JOIN vehicles v ON v.id = ta.vehicle_id
JOIN vehicle_statuses vs ON vs.id = v.vehicle_status_id
JOIN drivers d ON d.id = ta.driver_id;

COMMIT;
