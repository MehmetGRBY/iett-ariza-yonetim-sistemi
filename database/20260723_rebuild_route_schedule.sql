BEGIN;
SET search_path TO fault_management, public;

CREATE TEMP TABLE schedule_route_plan
(
    route_code varchar(30) PRIMARY KEY,
    route_name varchar(200) NOT NULL,
    start_point varchar(200) NOT NULL,
    end_point varchar(200) NOT NULL,
    garage_code varchar(30) NOT NULL,
    is_metrobus boolean NOT NULL,
    route_order integer NOT NULL
) ON COMMIT DROP;

-- Arnavutköy yedek garajına planlı hat verilmez.
INSERT INTO schedule_route_plan
    (route_code, route_name, start_point, end_point, garage_code, is_metrobus, route_order)
VALUES
    ('76D', '76D - Bahçeşehir / Taksim', 'Bahçeşehir', 'Taksim', 'İKT', false, 1),
    ('55T', '55T - Gaziosmanpaşa / Taksim', 'Gaziosmanpaşa', 'Taksim', 'EDK', false, 1),
    ('145T', '145T - Beylikdüzü / Taksim', 'Beylikdüzü', 'Taksim', 'BYL', false, 1),
    ('500L', '500L - 4. Levent / Cevizlibağ', '4. Levent', 'Cevizlibağ', 'KAG', false, 1),
    ('11ÜS', '11ÜS - Sultanbeyli / Üsküdar', 'Sultanbeyli', 'Üsküdar', 'ANA', false, 1),
    ('15F', '15F - Beykoz / Kadıköy', 'Beykoz', 'Kadıköy', 'HAS', false, 1),
    ('76B', '76B - Avcılar / Bakırköy', 'Avcılar', 'Bakırköy', 'AVC', false, 1),
    ('130Ş', '130Ş - Şifa Mahallesi / Kadıköy', 'Şifa Mahallesi', 'Kadıköy', 'KRT', false, 1),
    ('133T', '133T - Tuzla / Bostancı', 'Tuzla', 'Bostancı', 'YUN', false, 1),
    ('36T', '36T - Cebeci / Taksim', 'Cebeci', 'Taksim', 'SLG', false, 1),
    ('522ST', '522ST - Sultanbeyli / Mecidiyeköy', 'Sultanbeyli', 'Mecidiyeköy', 'SRG', false, 1),
    ('93T', '93T - Zeytinburnu / Taksim', 'Zeytinburnu', 'Taksim', 'TOP', false, 1),
    ('15ÇK', '15ÇK - Şahinkaya / Kadıköy', 'Şahinkaya', 'Kadıköy', 'ŞHK', false, 1),
    ('BA-2', 'BA-2 - Büyükada Ring Hattı', 'Büyükada Merkez', 'Lunapark Meydanı', 'ADA', false, 1),
    ('MB-01', 'Beylikdüzü / Söğütlüçeşme Gidiş-Dönüş', 'Beylikdüzü', 'Söğütlüçeşme', 'BYM', true, 1),
    ('MB-02', 'Cevizlibağ / Söğütlüçeşme Gidiş-Dönüş', 'Cevizlibağ', 'Söğütlüçeşme', 'CBM', true, 1),
    ('MB-03', 'Cevizlibağ / Beylikdüzü Gidiş-Dönüş', 'Cevizlibağ', 'Beylikdüzü', 'CBM', true, 2),
    ('MB-04', 'Avcılar / Beylikdüzü Gidiş-Dönüş', 'Avcılar', 'Beylikdüzü', 'BYM', true, 2),
    ('MB-05', 'Avcılar / Söğütlüçeşme Gidiş-Dönüş', 'Avcılar', 'Söğütlüçeşme', 'SLM', true, 1),
    ('MB-06', 'Zincirlikuyu / Söğütlüçeşme Gidiş-Dönüş', 'Zincirlikuyu', 'Söğütlüçeşme', 'ZKM', true, 1),
    ('MB-07', 'Zincirlikuyu / Beylikdüzü Gidiş-Dönüş', 'Zincirlikuyu', 'Beylikdüzü', 'ZKM', true, 2);

-- Eski planı silmek yerine geçmişi koruyarak pasife al.
UPDATE task_assignments ta
SET is_active = false, ended_at = now(),
    description = concat_ws(' | ', ta.description, 'Yeni garaj-hat planı nedeniyle sonlandırıldı.')
FROM service_tasks st
WHERE ta.service_task_id = st.id AND ta.is_active
  AND st.service_date BETWEEN DATE '2026-07-24' AND DATE '2026-07-30';

UPDATE service_tasks
SET is_active = false, deactivated_at = now(), deactivated_by_user_id = 2,
    deactivation_reason = 'Yeni garaj-hat planı ile değiştirildi.'
WHERE is_active AND service_date BETWEEN DATE '2026-07-24' AND DATE '2026-07-30';

UPDATE service_duties
SET is_active = false, deactivated_at = now(), deactivated_by_user_id = 2,
    deactivation_reason = 'Yeni garaj-hat planı ile değiştirildi.'
WHERE is_active AND service_date BETWEEN DATE '2026-07-24' AND DATE '2026-07-30';

UPDATE routes
SET code = left('OLD-' || id || '-' || code, 30), is_active = false
WHERE is_active;

INSERT INTO routes (code, name, start_point, end_point, is_active)
SELECT route_code, route_name, start_point, end_point, true
FROM schedule_route_plan ORDER BY is_metrobus, garage_code, route_order;

CREATE TEMP TABLE generated_duties
(
    duty_number varchar(50) PRIMARY KEY,
    service_date date NOT NULL,
    garage_id bigint NOT NULL,
    route_id bigint NOT NULL,
    vehicle_id bigint NOT NULL,
    driver_id bigint NOT NULL,
    shift_no integer NOT NULL,
    shift_start_hour integer NOT NULL,
    is_metrobus boolean NOT NULL
) ON COMMIT DROP;

WITH route_resources AS
(
    SELECT p.route_code, p.garage_code, p.is_metrobus, p.route_order,
           r.id route_id, g.id garage_id
    FROM schedule_route_plan p
    JOIN routes r ON r.code = p.route_code AND r.is_active
    JOIN garages g ON g.code = p.garage_code AND g.is_active
),
vehicle_pool AS
(
    SELECT v.garage_id, v.id,
           row_number() OVER (PARTITION BY v.garage_id ORDER BY v.door_number, v.id) rn,
           count(*) OVER (PARTITION BY v.garage_id) pool_count
    FROM vehicles v
    JOIN vehicle_types vt ON vt.id = v.vehicle_type_id
    WHERE v.is_active AND v.vehicle_status_id IN (1, 6, 7)
      AND vt.name IN ('Otobüs', 'Metrobüs')
),
driver_pool AS
(
    SELECT d.garage_id, d.id,
           row_number() OVER (PARTITION BY d.garage_id ORDER BY d.personnel_number, d.id) rn,
           count(*) OVER (PARTITION BY d.garage_id) pool_count
    FROM drivers d
    WHERE d.is_active AND d.driver_type = 'NORMAL'
      AND d.availability_status IN ('AVAILABLE', 'ON_DUTY')
),
duty_seed AS
(
    SELECT rr.*, day_value::date service_date, shift.shift_no, shift.shift_start_hour,
           ((rr.route_order - 1) * CASE WHEN rr.is_metrobus THEN 4 ELSE 3 END + shift.shift_no + 1) resource_slot
    FROM route_resources rr
    CROSS JOIN generate_series(DATE '2026-07-24', DATE '2026-07-30', INTERVAL '1 day') day_value
    CROSS JOIN LATERAL
    (
        SELECT * FROM (VALUES (0, 0), (1, 6), (2, 12), (3, 18)) value(shift_no, shift_start_hour)
        WHERE rr.is_metrobus OR value.shift_no > 0
    ) shift
)
INSERT INTO generated_duties
    (duty_number, service_date, garage_id, route_id, vehicle_id, driver_id,
     shift_no, shift_start_hour, is_metrobus)
SELECT 'PLAN-' || to_char(ds.service_date, 'YYYYMMDD') || '-' || ds.route_code || '-S' || ds.shift_no,
       ds.service_date, ds.garage_id, ds.route_id, vp.id, dp.id,
       ds.shift_no, ds.shift_start_hour, ds.is_metrobus
FROM duty_seed ds
JOIN vehicle_pool vp ON vp.garage_id = ds.garage_id
 AND vp.rn = ((ds.resource_slot - 1) % vp.pool_count) + 1
JOIN driver_pool dp ON dp.garage_id = ds.garage_id
 AND dp.rn = ((ds.resource_slot - 1) % dp.pool_count) + 1;

INSERT INTO service_duties
    (duty_number, service_date, garage_id, route_id, original_vehicle_id,
     original_driver_id, status, description, created_by_user_id, is_active)
SELECT duty_number, service_date, garage_id, route_id, vehicle_id, driver_id,
       'PLANNED',
       CASE WHEN is_metrobus
            THEN '24 saatlik metrobüs planı; altı gidiş-dönüş görevi.'
            ELSE '06.00-00.00 otobüs planı; altı gidiş-dönüş görevi.' END,
       2, true
FROM generated_duties;

INSERT INTO service_tasks
    (task_number, route_id, service_date, sequence_number,
     planned_departure_at, planned_arrival_at, status, is_active,
     created_by_user_id, service_duty_id)
SELECT gd.duty_number || '-G' || lpad(task_seq::text, 2, '0'),
       gd.route_id, gd.service_date, task_seq,
       gd.service_date::timestamp + make_interval(hours => gd.shift_start_hour + task_seq - 1),
       gd.service_date::timestamp + make_interval(hours => gd.shift_start_hour + task_seq),
       'PLANNED', true, 2, sd.id
FROM generated_duties gd
JOIN service_duties sd ON sd.duty_number = gd.duty_number AND sd.is_active
CROSS JOIN generate_series(1, 6) task_seq;

INSERT INTO task_assignments
    (service_task_id, vehicle_id, driver_id, assignment_type,
     assigned_by_user_id, assigned_at, is_active, description)
SELECT st.id, gd.vehicle_id, gd.driver_id, 'ORIGINAL', 2, now(), true,
       'Garaj-hat vardiya planıyla otomatik atandı.'
FROM generated_duties gd
JOIN service_duties sd ON sd.duty_number = gd.duty_number AND sd.is_active
JOIN service_tasks st ON st.service_duty_id = sd.id AND st.is_active;

UPDATE drivers d SET availability_status = 'AVAILABLE'
WHERE d.availability_status = 'ON_DUTY'
  AND NOT EXISTS
  (
      SELECT 1 FROM task_assignments ta
      JOIN service_tasks st ON st.id = ta.service_task_id
      WHERE ta.driver_id = d.id AND ta.is_active AND st.is_active
        AND st.status NOT IN ('COMPLETED', 'CANCELLED')
  );

UPDATE drivers d SET availability_status = 'ON_DUTY'
WHERE EXISTS
(
    SELECT 1 FROM task_assignments ta
    JOIN service_tasks st ON st.id = ta.service_task_id
    WHERE ta.driver_id = d.id AND ta.is_active AND st.is_active
      AND st.status NOT IN ('COMPLETED', 'CANCELLED')
);

UPDATE vehicles v SET vehicle_status_id = 6
WHERE v.vehicle_status_id = 7
  AND NOT EXISTS
  (
      SELECT 1 FROM task_assignments ta
      JOIN service_tasks st ON st.id = ta.service_task_id
      WHERE ta.vehicle_id = v.id AND ta.is_active AND st.is_active
        AND st.status NOT IN ('COMPLETED', 'CANCELLED')
  )
  AND NOT EXISTS
  (
      SELECT 1 FROM fault_resource_assignments fra
      WHERE fra.vehicle_id = v.id AND fra.is_active
        AND fra.status NOT IN ('COMPLETED', 'CANCELLED')
  );

UPDATE vehicles v SET vehicle_status_id = 7
WHERE EXISTS
(
    SELECT 1 FROM task_assignments ta
    JOIN service_tasks st ON st.id = ta.service_task_id
    WHERE ta.vehicle_id = v.id AND ta.is_active AND st.is_active
      AND st.status NOT IN ('COMPLETED', 'CANCELLED')
);

COMMIT;
-- Hat, vardiya ve servis görevlerini belirlenen günlük sefer kuralına göre yeniden oluşturur.
