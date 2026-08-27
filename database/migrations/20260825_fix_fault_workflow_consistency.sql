BEGIN;

SET LOCAL search_path TO fault_management, public;

-- Backend bütün ekipler doluyken planı WAITING_TEAM durumuna geçirir. Bu değer
-- kısıtta bulunmazsa kaynaklar ulaştıktan sonraki işlem tamamen geri alınır.
ALTER TABLE fault_response_plans
    DROP CONSTRAINT IF EXISTS ck_fault_response_plans_automation_status;

ALTER TABLE fault_response_plans
    ADD CONSTRAINT ck_fault_response_plans_automation_status CHECK
    (automation_status IN
        ('PENDING', 'RESOURCE_DEPARTING', 'RESOURCE_EN_ROUTE', 'RESOURCE_ARRIVED',
         'VEHICLE_DELIVERED', 'TEAM_ASSIGNED', 'WAITING_TEAM', 'DISPATCHED',
         'REPAIRING', 'WAITING_INSPECTION', 'READY_TO_CLOSE', 'MANUAL',
         'MANUAL_REPAIR_REQUIRED', 'COMPLETED', 'FAILED'));

-- Önceki kısıt yüzünden kaynak ulaştı aşamasında kalan planlar yeniden denenir.
UPDATE fault_response_plans
   SET next_automation_at = clock_timestamp(),
       last_automation_error = NULL
 WHERE is_active
   AND automation_enabled
   AND automation_status IN ('RESOURCE_ARRIVED', 'VEHICLE_DELIVERED', 'WAITING_TEAM')
   AND last_automation_error IS NOT NULL;

-- Açık arızası bulunan ve görevlerine devam etmeyeceği belirlenen ana araçlar
-- yanlışlıkla Göreve Hazır görünmemelidir.
WITH faulty_status AS (
    SELECT id FROM vehicle_statuses WHERE code = 'FAULTY'
), affected AS (
    SELECT DISTINCT v.id AS vehicle_id, v.vehicle_status_id AS old_status_id,
           f.id AS fault_id, s.id AS new_status_id
      FROM faults f
      JOIN fault_response_plans p ON p.fault_id = f.id AND p.is_active
      JOIN vehicles v ON v.id = f.vehicle_id
      CROSS JOIN faulty_status s
     WHERE f.is_active
       AND f.closed_at IS NULL
       AND NOT p.can_continue_remaining_tasks
       AND v.vehicle_status_id IN (
           SELECT id FROM vehicle_statuses WHERE code IN ('AVAILABLE', 'IN_SERVICE', 'ON_DUTY'))
)
INSERT INTO vehicle_status_histories
    (vehicle_id, old_status_id, new_status_id, changed_by_user_id,
     changed_at, description, fault_id)
SELECT a.vehicle_id, a.old_status_id, a.new_status_id, u.id,
       clock_timestamp(), 'Açık arıza iş akışı tutarlılık kontrolüyle araç arızalı duruma alındı.', a.fault_id
  FROM affected a
 CROSS JOIN LATERAL (
     SELECT id FROM app_users WHERE is_active ORDER BY personnel_number = 'ADM-0001' DESC, id LIMIT 1
 ) u;

UPDATE vehicles v
   SET vehicle_status_id = s.id
  FROM vehicle_statuses s
 WHERE s.code = 'FAULTY'
   AND EXISTS (
       SELECT 1
         FROM faults f
         JOIN fault_response_plans p ON p.fault_id = f.id AND p.is_active
        WHERE f.vehicle_id = v.id
          AND f.is_active
          AND f.closed_at IS NULL
          AND NOT p.can_continue_remaining_tasks)
   AND v.vehicle_status_id IN (
       SELECT id FROM vehicle_statuses WHERE code IN ('AVAILABLE', 'IN_SERVICE', 'ON_DUTY'));

COMMIT;

