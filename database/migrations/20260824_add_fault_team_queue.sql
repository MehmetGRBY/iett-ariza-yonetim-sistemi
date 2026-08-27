-- Araç başına tek açık arıza ve teknik ekip bekleme kuyruğunu veritabanı düzeyinde güvenceye alır.
BEGIN;

SET LOCAL search_path TO fault_management, public;

-- Bütün ekipler dolu olduğunda arızanın kullanıcıya açık biçimde görünen durumudur.
INSERT INTO fault_statuses (code, name, is_closed_status, display_order, is_active)
VALUES ('WAITING_TEAM', 'Ekip Bekliyor', false, 35, true)
ON CONFLICT (code) DO UPDATE
SET name = EXCLUDED.name,
    is_closed_status = EXCLUDED.is_closed_status,
    display_order = EXCLUDED.display_order,
    is_active = true;

-- Eski uygulama kuralı yalnızca aynı açıklamayı engellediğinden oluşmuş tekrarları tespit eder.
-- Her araç için en eski açık kayıt korunur, sonraki kayıtlar denetlenebilir biçimde iptal edilir.
CREATE TEMP TABLE duplicate_open_faults ON COMMIT DROP AS
SELECT id, fault_status_id
FROM (
    SELECT f.id,
           f.fault_status_id,
           row_number() OVER (
               PARTITION BY f.vehicle_id
               ORDER BY f.occurred_at, f.created_at, f.id) AS open_order
      FROM faults f
     WHERE f.is_active AND f.closed_at IS NULL
) ranked
WHERE open_order > 1;

-- Tekrarlanan kayda bağlı aktif ekip/kaynak atamaları kapatılır.
UPDATE fault_assignments a
   SET is_active = false,
       completed_at = COALESCE(a.completed_at, clock_timestamp())
  FROM duplicate_open_faults d
 WHERE a.fault_id = d.id AND a.is_active;

UPDATE fault_resource_assignments r
   SET is_active = false,
       completed_at = COALESCE(r.completed_at, clock_timestamp()),
       status = 'CANCELLED'
  FROM duplicate_open_faults d
 WHERE r.fault_id = d.id AND r.is_active;

WITH admin_user AS (
    SELECT u.id, u.role_id
      FROM app_users u
      JOIN roles r ON r.id = u.role_id
     WHERE u.is_active AND r.name = 'Admin'
     ORDER BY u.id
     LIMIT 1
), cancelled_status AS (
    SELECT id FROM fault_statuses WHERE code = 'CANCELLED'
)
INSERT INTO fault_status_histories
    (fault_id, old_status_id, new_status_id, changed_by_user_id, changed_by_role_id,
     description, is_system_action, changed_at)
SELECT d.id, d.fault_status_id, s.id, a.id, a.role_id,
       'Aynı araç için birden fazla açık kayıt bulunduğundan sonraki kayıt sistem tarafından iptal edildi.',
       true, clock_timestamp()
  FROM duplicate_open_faults d
 CROSS JOIN admin_user a
 CROSS JOIN cancelled_status s;

WITH admin_user AS (
    SELECT u.id, u.role_id
      FROM app_users u
      JOIN roles r ON r.id = u.role_id
     WHERE u.is_active AND r.name = 'Admin'
     ORDER BY u.id
     LIMIT 1
), cancelled_status AS (
    SELECT id FROM fault_statuses WHERE code = 'CANCELLED'
)
UPDATE faults f
   SET fault_status_id = s.id,
       closed_at = clock_timestamp(),
       is_active = false,
       deactivated_at = clock_timestamp(),
       deactivated_by_user_id = a.id,
       deactivation_reason = 'Aynı araç için mükerrer açık arıza kaydı.'
  FROM duplicate_open_faults d
 CROSS JOIN admin_user a
 CROSS JOIN cancelled_status s
 WHERE f.id = d.id;

-- Ekip müsaitliği aktif atamalardan yeniden hesaplanır; eski tutarsız bayraklar temizlenir.
UPDATE technician_teams t
   SET is_available = t.is_active AND NOT EXISTS (
       SELECT 1
         FROM fault_assignments a
         JOIN faults f ON f.id = a.fault_id
        WHERE a.team_id = t.id
          AND a.is_active
          AND f.is_active
          AND f.closed_at IS NULL);

-- Uygulamadaki kontrole ek olarak eşzamanlı istekleri PostgreSQL de engeller.
CREATE UNIQUE INDEX IF NOT EXISTS uq_faults_one_open_per_vehicle
    ON faults (vehicle_id)
    WHERE is_active AND closed_at IS NULL;

CREATE UNIQUE INDEX IF NOT EXISTS uq_fault_assignments_one_active_team
    ON fault_assignments (team_id)
    WHERE is_active;

COMMIT;

-- Bu iki sorgunun da sıfır satır döndürmesi beklenir.
SELECT vehicle_id, COUNT(*) AS open_fault_count
  FROM fault_management.faults
 WHERE is_active AND closed_at IS NULL
 GROUP BY vehicle_id
HAVING COUNT(*) > 1;

SELECT a.team_id, COUNT(*) AS active_assignment_count
  FROM fault_management.fault_assignments a
 WHERE a.is_active
 GROUP BY a.team_id
HAVING COUNT(*) > 1;
