BEGIN;

SET search_path TO fault_management, public;

-- Güvenlik kontrolleri: en az bir aktif garaj ve gerekli tanımlar bulunmalıdır.
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM garages WHERE is_active = true) THEN
        RAISE EXCEPTION 'Önce en az bir aktif garaj eklemelisiniz.';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM vehicle_types WHERE name = 'Otobüs' AND is_active = true) THEN
        RAISE EXCEPTION 'vehicle_types tablosunda aktif Otobüs kaydı bulunamadı.';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM vehicle_statuses WHERE code = 'IN_SERVICE' AND is_active = true) THEN
        RAISE EXCEPTION 'vehicle_statuses tablosunda IN_SERVICE kaydı bulunamadı.';
    END IF;

    IF (SELECT count(DISTINCT name) FROM fuel_types WHERE name IN ('Dizel', 'CNG', 'Elektrik') AND is_active = true) <> 3 THEN
        RAISE EXCEPTION 'Dizel, CNG ve Elektrik yakıt tipi kayıtlarının üçü de bulunmalıdır.';
    END IF;
END $$;

WITH model_counts(brand, model, vehicle_count, fuel_name) AS (
    VALUES
        ('Akia',          'LF25',                 132, 'Dizel'),
        ('BMC',           'Procity',               48, 'Dizel'),
        ('BMC',           'Procity TR',           381, 'Dizel'),
        ('Cleanvac',      'Emicro',                60, 'Elektrik'),
        ('Green Car',     'LSV 4 Kabinli',         20, 'Elektrik'),
        ('Green Car',     'S 14 Kabinli',          40, 'Elektrik'),
        ('Karsan',        'Avancity CNG',          245, 'CNG'),
        ('Karsan',        'Avancity S Plus',       305, 'Dizel'),
        ('Mercedes-Benz', 'Capacity (Körüklü)',    249, 'Dizel'),
        ('Mercedes-Benz', 'Citaro 0530 G',          88, 'Dizel'),
        ('Mercedes-Benz', 'Citaro 0530',           356, 'Dizel'),
        ('Mercedes-Benz', 'Conecto G',              389, 'Dizel'),
        ('Mercedes-Benz', 'Conecto',                 13, 'Dizel'),
        ('Otokar',        'Kent 290 LF',            933, 'Dizel'),
        ('Otokar',        'Kent XL',                120, 'Dizel'),
        ('Temsa',         'Avenue LF CNG',          107, 'CNG'),
        ('SGMS',          'MASTIFF M4',              60, 'Elektrik'),
        ('Akia',          'Ultra LF 12',            150, 'Dizel'),
        ('Karsan',        'E-JEST',                  60, 'Elektrik')
),
expanded AS (
    SELECT
        mc.brand,
        mc.model,
        mc.fuel_name,
        gs.model_sequence,
        row_number() OVER (ORDER BY mc.brand, mc.model, gs.model_sequence) AS global_sequence
    FROM model_counts mc
    CROSS JOIN LATERAL generate_series(1, mc.vehicle_count) AS gs(model_sequence)
),
active_garages AS (
    SELECT
        id,
        row_number() OVER (ORDER BY id) AS garage_sequence,
        count(*) OVER () AS garage_count
    FROM garages
    WHERE is_active = true
),
resolved AS (
    SELECT
        e.*,
        g.id AS garage_id,
        ft.id AS fuel_type_id,
        vt.id AS vehicle_type_id,
        vs.id AS vehicle_status_id
    FROM expanded e
    JOIN active_garages g
      ON g.garage_sequence = ((e.global_sequence - 1) % g.garage_count) + 1
    JOIN fuel_types ft
      ON ft.name = e.fuel_name AND ft.is_active = true
    CROSS JOIN LATERAL (
        SELECT id
        FROM vehicle_types
        WHERE name = 'Otobüs' AND is_active = true
        ORDER BY id
        LIMIT 1
    ) vt
    CROSS JOIN LATERAL (
        SELECT id
        FROM vehicle_statuses
        WHERE code = 'IN_SERVICE' AND is_active = true
        ORDER BY id
        LIMIT 1
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
    'DEMO-' || lpad(global_sequence::text, 6, '0'),
    '34 DEMO ' || lpad(global_sequence::text, 6, '0'),
    brand,
    model,
    2020,
    vehicle_type_id,
    fuel_type_id,
    0,
    garage_id,
    vehicle_status_id,
    'Demo veri',
    NULL
FROM resolved
ON CONFLICT (door_number) DO NOTHING;

COMMIT;

-- Eklenen demo araçlarını model bazında kontrol eder.
SELECT
    brand AS marka,
    model,
    COUNT(*) AS adet
FROM fault_management.vehicles
WHERE door_number LIKE 'DEMO-%'
GROUP BY brand, model
ORDER BY brand, model;

SELECT COUNT(*) AS toplam_demo_arac
FROM fault_management.vehicles
WHERE door_number LIKE 'DEMO-%';
-- Test ve sunum için belirtilen marka/model adetlerine uygun demo otobüs kayıtları üretir.
