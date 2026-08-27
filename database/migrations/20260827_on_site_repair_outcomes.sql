BEGIN;

SET LOCAL search_path TO fault_management, public;

INSERT INTO fault_statuses (code, name, is_closed_status, display_order, is_active)
VALUES ('TOW_SELECTION_REQUIRED', 'Çekici Seçimi Bekleniyor', false, 34, true)
ON CONFLICT (code) DO UPDATE SET name=EXCLUDED.name,is_closed_status=false,display_order=EXCLUDED.display_order,is_active=true;

ALTER TABLE fault_response_plans DROP CONSTRAINT IF EXISTS ck_fault_response_plans_automation_status;
ALTER TABLE fault_response_plans ADD CONSTRAINT ck_fault_response_plans_automation_status CHECK
(automation_status IN
 ('PENDING','WAITING_CURRENT_TASK_END','WAITING_TODAYS_TASKS_END','VEHICLE_RETURNING_TO_GARAGE',
  'ON_SITE_REPAIRED_RETURNING','AWAITING_TOW_SELECTION','RESOURCE_DEPARTING','RESOURCE_EN_ROUTE',
  'RESOURCE_ARRIVED','VEHICLE_DELIVERED','TEAM_ASSIGNED','WAITING_TEAM','DISPATCHED','REPAIRING',
  'WAITING_INSPECTION','READY_TO_CLOSE','MANUAL','MANUAL_REPAIR_REQUIRED','COMPLETED','FAILED'));

COMMIT;
