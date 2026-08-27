BEGIN;

SET search_path TO fault_management, public;

-- Gerekli tanımlar ve dört metrobüs garajı mevcut/aktif olmalıdır.
DO $$
DECLARE
    active_metrobus_garage_count integer;
BEGIN
    SELECT COUNT(*)
    INTO active_metrobus_garage_count
    FROM garages
    WHERE code IN ('BYM', 'CBM', 'ZKM', 'SLM')
      AND is_active = true;

    IF active_metrobus_garage_count <> 4 THEN
        RAISE EXCEPTION 'BYM, CBM, ZKM ve SLM garajlarının dördü de aktif olmalıdır.';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM vehicle_types WHERE name = 'Metrobüs' AND is_active = true
    ) THEN
        RAISE EXCEPTION 'vehicle_types tablosunda aktif Metrobüs kaydı bulunamadı.';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM fuel_types WHERE name = 'Dizel' AND is_active = true
    ) THEN
        RAISE EXCEPTION 'fuel_types tablosunda aktif Dizel kaydı bulunamadı.';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM vehicle_statuses WHERE code = 'AVAILABLE' AND is_active = true
    ) THEN
        RAISE EXCEPTION 'Önce görev yönetimi migration dosyasını çalıştırın: AVAILABLE durumu bulunamadı.';
    END IF;
END $$;

-- Her garajın %75 hedefinden ileride eklenecek 10 hizmet aracı ve
-- 10 çekici çıkarılarak metrobüs adetleri hesaplanmıştır.
WITH garage_targets(garage_code, metrobus_count) AS (
    VALUES
        ('BYM', 490),
        ('CBM', 242),
        ('ZKM', 242),
        ('SLM', 490)
),
expanded AS (
    SELECT
        gt.garage_code,
        gs.garage_sequence,
        row_number() OVER (ORDER BY gt.garage_code, gs.garage_sequence) AS global_sequence
    FROM garage_targets gt
    CROSS JOIN LATERAL generate_series(1, gt.metrobus_count) AS gs(garage_sequence)
),
resolved AS (
    SELECT
        e.*,
        g.id AS garage_id,
        vt.id AS vehicle_type_id,
        ft.id AS fuel_type_id,
        vs.id AS vehicle_status_id
    FROM expanded e
    JOIN garages g
      ON g.code = e.garage_code AND g.is_active = true
    CROSS JOIN LATERAL (
        SELECT id FROM vehicle_types
        WHERE name = 'Metrobüs' AND is_active = true
        ORDER BY id LIMIT 1
    ) vt
    CROSS JOIN LATERAL (
        SELECT id FROM fuel_types
        WHERE name = 'Dizel' AND is_active = true
        ORDER BY id LIMIT 1
    ) ft
    CROSS JOIN LATERAL (
        SELECT id FROM vehicle_statuses
        WHERE code = 'AVAILABLE' AND is_active = true
        ORDER BY id LIMIT 1
    ) vs
)
INSERT INTO vehicles
(
    door_number,
    plate,
    brand,
    model,
    model_year,
    vehicle_type_id,
    fuel_type_id,
    current_mileage,
    garage_id,
    vehicle_status_id,
    duty_type,
    capacity
)
SELECT
    'MB-' || lpad(global_sequence::text, 6, '0'),
    '34 MB ' || lpad(global_sequence::text, 6, '0'),
    CASE ((global_sequence - 1) % 4)
        WHEN 0 THEN 'Mercedes-Benz'
        WHEN 1 THEN 'Mercedes-Benz'
        WHEN 2 THEN 'Otokar'
        ELSE 'Akia'
    END,
    CASE ((global_sequence - 1) % 4)
        WHEN 0 THEN 'Capacity'
        WHEN 1 THEN 'Conecto'
        WHEN 2 THEN 'Kent XL'
        ELSE 'Ultra LF 25'
    END,
    2020,
    vehicle_type_id,
    fuel_type_id,
    0,
    garage_id,
    vehicle_status_id,
    'Demo Metrobüs',
    NULL
FROM resolved
ON CONFLICT (door_number) DO NOTHING;

COMMIT;

-- Garaj dağılımı kontrolü
SELECT
    g.code,
    g.name,
    g.vehicle_capacity,
    COUNT(v.id) AS metrobus_count
FROM fault_management.garages g
LEFT JOIN fault_management.vehicles v
  ON v.garage_id = g.id
 AND v.door_number LIKE 'MB-%'
WHERE g.code IN ('BYM', 'CBM', 'ZKM', 'SLM')
GROUP BY g.id, g.code, g.name, g.vehicle_capacity
ORDER BY g.code;

-- Marka/model dağılımı kontrolü: her model 366 adet olmalıdır.
SELECT
    brand,
    model,
    COUNT(*) AS vehicle_count
FROM fault_management.vehicles
WHERE door_number LIKE 'MB-%'
GROUP BY brand, model
ORDER BY brand, model;

SELECT COUNT(*) AS total_demo_metrobus
FROM fault_management.vehicles
WHERE door_number LIKE 'MB-%';
-- Metrobüs garajlarına uygun tip, marka, model ve kapasitede demo metrobüsleri ekler.
