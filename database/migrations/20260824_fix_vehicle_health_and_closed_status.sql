-- Tamamlanmış arızaların araç durumunu ve sağlık puanı hesaplamasını düzeltir.
BEGIN;

SET LOCAL search_path TO fault_management, public;

CREATE OR REPLACE VIEW vw_vehicle_health_scores AS
WITH fault_stats AS (
  SELECT vehicle_id,
         count(*) FILTER (WHERE occurred_at >= now() - interval '90 days') AS faults_90d,
         count(*) FILTER (WHERE occurred_at >= now() - interval '30 days') AS faults_30d
  FROM faults
  WHERE is_active
  GROUP BY vehicle_id
), latest_fault_checks AS (
  SELECT DISTINCT ON (vi.fault_id)
         vi.fault_id, vi.vehicle_id, vi.result, vi.created_at
  FROM vehicle_inspections vi
  WHERE vi.fault_id IS NOT NULL
    AND vi.created_at >= now() - interval '90 days'
  ORDER BY vi.fault_id, coalesce(vi.inspected_at, vi.created_at) DESC, vi.id DESC
), failed_checks AS (
  SELECT lfc.vehicle_id, count(*) AS failed_count
  FROM latest_fault_checks lfc
  JOIN faults f ON f.id = lfc.fault_id
  WHERE lfc.result = 'FAILED'
    AND f.is_active
    AND f.closed_at IS NULL
  GROUP BY lfc.vehicle_id
)
SELECT v.id AS vehicle_id,
       v.door_number,
       v.garage_id,
       v.vehicle_status_id,
       greatest(0,
         100
         - coalesce(fs.faults_90d, 0) * 5
         - coalesce(fs.faults_30d, 0) * 5
         - coalesce(fc.failed_count, 0) * 10)::integer AS health_score,
       coalesce(fs.faults_90d, 0)::bigint AS faults_90d,
       coalesce(fs.faults_30d, 0)::bigint AS faults_30d,
       coalesce(fc.failed_count, 0)::bigint AS failed_inspections_90d
FROM vehicles v
LEFT JOIN fault_stats fs ON fs.vehicle_id = v.id
LEFT JOIN failed_checks fc ON fc.vehicle_id = v.id;

-- Açık arızası kalmadığı ve en son teknik sonucu başarılı olduğu hâlde eski sürüm
-- nedeniyle Tamirde kalan araçları göreve hazır duruma getirir.
WITH available_status AS (
  SELECT id FROM vehicle_statuses WHERE code = 'AVAILABLE'
), repaired_vehicles AS (
  SELECT DISTINCT f.vehicle_id
  FROM faults f
  WHERE f.closed_at IS NOT NULL
    AND NOT EXISTS (
      SELECT 1 FROM faults open_fault
      WHERE open_fault.vehicle_id = f.vehicle_id
        AND open_fault.is_active
        AND open_fault.closed_at IS NULL)
    AND EXISTS (
      SELECT 1 FROM vehicle_inspections vi
      WHERE vi.fault_id = f.id
        AND vi.result IN ('PASSED', 'CONDITIONAL'))
    AND EXISTS (
      SELECT 1
      FROM repair_reports rr
      JOIN fault_assignments fa ON fa.id = rr.fault_assignment_id
      WHERE fa.fault_id = f.id
        AND rr.is_active
        AND rr.is_submitted
        AND rr.result IN ('REPAIRED', 'TEMPORARY_REPAIR', 'RESOLVED'))
)
UPDATE vehicles v
SET vehicle_status_id = available.id
FROM repaired_vehicles repaired
CROSS JOIN available_status available
JOIN vehicle_statuses current_status ON true
WHERE v.id = repaired.vehicle_id
  AND current_status.id = v.vehicle_status_id
  AND current_status.code = 'UNDER_REPAIR';

COMMIT;
