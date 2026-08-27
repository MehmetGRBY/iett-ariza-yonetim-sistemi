BEGIN;
SET search_path TO fault_management, public;

CREATE TEMP TABLE missing_night_duties
(
    duty_number varchar(50) PRIMARY KEY,
    service_date date NOT NULL,
    garage_id bigint NOT NULL,
    route_id bigint NOT NULL,
    vehicle_id bigint NOT NULL,
    driver_id bigint NOT NULL
) ON COMMIT DROP;

WITH night_routes(route_code, garage_code) AS
(
    VALUES ('MB-01', 'BYM'), ('MB-02', 'CBM'), ('MB-05', 'SLM'), ('MB-06', 'ZKM')
),
vehicle_pool AS
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
INSERT INTO missing_night_duties
SELECT 'PLAN-' || to_char(day_value::date, 'YYYYMMDD') || '-' || nr.route_code || '-S0',
       day_value::date, g.id, r.id, vp.id, dp.id
FROM night_routes nr
JOIN routes r ON r.code = nr.route_code AND r.is_active
JOIN garages g ON g.code = nr.garage_code AND g.is_active
JOIN vehicle_pool vp ON vp.garage_id = g.id AND vp.rn = 1
JOIN driver_pool dp ON dp.garage_id = g.id AND dp.rn = 1
CROSS JOIN generate_series(DATE '2026-07-24', DATE '2026-07-30', INTERVAL '1 day') day_value
WHERE NOT EXISTS
(
    SELECT 1 FROM service_duties sd
    WHERE sd.duty_number = 'PLAN-' || to_char(day_value::date, 'YYYYMMDD') || '-' || nr.route_code || '-S0'
      AND sd.is_active
);

INSERT INTO service_duties
    (duty_number, service_date, garage_id, route_id, original_vehicle_id,
     original_driver_id, status, description, created_by_user_id, is_active)
SELECT duty_number, service_date, garage_id, route_id, vehicle_id, driver_id,
       'PLANNED', '00.00-06.00 metrobüs gece vardiyası; altı gidiş-dönüş görevi.',
       2, true
FROM missing_night_duties;

INSERT INTO service_tasks
    (task_number, route_id, service_date, sequence_number,
     planned_departure_at, planned_arrival_at, status, is_active,
     created_by_user_id, service_duty_id)
SELECT m.duty_number || '-G' || lpad(task_seq::text, 2, '0'),
       m.route_id, m.service_date, task_seq,
       m.service_date::timestamp + make_interval(hours => task_seq - 1),
       m.service_date::timestamp + make_interval(hours => task_seq),
       'PLANNED', true, 2, sd.id
FROM missing_night_duties m
JOIN service_duties sd ON sd.duty_number = m.duty_number AND sd.is_active
CROSS JOIN generate_series(1, 6) task_seq;

INSERT INTO task_assignments
    (service_task_id, vehicle_id, driver_id, assignment_type,
     assigned_by_user_id, assigned_at, is_active, description)
SELECT st.id, m.vehicle_id, m.driver_id, 'ORIGINAL', 2, now(), true,
       'Metrobüs gece vardiyasıyla otomatik atandı.'
FROM missing_night_duties m
JOIN service_duties sd ON sd.duty_number = m.duty_number AND sd.is_active
JOIN service_tasks st ON st.service_duty_id = sd.id AND st.is_active;

UPDATE drivers d SET availability_status = 'ON_DUTY'
WHERE EXISTS
(
    SELECT 1 FROM missing_night_duties m WHERE m.driver_id = d.id
);

UPDATE vehicles v SET vehicle_status_id = 7
WHERE EXISTS
(
    SELECT 1 FROM missing_night_duties m WHERE m.vehicle_id = v.id
);

COMMIT;
-- Metrobüs hatlarının 00:00-06:00 gece vardiyalarında eksik kalan görevlerini tamamlar.
