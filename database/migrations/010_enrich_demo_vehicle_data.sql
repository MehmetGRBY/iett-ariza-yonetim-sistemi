BEGIN;

SET search_path TO fault_management, public;

-- Aynı veri zenginleştirmesinin yanlışlıkla ikinci kez uygulanmasını engeller.
DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM system_settings
        WHERE setting_key = 'demo_vehicle_data_quality_v1_applied'
    ) THEN
        RAISE EXCEPTION 'Demo araç veri kalitesi güncellemesi daha önce uygulanmış.';
    END IF;
END $$;

-- Garaj ad ve kodlarında baştaki/sondaki ya da tekrarlanan boşlukları temizler.
UPDATE garages
SET code = upper(btrim(code)),
    name = regexp_replace(btrim(name), '\s+', ' ', 'g')
WHERE code <> upper(btrim(code))
   OR name <> regexp_replace(btrim(name), '\s+', ' ', 'g');

-- Mevcut otobüs modelleri için örnek fakat modelle uyumlu üretim yılları.
UPDATE vehicles v
SET model_year = CASE
        WHEN v.brand = 'BMC' AND v.model = 'Procity'             THEN (2012 + v.id % 4)::smallint
        WHEN v.brand = 'BMC' AND v.model = 'Procity TR'          THEN (2018 + v.id % 4)::smallint
        WHEN v.brand = 'Mercedes-Benz' AND v.model = 'Citaro 0530 G' THEN (2007 + v.id % 4)::smallint
        WHEN v.brand = 'Mercedes-Benz' AND v.model = 'Citaro 0530'   THEN (2006 + v.id % 5)::smallint
        WHEN v.brand = 'Mercedes-Benz' AND v.model = 'Conecto G'     THEN (2012 + v.id % 4)::smallint
        WHEN v.brand = 'Mercedes-Benz' AND v.model = 'Conecto'       THEN (2011 + v.id % 4)::smallint
        WHEN v.brand = 'Mercedes-Benz' AND v.model = 'Capacity (Körüklü)' THEN (2008 + v.id % 4)::smallint
        WHEN v.brand = 'Otokar' AND v.model = 'Kent 290 LF'      THEN (2012 + v.id % 7)::smallint
        WHEN v.brand = 'Otokar' AND v.model = 'Kent XL'          THEN (2021 + v.id % 4)::smallint
        WHEN v.brand = 'Karsan' AND v.model = 'Avancity CNG'     THEN (2013 + v.id % 4)::smallint
        WHEN v.brand = 'Karsan' AND v.model = 'Avancity S Plus'  THEN (2017 + v.id % 4)::smallint
        WHEN v.brand = 'Karsan' AND v.model = 'E-JEST'           THEN (2021 + v.id % 4)::smallint
        WHEN v.brand = 'Temsa' AND v.model = 'Avenue LF CNG'     THEN (2014 + v.id % 4)::smallint
        WHEN v.brand = 'Akia' AND v.model = 'LF25'               THEN (2021 + v.id % 4)::smallint
        WHEN v.brand = 'Akia' AND v.model = 'Ultra LF 12'        THEN (2020 + v.id % 5)::smallint
        WHEN v.brand = 'Cleanvac' AND v.model = 'Emicro'         THEN (2022 + v.id % 3)::smallint
        WHEN v.brand = 'Green Car'                               THEN (2021 + v.id % 4)::smallint
        WHEN v.brand = 'SGMS' AND v.model = 'MASTIFF M4'         THEN (2022 + v.id % 3)::smallint
        ELSE (2015 + v.id % 8)::smallint
    END,
    duty_type = CASE
        WHEN g.code = 'ARV' THEN 'Yedek Hat Aracı'
        ELSE 'Hat İşletme'
    END
FROM vehicle_types vt, garages g
WHERE v.vehicle_type_id = vt.id
  AND v.garage_id = g.id
  AND vt.name = 'Otobüs';

-- Metrobüs filosunda eski ve yeni nesil modelleri ayırır.
UPDATE vehicles v
SET brand = CASE
        WHEN v.brand = 'Akia' THEN 'Akia'
        ELSE btrim(v.brand)
    END,
    model_year = CASE
        WHEN v.brand = 'Mercedes-Benz' AND v.model = 'Capacity' THEN (2008 + v.id % 4)::smallint
        WHEN v.brand = 'Mercedes-Benz' AND v.model = 'Conecto'  THEN (2009 + v.id % 4)::smallint
        WHEN v.brand = 'Otokar' AND v.model = 'Kent XL'         THEN (2022 + v.id % 3)::smallint
        WHEN v.brand = 'Akia' AND v.model = 'Ultra LF 25'       THEN (2022 + v.id % 3)::smallint
        ELSE (2018 + v.id % 6)::smallint
    END,
    duty_type = CASE
        WHEN g.code = 'ARV' THEN 'Yedek Metrobüs'
        ELSE 'Metrobüs Hat İşletme'
    END
FROM vehicle_types vt, garages g
WHERE v.vehicle_type_id = vt.id
  AND v.garage_id = g.id
  AND vt.name = 'Metrobüs';

-- Hizmet araçlarını beş mantıklı örnek marka/model grubuna dağıtır.
UPDATE vehicles v
SET brand = CASE v.id % 5
        WHEN 0 THEN 'Ford'
        WHEN 1 THEN 'Fiat'
        WHEN 2 THEN 'Renault'
        WHEN 3 THEN 'Toyota'
        ELSE 'Volkswagen'
    END,
    model = CASE v.id % 5
        WHEN 0 THEN 'Transit Courier'
        WHEN 1 THEN 'Doblo Cargo'
        WHEN 2 THEN 'Kangoo'
        WHEN 3 THEN 'Proace City'
        ELSE 'Caddy'
    END,
    model_year = (2018 + v.id % 7)::smallint,
    duty_type = 'Saha Destek'
FROM vehicle_types vt
WHERE v.vehicle_type_id = vt.id
  AND vt.name = 'Hizmet Aracı';

-- Çekicileri ağır ticari örnek marka/model gruplarına dağıtır.
UPDATE vehicles v
SET brand = CASE v.id % 5
        WHEN 0 THEN 'Ford Trucks'
        WHEN 1 THEN 'Mercedes-Benz'
        WHEN 2 THEN 'Iveco'
        WHEN 3 THEN 'BMC'
        ELSE 'MAN'
    END,
    model = CASE v.id % 5
        WHEN 0 THEN 'Cargo 1833D'
        WHEN 1 THEN 'Atego 1824'
        WHEN 2 THEN 'Eurocargo ML180'
        WHEN 3 THEN 'Tuğra 1846'
        ELSE 'TGM 18.290'
    END,
    model_year = (2017 + v.id % 8)::smallint,
    duty_type = 'Araç Kurtarma'
FROM vehicle_types vt
WHERE v.vehicle_type_id = vt.id
  AND vt.name = 'Çekici';

-- Tüm demo araçlara tür, yaş ve kayıt kimliğine göre deterministik kilometre verir.
-- Gerçek ve sıfırdan büyük bir kilometre daha sonra girilmişse üzerine yazmaz.
UPDATE vehicles v
SET current_mileage = CASE vt.name
        WHEN 'Otobüs' THEN
            GREATEST(EXTRACT(YEAR FROM CURRENT_DATE)::integer - v.model_year, 1)
            * (45000 + (v.id % 20001)::integer)
            + (v.id % 12000)::integer
        WHEN 'Metrobüs' THEN
            GREATEST(EXTRACT(YEAR FROM CURRENT_DATE)::integer - v.model_year, 1)
            * (65000 + (v.id % 25001)::integer)
            + (v.id % 15000)::integer
        WHEN 'Hizmet Aracı' THEN
            GREATEST(EXTRACT(YEAR FROM CURRENT_DATE)::integer - v.model_year, 1)
            * (18000 + (v.id % 10001)::integer)
            + (v.id % 5000)::integer
        WHEN 'Çekici' THEN
            GREATEST(EXTRACT(YEAR FROM CURRENT_DATE)::integer - v.model_year, 1)
            * (12000 + (v.id % 8001)::integer)
            + (v.id % 4000)::integer
        ELSE v.current_mileage
    END
FROM vehicle_types vt
WHERE v.vehicle_type_id = vt.id
  AND (v.current_mileage IS NULL OR v.current_mileage <= 0);

-- Yolcu kapasitesi yalnızca yolcu taşıyan araçlarda tutulur.
UPDATE vehicles v
SET capacity = CASE vt.name
        WHEN 'Otobüs' THEN 94
        WHEN 'Metrobüs' THEN 193
        ELSE NULL
    END
FROM vehicle_types vt
WHERE v.vehicle_type_id = vt.id
  AND (
      (vt.name = 'Otobüs' AND v.capacity IS DISTINCT FROM 94)
      OR (vt.name = 'Metrobüs' AND v.capacity IS DISTINCT FROM 193)
      OR (vt.name IN ('Hizmet Aracı', 'Çekici') AND v.capacity IS NOT NULL)
  );

-- Pasif şoförlerin operasyonel durumunu pasif olarak eşitler.
UPDATE drivers
SET availability_status = 'PASSIVE'
WHERE NOT is_active
  AND availability_status <> 'PASSIVE';

-- Uygulamanın görebileceği tek seferlik veri kalite işareti.
INSERT INTO system_settings (
    setting_key,
    setting_value,
    description,
    is_active,
    updated_by_user_id
)
SELECT
    'demo_vehicle_data_quality_v1_applied',
    jsonb_build_object('appliedAt', now(), 'vehicleCount', COUNT(*)),
    'Demo araçların marka, model, üretim yılı, kilometre ve görev verileri zenginleştirildi.',
    true,
    (SELECT id FROM app_users WHERE personnel_number = 'ADM-0001')
FROM vehicles;

-- Tek tek 6.270 log yerine yapılan toplu işlemi tek audit kaydıyla belgeler.
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
    'BULK_DATA_ENRICHMENT',
    'vehicles',
    jsonb_build_object(
        'vehicleCount', (SELECT COUNT(*) FROM vehicles),
        'ruleVersion', 'v1',
        'executedAt', now()
    ),
    'Demo filo verileri mantıklı marka, model, yıl, kilometre ve görev değerleriyle güncellendi.'
FROM app_users u
WHERE u.personnel_number = 'ADM-0001';

COMMIT;

-- Güncelleme sonrası özet kontrol.
SELECT
    vt.name AS vehicle_type,
    COUNT(*) AS total,
    COUNT(*) FILTER (WHERE v.current_mileage <= 0) AS invalid_mileage,
    COUNT(*) FILTER (WHERE v.brand IS NULL OR btrim(v.brand) = '') AS blank_brand,
    COUNT(*) FILTER (WHERE v.model IS NULL OR btrim(v.model) = '') AS blank_model,
    MIN(v.model_year) AS min_year,
    MAX(v.model_year) AS max_year,
    MIN(v.current_mileage) AS min_mileage,
    MAX(v.current_mileage) AS max_mileage
FROM fault_management.vehicles v
JOIN fault_management.vehicle_types vt ON vt.id = v.vehicle_type_id
GROUP BY vt.id, vt.name
ORDER BY vt.name;

SELECT
    vt.name AS vehicle_type,
    v.brand,
    v.model,
    COUNT(*) AS quantity
FROM fault_management.vehicles v
JOIN fault_management.vehicle_types vt ON vt.id = v.vehicle_type_id
GROUP BY vt.name, v.brand, v.model
ORDER BY vt.name, quantity DESC, v.brand, v.model;
-- Demo araçlarda boş veya mantıksız marka, model, yıl, kilometre ve kapasite değerlerini gerçekçi hale getirir.
