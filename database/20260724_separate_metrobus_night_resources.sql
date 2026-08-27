BEGIN;
SET search_path TO fault_management, public;

CREATE TEMP TABLE night_resource_plan(route_code varchar(30), garage_code varchar(30), resource_rn integer)
ON COMMIT DROP;

INSERT INTO night_resource_plan VALUES
    ('MB-01', 'BYM', 8),
    ('MB-02', 'CBM', 8),
    ('MB-05', 'SLM', 4),
    ('MB-06', 'ZKM', 8);

CREATE TEMP TABLE selected_night_resources AS
WITH vehicle_pool AS
(
    SELECT v.garage_id, v.id,
           row_number() OVER (PARTITION BY v.garage_id ORDER BY v.door_number, v.id) rn
    FROM vehicles v
    JOIN vehicle_types vt ON vt.id = v.vehicle_type_id
    WHERE v.is_active AND v.vehicle_status_id IN (1, 6, 7) AND vt.name = 'Metrobüs'
),
driver_pool AS
(
    SELECT d.garage_id, d.id,
           row_number() OVER (PARTITION BY d.garage_id ORDER BY d.personnel_number, d.id) rn
    FROM drivers d
    WHERE d.is_active AND d.driver_type = 'NORMAL'
      AND d.availability_status IN ('AVAILABLE', 'ON_DUTY')
)
SELECT p.route_code, g.id garage_id, vp.id vehicle_id, dp.id driver_id
FROM night_resource_plan p
JOIN garages g ON g.code = p.garage_code AND g.is_active
JOIN vehicle_pool vp ON vp.garage_id = g.id AND vp.rn = p.resource_rn
JOIN driver_pool dp ON dp.garage_id = g.id AND dp.rn = p.resource_rn;

UPDATE service_duties sd
SET original_vehicle_id = r.vehicle_id,
    original_driver_id = r.driver_id,
    description = '00.00-06.00 metrobüs gece vardiyası; ayrı araç ve şoför.'
FROM routes route
JOIN selected_night_resources r ON r.route_code = route.code
WHERE sd.route_id = route.id
  AND sd.is_active
  AND sd.duty_number LIKE '%-S0';

UPDATE task_assignments ta
SET vehicle_id = r.vehicle_id,
    driver_id = r.driver_id,
    description = 'Metrobüs gece vardiyasına ayrı kaynak atandı.'
FROM service_tasks st
JOIN service_duties sd ON sd.id = st.service_duty_id
JOIN routes route ON route.id = sd.route_id
JOIN selected_night_resources r ON r.route_code = route.code
WHERE ta.service_task_id = st.id
  AND ta.is_active
  AND st.is_active
  AND sd.is_active
  AND sd.duty_number LIKE '%-S0';

UPDATE drivers d SET availability_status = 'ON_DUTY'
WHERE EXISTS (SELECT 1 FROM selected_night_resources r WHERE r.driver_id = d.id);

UPDATE vehicles v SET vehicle_status_id = 7
WHERE EXISTS (SELECT 1 FROM selected_night_resources r WHERE r.vehicle_id = v.id);

COMMIT;
-- Metrobüs gece görevlerinde kaynak çakışması olmaması için araç ve sürücü atamalarını ayrıştırır.
