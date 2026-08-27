BEGIN;

SET search_path TO fault_management, public;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM vehicle_types WHERE name = 'Hizmet Aracı' AND is_active = true
    ) THEN
        RAISE EXCEPTION 'vehicle_types tablosunda aktif Hizmet Aracı kaydı bulunamadı.';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM vehicle_types WHERE name = 'Çekici' AND is_active = true
    ) THEN
        RAISE EXCEPTION 'vehicle_types tablosunda aktif Çekici kaydı bulunamadı.';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM fuel_types WHERE name = 'Dizel' AND is_active = true
    ) THEN
        RAISE EXCEPTION 'fuel_types tablosunda aktif Dizel kaydı bulunamadı.';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM vehicle_statuses WHERE code = 'AVAILABLE' AND is_active = true
    ) THEN
        RAISE EXCEPTION 'vehicle_statuses tablosunda AVAILABLE durumu bulunamadı.';
    END IF;
END $$;

-- BYL için 5, diğer tüm aktif garajlar için 10 hizmet aracı.
WITH garage_targets AS (
    SELECT
        g.id AS garage_id,
        g.code AS garage_code,
        CASE WHEN g.code = 'BYL' THEN 5 ELSE 10 END AS vehicle_count
    FROM garages g
    WHERE g.is_active = true
),
expanded AS (
    SELECT
        gt.garage_id,
        gt.garage_code,
        gs.garage_sequence,
        row_number() OVER (ORDER BY gt.garage_code, gs.garage_sequence) AS global_sequence
    FROM garage_targets gt
    CROSS JOIN LATERAL generate_series(1, gt.vehicle_count) AS gs(garage_sequence)
),
definitions AS (
    SELECT
        (SELECT id FROM vehicle_types WHERE name = 'Hizmet Aracı' AND is_active ORDER BY id LIMIT 1) AS vehicle_type_id,
        (SELECT id FROM fuel_types WHERE name = 'Dizel' AND is_active ORDER BY id LIMIT 1) AS fuel_type_id,
        (SELECT id FROM vehicle_statuses WHERE code = 'AVAILABLE' AND is_active ORDER BY id LIMIT 1) AS vehicle_status_id
)
INSERT INTO vehicles
(
    door_number, plate, brand, model, model_year,
    vehicle_type_id, fuel_type_id, current_mileage,
    garage_id, vehicle_status_id, duty_type, capacity
)
SELECT
    'HZM-' || garage_code || '-' || lpad(garage_sequence::text, 3, '0'),
    '34 HZM ' || lpad(global_sequence::text, 6, '0'),
    'Demo',
    'Hizmet Aracı',
    2020,
    d.vehicle_type_id,
    d.fuel_type_id,
    0,
    e.garage_id,
    d.vehicle_status_id,
    'Hizmet Aracı',
    NULL
FROM expanded e
CROSS JOIN definitions d
ON CONFLICT (door_number) DO NOTHING;

-- BYL için 5, diğer tüm aktif garajlar için 10 çekici.
WITH garage_targets AS (
    SELECT
        g.id AS garage_id,
        g.code AS garage_code,
        CASE WHEN g.code = 'BYL' THEN 5 ELSE 10 END AS vehicle_count
    FROM garages g
    WHERE g.is_active = true
),
expanded AS (
    SELECT
        gt.garage_id,
        gt.garage_code,
        gs.garage_sequence,
        row_number() OVER (ORDER BY gt.garage_code, gs.garage_sequence) AS global_sequence
    FROM garage_targets gt
    CROSS JOIN LATERAL generate_series(1, gt.vehicle_count) AS gs(garage_sequence)
),
definitions AS (
    SELECT
        (SELECT id FROM vehicle_types WHERE name = 'Çekici' AND is_active ORDER BY id LIMIT 1) AS vehicle_type_id,
        (SELECT id FROM fuel_types WHERE name = 'Dizel' AND is_active ORDER BY id LIMIT 1) AS fuel_type_id,
        (SELECT id FROM vehicle_statuses WHERE code = 'AVAILABLE' AND is_active ORDER BY id LIMIT 1) AS vehicle_status_id
)
INSERT INTO vehicles
(
    door_number, plate, brand, model, model_year,
    vehicle_type_id, fuel_type_id, current_mileage,
    garage_id, vehicle_status_id, duty_type, capacity
)
SELECT
    'CKC-' || garage_code || '-' || lpad(garage_sequence::text, 3, '0'),
    '34 CKC ' || lpad(global_sequence::text, 6, '0'),
    'Demo',
    'Çekici',
    2020,
    d.vehicle_type_id,
    d.fuel_type_id,
    0,
    e.garage_id,
    d.vehicle_status_id,
    'Çekici',
    NULL
FROM expanded e
CROSS JOIN definitions d
ON CONFLICT (door_number) DO NOTHING;

COMMIT;

-- Garaj bazında hizmet aracı ve çekici kontrolü
SELECT
    g.code,
    g.name,
    COUNT(v.id) FILTER (WHERE vt.name = 'Hizmet Aracı' AND v.is_active) AS service_vehicle_count,
    COUNT(v.id) FILTER (WHERE vt.name = 'Çekici' AND v.is_active) AS tow_vehicle_count
FROM fault_management.garages g
LEFT JOIN fault_management.vehicles v ON v.garage_id = g.id
LEFT JOIN fault_management.vehicle_types vt ON vt.id = v.vehicle_type_id
WHERE g.is_active = true
GROUP BY g.id, g.code, g.name
ORDER BY g.name;

-- Destek araçları eklendikten sonraki genel doluluk
SELECT
    g.code,
    g.name,
    g.vehicle_capacity,
    COUNT(v.id) FILTER (WHERE v.is_active) AS active_vehicle_count,
    ROUND(
        COUNT(v.id) FILTER (WHERE v.is_active) * 100.0
        / NULLIF(g.vehicle_capacity, 0),
        2
    ) AS occupancy_percent
FROM fault_management.garages g
LEFT JOIN fault_management.vehicles v ON v.garage_id = g.id
WHERE g.is_active = true
GROUP BY g.id, g.code, g.name, g.vehicle_capacity
ORDER BY g.name;
-- Garajlara operasyon için hizmet aracı ve çekici demo filosu ekler.
