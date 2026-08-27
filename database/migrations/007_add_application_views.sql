BEGIN;

SET search_path TO fault_management, public;

-- ============================================================
-- 1) AÇIK ARIZALAR
-- Araç, garaj, kategori, durum ve açık kalma süresini birlikte verir.
-- ============================================================
CREATE OR REPLACE VIEW vw_active_faults AS
SELECT
    f.id AS fault_id,
    f.fault_number,
    f.vehicle_id,
    v.door_number,
    v.plate,
    v.brand,
    v.model,
    f.driver_id,
    d.personnel_number AS driver_personnel_number,
    concat_ws(' ', d.first_name, d.last_name) AS driver_full_name,
    f.garage_id,
    g.code AS garage_code,
    g.name AS garage_name,
    fc.id AS category_id,
    fc.name AS category_name,
    parent_fc.id AS parent_category_id,
    parent_fc.name AS parent_category_name,
    fs.id AS fault_status_id,
    fs.code AS fault_status_code,
    fs.name AS fault_status_name,
    f.description,
    f.mileage_at_failure,
    f.latitude,
    f.longitude,
    f.location_description,
    f.occurred_at,
    f.created_at,
    now() - f.occurred_at AS open_duration
FROM faults f
JOIN vehicles v ON v.id = f.vehicle_id
JOIN drivers d ON d.id = f.driver_id
JOIN garages g ON g.id = f.garage_id
JOIN fault_categories fc ON fc.id = f.fault_category_id
LEFT JOIN fault_categories parent_fc ON parent_fc.id = fc.parent_category_id
JOIN fault_statuses fs ON fs.id = f.fault_status_id
WHERE f.is_active
  AND NOT fs.is_closed_status;

-- ============================================================
-- 2) MÜSAİT ARAÇLAR
-- Aktif arızası ve aktif sefer ataması olmayan, göreve hazır araçlar.
-- ============================================================
CREATE OR REPLACE VIEW vw_available_vehicles AS
SELECT
    v.id AS vehicle_id,
    v.door_number,
    v.plate,
    v.brand,
    v.model,
    v.model_year,
    vt.id AS vehicle_type_id,
    vt.name AS vehicle_type_name,
    v.capacity,
    v.current_mileage,
    v.garage_id,
    g.code AS garage_code,
    g.name AS garage_name,
    vs.code AS vehicle_status_code,
    vs.name AS vehicle_status_name
FROM vehicles v
JOIN vehicle_types vt ON vt.id = v.vehicle_type_id
JOIN garages g ON g.id = v.garage_id
JOIN vehicle_statuses vs ON vs.id = v.vehicle_status_id
WHERE v.is_active
  AND g.is_active
  AND vs.code = 'AVAILABLE'
  AND NOT EXISTS (
      SELECT 1
      FROM faults f
      JOIN fault_statuses fs ON fs.id = f.fault_status_id
      WHERE f.vehicle_id = v.id
        AND f.is_active
        AND NOT fs.is_closed_status
  )
  AND NOT EXISTS (
      SELECT 1
      FROM task_assignments ta
      WHERE ta.vehicle_id = v.id
        AND ta.is_active
  );

-- ============================================================
-- 3) MÜSAİT TEKNİSYEN EKİPLERİ
-- Otomatik ekip atamasında aynı garajdaki boş ekipleri gösterir.
-- ============================================================
CREATE OR REPLACE VIEW vw_available_technician_teams AS
SELECT
    tt.id AS team_id,
    tt.name AS team_name,
    tt.garage_id,
    g.code AS garage_code,
    g.name AS garage_name,
    COUNT(tm.id) FILTER (WHERE tm.is_active) AS active_member_count,
    COUNT(tm.id) FILTER (WHERE tm.is_active AND tm.is_team_leader) AS active_leader_count,
    tt.last_assigned_at
FROM technician_teams tt
JOIN garages g ON g.id = tt.garage_id
LEFT JOIN team_members tm ON tm.team_id = tt.id
WHERE tt.is_active
  AND tt.is_available
  AND g.is_active
  AND NOT EXISTS (
      SELECT 1
      FROM fault_assignments fa
      WHERE fa.team_id = tt.id
        AND fa.is_active
  )
GROUP BY tt.id, tt.name, tt.garage_id, g.code, g.name, tt.last_assigned_at;

-- ============================================================
-- 4) GARAJLARA GÖRE ARAÇ TİPİ DAĞILIMI
-- Otobüs, metrobüs, hizmet aracı ve çekici sayılarını verir.
-- ============================================================
CREATE OR REPLACE VIEW vw_garage_vehicle_type_summary AS
SELECT
    g.id AS garage_id,
    g.code AS garage_code,
    g.name AS garage_name,
    g.vehicle_capacity,
    vt.id AS vehicle_type_id,
    vt.name AS vehicle_type_name,
    COUNT(v.id) FILTER (WHERE v.is_active) AS active_vehicle_count,
    COUNT(v.id) FILTER (WHERE NOT v.is_active) AS passive_vehicle_count,
    COUNT(v.id) AS total_vehicle_count
FROM garages g
CROSS JOIN vehicle_types vt
LEFT JOIN vehicles v
       ON v.garage_id = g.id
      AND v.vehicle_type_id = vt.id
GROUP BY
    g.id, g.code, g.name, g.vehicle_capacity,
    vt.id, vt.name;

-- ============================================================
-- 5) GÜNLÜK ARIZA ÖZETİ
-- Dashboard grafiklerinde kullanılabilir.
-- ============================================================
CREATE OR REPLACE VIEW vw_daily_fault_summary AS
SELECT
    f.occurred_at::date AS fault_date,
    COUNT(*) FILTER (WHERE f.is_active) AS opened_fault_count,
    COUNT(*) FILTER (WHERE f.is_active AND f.closed_at IS NOT NULL) AS closed_fault_count,
    COUNT(*) FILTER (WHERE f.is_active AND f.closed_at IS NULL) AS still_open_fault_count,
    COUNT(DISTINCT f.vehicle_id) FILTER (WHERE f.is_active) AS affected_vehicle_count,
    round(
        AVG(EXTRACT(EPOCH FROM (f.closed_at - f.occurred_at)) / 3600.0)
            FILTER (WHERE f.is_active AND f.closed_at IS NOT NULL),
        2
    ) AS average_resolution_hours
FROM faults f
GROUP BY f.occurred_at::date;

-- ============================================================
-- 6) ARIZA KATEGORİSİ ÖZETİ
-- Ana kategori ve alt kategori bazında sayıları verir.
-- ============================================================
CREATE OR REPLACE VIEW vw_fault_category_summary AS
SELECT
    COALESCE(parent_fc.id, fc.id) AS parent_category_id,
    COALESCE(parent_fc.name, fc.name) AS parent_category_name,
    CASE WHEN fc.parent_category_id IS NOT NULL THEN fc.id END AS subcategory_id,
    CASE WHEN fc.parent_category_id IS NOT NULL THEN fc.name END AS subcategory_name,
    COUNT(f.id) FILTER (WHERE f.is_active) AS total_fault_count,
    COUNT(f.id) FILTER (
        WHERE f.is_active AND NOT fs.is_closed_status
    ) AS open_fault_count,
    COUNT(f.id) FILTER (
        WHERE f.is_active AND fs.is_closed_status
    ) AS closed_fault_count,
    MAX(f.occurred_at) FILTER (WHERE f.is_active) AS last_fault_at
FROM fault_categories fc
LEFT JOIN fault_categories parent_fc ON parent_fc.id = fc.parent_category_id
LEFT JOIN faults f ON f.fault_category_id = fc.id
LEFT JOIN fault_statuses fs ON fs.id = f.fault_status_id
GROUP BY
    COALESCE(parent_fc.id, fc.id),
    COALESCE(parent_fc.name, fc.name),
    CASE WHEN fc.parent_category_id IS NOT NULL THEN fc.id END,
    CASE WHEN fc.parent_category_id IS NOT NULL THEN fc.name END;

-- ============================================================
-- 7) ŞOFÖR ARIZA BİLDİRİM ÖZETİ
-- ============================================================
CREATE OR REPLACE VIEW vw_driver_fault_summary AS
SELECT
    d.id AS driver_id,
    d.personnel_number,
    d.first_name,
    d.last_name,
    d.is_active AS driver_is_active,
    COUNT(f.id) FILTER (WHERE f.is_active) AS total_fault_count,
    COUNT(f.id) FILTER (
        WHERE f.is_active AND NOT fs.is_closed_status
    ) AS open_fault_count,
    COUNT(f.id) FILTER (
        WHERE f.is_active AND fs.is_closed_status
    ) AS closed_fault_count,
    MAX(f.occurred_at) FILTER (WHERE f.is_active) AS last_fault_at
FROM drivers d
LEFT JOIN faults f ON f.driver_id = d.id
LEFT JOIN fault_statuses fs ON fs.id = f.fault_status_id
GROUP BY d.id, d.personnel_number, d.first_name, d.last_name, d.is_active;

-- ============================================================
-- 8) ARAÇLARIN AKTİF/GÜNCEL SEFER GÖREVLERİ
-- ============================================================
CREATE OR REPLACE VIEW vw_vehicle_current_task AS
SELECT
    st.id AS service_task_id,
    st.task_number,
    st.service_date,
    st.sequence_number,
    st.status AS task_status,
    st.planned_departure_at,
    st.planned_arrival_at,
    st.actual_departure_at,
    st.actual_arrival_at,
    r.id AS route_id,
    r.code AS route_code,
    r.name AS route_name,
    r.start_point,
    r.end_point,
    ta.id AS task_assignment_id,
    ta.vehicle_id,
    v.door_number,
    v.plate,
    v.garage_id,
    g.name AS garage_name,
    ta.driver_id,
    d.personnel_number AS driver_personnel_number,
    concat_ws(' ', d.first_name, d.last_name) AS driver_full_name,
    ta.assignment_type,
    ta.assigned_at
FROM service_tasks st
JOIN routes r ON r.id = st.route_id
JOIN task_assignments ta
  ON ta.service_task_id = st.id
 AND ta.is_active
JOIN vehicles v ON v.id = ta.vehicle_id
JOIN garages g ON g.id = v.garage_id
JOIN drivers d ON d.id = ta.driver_id
WHERE st.is_active
  AND st.status IN ('ASSIGNED', 'IN_PROGRESS', 'TRANSFER_PENDING');

-- ============================================================
-- 9) YEDEK ARACA AKTARILMAYI BEKLEYEN GÖREVLER
-- ============================================================
CREATE OR REPLACE VIEW vw_tasks_waiting_for_transfer AS
SELECT
    st.id AS service_task_id,
    st.task_number,
    st.service_date,
    st.sequence_number,
    st.planned_departure_at,
    st.planned_arrival_at,
    r.id AS route_id,
    r.code AS route_code,
    r.name AS route_name,
    ta.vehicle_id AS current_vehicle_id,
    v.door_number AS current_vehicle_door_number,
    v.garage_id,
    g.name AS garage_name,
    ta.driver_id,
    d.personnel_number AS driver_personnel_number,
    concat_ws(' ', d.first_name, d.last_name) AS driver_full_name,
    f.id AS fault_id,
    f.fault_number
FROM service_tasks st
JOIN routes r ON r.id = st.route_id
JOIN task_assignments ta
  ON ta.service_task_id = st.id
 AND ta.is_active
JOIN vehicles v ON v.id = ta.vehicle_id
JOIN garages g ON g.id = v.garage_id
JOIN drivers d ON d.id = ta.driver_id
LEFT JOIN faults f
       ON f.service_task_id = st.id
      AND f.is_active
      AND f.closed_at IS NULL
WHERE st.is_active
  AND st.status = 'TRANSFER_PENDING';

-- ============================================================
-- 10) ARIZA VE TAMİR RAPORU AYRINTILARI
-- Her raporu tek satırda, işlem/parça özetleriyle gösterir.
-- ============================================================
CREATE OR REPLACE VIEW vw_fault_repair_details AS
SELECT
    f.id AS fault_id,
    f.fault_number,
    f.vehicle_id,
    v.door_number,
    v.plate,
    f.garage_id,
    g.name AS garage_name,
    fs.code AS fault_status_code,
    fs.name AS fault_status_name,
    fa.id AS fault_assignment_id,
    tt.id AS team_id,
    tt.name AS team_name,
    fa.assigned_at,
    fa.started_at AS assignment_started_at,
    fa.completed_at AS assignment_completed_at,
    rr.id AS repair_report_id,
    rr.result AS repair_result,
    rr.description AS repair_description,
    rr.started_at AS repair_started_at,
    rr.completed_at AS repair_completed_at,
    rr.submitted_at,
    rr.is_submitted,
    COALESCE(actions.action_count, 0) AS action_count,
    COALESCE(actions.action_descriptions, '') AS action_descriptions,
    COALESCE(parts.part_line_count, 0) AS part_line_count,
    COALESCE(parts.part_descriptions, '') AS part_descriptions,
    COALESCE(attachments.attachment_count, 0) AS attachment_count
FROM faults f
JOIN vehicles v ON v.id = f.vehicle_id
JOIN garages g ON g.id = f.garage_id
JOIN fault_statuses fs ON fs.id = f.fault_status_id
LEFT JOIN fault_assignments fa ON fa.fault_id = f.id
LEFT JOIN technician_teams tt ON tt.id = fa.team_id
LEFT JOIN repair_reports rr
       ON rr.fault_assignment_id = fa.id
      AND rr.is_active
LEFT JOIN LATERAL (
    SELECT
        COUNT(*) AS action_count,
        string_agg(rra.description, ' | ' ORDER BY rra.performed_at) AS action_descriptions
    FROM repair_report_actions rra
    WHERE rra.repair_report_id = rr.id
) actions ON true
LEFT JOIN LATERAL (
    SELECT
        COUNT(*) AS part_line_count,
        string_agg(
            rrp.part_name || ' x ' || rrp.quantity::text,
            ' | ' ORDER BY rrp.id
        ) AS part_descriptions
    FROM repair_report_parts rrp
    WHERE rrp.repair_report_id = rr.id
) parts ON true
LEFT JOIN LATERAL (
    SELECT COUNT(*) AS attachment_count
    FROM repair_report_attachments rrat
    WHERE rrat.repair_report_id = rr.id
      AND rrat.is_active
) attachments ON true
WHERE f.is_active;

-- ============================================================
-- 11) KULLANICI BAZINDA OKUNMAMIŞ BİLDİRİM SAYISI
-- Navbar üzerindeki bildirim rozeti için kullanılabilir.
-- ============================================================
CREATE OR REPLACE VIEW vw_unread_notification_counts AS
SELECT
    u.id AS user_id,
    u.personnel_number,
    u.first_name,
    u.last_name,
    COUNT(n.id) FILTER (WHERE NOT n.is_read) AS unread_notification_count,
    MAX(n.created_at) FILTER (WHERE NOT n.is_read) AS latest_unread_notification_at
FROM app_users u
LEFT JOIN notifications n ON n.user_id = u.id
WHERE u.is_active
GROUP BY u.id, u.personnel_number, u.first_name, u.last_name;

COMMIT;

-- ============================================================
-- 12) KURULUM KONTROLÜ
-- Bu sorgu 11 satır döndürmelidir.
-- ============================================================
SELECT table_name AS view_name
FROM information_schema.views
WHERE table_schema = 'fault_management'
  AND table_name IN (
      'vw_active_faults',
      'vw_available_vehicles',
      'vw_available_technician_teams',
      'vw_garage_vehicle_type_summary',
      'vw_daily_fault_summary',
      'vw_fault_category_summary',
      'vw_driver_fault_summary',
      'vw_vehicle_current_task',
      'vw_tasks_waiting_for_transfer',
      'vw_fault_repair_details',
      'vw_unread_notification_counts'
  )
ORDER BY table_name;
-- Dashboard ve liste ekranlarındaki karmaşık sorguları sadeleştiren raporlama view'larını oluşturur.
