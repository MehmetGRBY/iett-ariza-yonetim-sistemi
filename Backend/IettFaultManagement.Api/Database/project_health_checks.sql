-- AMAÇ: Uygulama verisinde iş kurallarına aykırı kayıt kalıp kalmadığını tek sonuç tablosunda denetler.
-- Her UNION ALL kolu ayrı bir kontrolün adını ve sorunlu kayıt sayısını döndürür; ideal sonuçların tamamı 0'dır.
SET search_path TO fault_management, public;

SELECT 'Uygunsuz araçlı gelecek görev' AS check_name, count(*) AS issue_count
FROM task_assignments ta JOIN service_tasks st ON st.id=ta.service_task_id
JOIN vehicles v ON v.id=ta.vehicle_id JOIN vehicle_statuses vs ON vs.id=v.vehicle_status_id
WHERE ta.is_active AND st.is_active AND st.planned_arrival_at>now()
  AND (NOT v.is_active OR vs.code IN ('FAULTY','WAITING_REPAIR','UNDER_REPAIR','OUT_OF_SERVICE'))
UNION ALL
SELECT 'Rapor bekleyen personele gelecek görev',count(*)
FROM task_assignments ta JOIN service_tasks st ON st.id=ta.service_task_id
JOIN personnel_incidents pi ON pi.driver_id=ta.driver_id
WHERE ta.is_active AND st.is_active AND st.planned_arrival_at>now()
  AND pi.is_active AND pi.status<>'CANCELLED' AND pi.report_status='PENDING'
UNION ALL
SELECT 'Rapor dönemiyle çakışan görev',count(*)
FROM task_assignments ta JOIN service_tasks st ON st.id=ta.service_task_id
JOIN personnel_incidents pi ON pi.driver_id=ta.driver_id
WHERE ta.is_active AND st.is_active AND pi.is_active AND pi.status<>'CANCELLED'
  AND pi.report_status='SUBMITTED' AND pi.absence_start_at<st.planned_arrival_at
  AND pi.expected_return_at>st.planned_departure_at AND st.planned_arrival_at>now()
UNION ALL
SELECT 'Görev başına birden fazla aktif atama',count(*) FROM (
  SELECT service_task_id FROM task_assignments WHERE is_active GROUP BY service_task_id HAVING count(*)>1
) problems
UNION ALL
SELECT 'Pasif garajdaki aktif araç',count(*)
FROM vehicles v JOIN garages g ON g.id=v.garage_id WHERE v.is_active AND NOT g.is_active;
