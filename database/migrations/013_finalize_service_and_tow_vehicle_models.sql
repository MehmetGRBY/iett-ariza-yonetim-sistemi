BEGIN;

SET search_path TO fault_management, public;

-- Bu migration yolcu otobüsü ve metrobüs modellerine dokunmaz.
-- Yalnızca hizmet aracı ve çekici filolarını sabit listelere dağıtır.

DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM system_settings
        WHERE setting_key = 'service_tow_fixed_model_distribution_v1'
    ) THEN
        RAISE EXCEPTION 'Hizmet ve çekici sabit model dağılımı daha önce uygulanmış.';
    END IF;
END $$;

-- ============================================================
-- 1) HİZMET ARAÇLARI: 275 ARAÇ
-- 55 Ford Transit Minibüs
-- 40 Volkswagen Transporter
-- 45 Fiat Doblo Cargo
-- 35 Renault Kangoo
-- 30 Ford Tourneo Custom
-- 25 Fiat Egea Sedan
-- 25 Renault Megane Sedan
-- 20 Toyota Corolla Hybrid
-- ============================================================
WITH ranked_service_vehicles AS (
    SELECT
        v.id,
        row_number() OVER (ORDER BY v.id) AS rn
    FROM vehicles v
    JOIN vehicle_types vt ON vt.id = v.vehicle_type_id
    WHERE vt.name = 'Hizmet Aracı'
),
service_values AS (
    SELECT
        r.id,
        r.rn,
        CASE
            WHEN r.rn <= 55  THEN 'Ford'
            WHEN r.rn <= 95  THEN 'Volkswagen'
            WHEN r.rn <= 140 THEN 'Fiat'
            WHEN r.rn <= 175 THEN 'Renault'
            WHEN r.rn <= 205 THEN 'Ford'
            WHEN r.rn <= 230 THEN 'Fiat'
            WHEN r.rn <= 255 THEN 'Renault'
            ELSE 'Toyota'
        END AS brand,
        CASE
            WHEN r.rn <= 55  THEN 'Transit Minibüs'
            WHEN r.rn <= 95  THEN 'Transporter'
            WHEN r.rn <= 140 THEN 'Doblo Cargo'
            WHEN r.rn <= 175 THEN 'Kangoo'
            WHEN r.rn <= 205 THEN 'Tourneo Custom'
            WHEN r.rn <= 230 THEN 'Egea Sedan'
            WHEN r.rn <= 255 THEN 'Megane Sedan'
            ELSE 'Corolla Hybrid'
        END AS model,
        CASE
            WHEN r.rn <= 95  THEN 'Personel Servisi'
            WHEN r.rn <= 175 THEN 'Saha Destek'
            WHEN r.rn <= 205 THEN 'Personel Servisi'
            ELSE 'Binek Hizmet Aracı'
        END AS duty_type,
        CASE
            WHEN r.rn > 255 THEN 'Hibrit'
            ELSE 'Dizel'
        END AS fuel_name,
        CASE
            WHEN r.rn <= 55  THEN (2018 + r.rn % 7)::smallint
            WHEN r.rn <= 95  THEN (2019 + r.rn % 6)::smallint
            WHEN r.rn <= 140 THEN (2018 + r.rn % 7)::smallint
            WHEN r.rn <= 175 THEN (2019 + r.rn % 6)::smallint
            WHEN r.rn <= 205 THEN (2020 + r.rn % 5)::smallint
            WHEN r.rn <= 230 THEN (2020 + r.rn % 5)::smallint
            WHEN r.rn <= 255 THEN (2019 + r.rn % 6)::smallint
            ELSE (2021 + r.rn % 4)::smallint
        END AS model_year
    FROM ranked_service_vehicles r
)
UPDATE vehicles v
SET brand = sv.brand,
    model = sv.model,
    model_year = sv.model_year,
    duty_type = sv.duty_type,
    fuel_type_id = ft.id,
    capacity = NULL,
    current_mileage =
        GREATEST(EXTRACT(YEAR FROM CURRENT_DATE)::integer - sv.model_year, 1)
        * (17000 + (sv.rn % 9001)::integer)
        + (sv.rn % 4000)::integer
FROM service_values sv
JOIN fuel_types ft ON ft.name = sv.fuel_name AND ft.is_active
WHERE v.id = sv.id;

-- ============================================================
-- 2) ÇEKİCİLER: 275 ARAÇ, HER MODELDEN 55
-- ============================================================
WITH ranked_tow_vehicles AS (
    SELECT
        v.id,
        row_number() OVER (ORDER BY v.id) AS rn
    FROM vehicles v
    JOIN vehicle_types vt ON vt.id = v.vehicle_type_id
    WHERE vt.name = 'Çekici'
),
tow_values AS (
    SELECT
        r.id,
        r.rn,
        CASE
            WHEN r.rn <= 55  THEN 'Ford Trucks'
            WHEN r.rn <= 110 THEN 'Mercedes-Benz'
            WHEN r.rn <= 165 THEN 'Iveco'
            WHEN r.rn <= 220 THEN 'BMC'
            ELSE 'MAN'
        END AS brand,
        CASE
            WHEN r.rn <= 55  THEN 'Cargo 1833 DC Oto Kurtarıcı'
            WHEN r.rn <= 110 THEN 'Atego 1824 Oto Kurtarıcı'
            WHEN r.rn <= 165 THEN 'Eurocargo 180E Oto Kurtarıcı'
            WHEN r.rn <= 220 THEN 'Tuğra 1846 Oto Kurtarıcı'
            ELSE 'TGM 18.290 Oto Kurtarıcı'
        END AS model,
        (2017 + r.rn % 8)::smallint AS model_year
    FROM ranked_tow_vehicles r
)
UPDATE vehicles v
SET brand = tv.brand,
    model = tv.model,
    model_year = tv.model_year,
    duty_type = 'Araç Kurtarma',
    fuel_type_id = ft.id,
    capacity = NULL,
    current_mileage =
        GREATEST(EXTRACT(YEAR FROM CURRENT_DATE)::integer - tv.model_year, 1)
        * (12000 + (tv.rn % 7001)::integer)
        + (tv.rn % 3000)::integer
FROM tow_values tv
JOIN fuel_types ft ON ft.name = 'Dizel' AND ft.is_active
WHERE v.id = tv.id;

INSERT INTO system_settings (
    setting_key,
    setting_value,
    description,
    is_active,
    updated_by_user_id
)
VALUES (
    'service_tow_fixed_model_distribution_v1',
    jsonb_build_object(
        'appliedAt', now(),
        'serviceVehicleCount', 275,
        'towVehicleCount', 275
    ),
    'Hizmet ve çekici filoları sabit, kullanım amacına uygun marka-model listelerine dağıtıldı.',
    true,
    (SELECT id FROM app_users WHERE personnel_number = 'ADM-0001')
);

INSERT INTO audit_logs (
    user_id,
    role_id,
    action,
    entity_type,
    new_values,
    description
)
SELECT
    u.id,
    u.role_id,
    'SERVICE_TOW_MODEL_FINALIZATION',
    'vehicles',
    jsonb_build_object(
        'serviceVehicleCount', 275,
        'towVehicleCount', 275,
        'executedAt', now()
    ),
    'Hizmet ve çekici araçların sabit marka-model, görev, yakıt, yıl ve kilometre verileri güncellendi.'
FROM app_users u
WHERE u.personnel_number = 'ADM-0001';

COMMIT;

-- Son kontrol
SELECT
    vt.name AS vehicle_type,
    v.brand,
    v.model,
    v.duty_type,
    ft.name AS fuel_type,
    COUNT(*) AS quantity,
    MIN(v.model_year) AS min_year,
    MAX(v.model_year) AS max_year,
    MIN(v.current_mileage) AS min_mileage,
    MAX(v.current_mileage) AS max_mileage
FROM fault_management.vehicles v
JOIN fault_management.vehicle_types vt ON vt.id = v.vehicle_type_id
JOIN fault_management.fuel_types ft ON ft.id = v.fuel_type_id
WHERE vt.name IN ('Hizmet Aracı', 'Çekici')
GROUP BY vt.name, v.brand, v.model, v.duty_type, ft.name
ORDER BY vt.name, v.duty_type, v.brand, v.model;
-- Hizmet aracı ve çekicilerin marka/model verilerini sabit ve gerçekçi operasyon tiplerine göre son haline getirir.
