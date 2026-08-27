BEGIN;

SET search_path TO fault_management, public;

-- Bu seed şu dağılımı kurar:
-- Normal otobüs garajı: 25 NORMAL + 5 RESERVE = 30
-- ARV yedek garajı:     0 NORMAL + 15 RESERVE = 15
-- Metrobüs garajı:     30 NORMAL + 20 RESERVE = 50

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM garages WHERE code = 'ARV' AND is_active) THEN
        RAISE EXCEPTION 'Aktif ARV garajı bulunamadı.';
    END IF;

    IF (SELECT COUNT(*) FROM garages
        WHERE code IN ('BYM', 'CBM', 'ZKM', 'SLM') AND is_active) <> 4 THEN
        RAISE EXCEPTION 'BYM, CBM, ZKM ve SLM metrobüs garajlarının tamamı aktif olmalıdır.';
    END IF;

    IF (SELECT COUNT(*) FROM vehicle_types
        WHERE name IN ('Otobüs', 'Metrobüs', 'Hizmet Aracı', 'Çekici') AND is_active) <> 4 THEN
        RAISE EXCEPTION 'Gerekli dört aktif araç tipi bulunamadı.';
    END IF;
END $$;

CREATE TEMP TABLE desired_driver_groups (
    garage_id      bigint NOT NULL,
    garage_code    varchar(30) NOT NULL,
    driver_type    varchar(20) NOT NULL,
    desired_count  integer NOT NULL
) ON COMMIT DROP;

-- Normal otobüs garajları.
INSERT INTO desired_driver_groups (garage_id, garage_code, driver_type, desired_count)
SELECT id, code, 'NORMAL', 25
FROM garages
WHERE is_active
  AND code <> 'ARV'
  AND code NOT IN ('BYM', 'CBM', 'ZKM', 'SLM');

INSERT INTO desired_driver_groups (garage_id, garage_code, driver_type, desired_count)
SELECT id, code, 'RESERVE', 5
FROM garages
WHERE is_active
  AND code <> 'ARV'
  AND code NOT IN ('BYM', 'CBM', 'ZKM', 'SLM');

-- ARV yedek garajı.
INSERT INTO desired_driver_groups (garage_id, garage_code, driver_type, desired_count)
SELECT id, code, 'RESERVE', 15
FROM garages
WHERE is_active AND code = 'ARV';

-- Metrobüs garajları.
INSERT INTO desired_driver_groups (garage_id, garage_code, driver_type, desired_count)
SELECT id, code, 'NORMAL', 30
FROM garages
WHERE is_active AND code IN ('BYM', 'CBM', 'ZKM', 'SLM');

INSERT INTO desired_driver_groups (garage_id, garage_code, driver_type, desired_count)
SELECT id, code, 'RESERVE', 20
FROM garages
WHERE is_active AND code IN ('BYM', 'CBM', 'ZKM', 'SLM');

-- Mevcut garajsız 30 şoförü boş kontenjanlara dağıtır; kayıtları silmez.
WITH current_counts AS (
    SELECT
        ddg.garage_id,
        ddg.driver_type,
        ddg.desired_count,
        COUNT(d.id) AS current_count
    FROM desired_driver_groups ddg
    LEFT JOIN drivers d
           ON d.garage_id = ddg.garage_id
          AND d.driver_type = ddg.driver_type
          AND d.is_active
    GROUP BY ddg.garage_id, ddg.driver_type, ddg.desired_count
),
available_slots AS (
    SELECT
        cc.garage_id,
        cc.driver_type,
        row_number() OVER (ORDER BY cc.garage_id, cc.driver_type, gs.slot_no) AS row_no
    FROM current_counts cc
    CROSS JOIN LATERAL generate_series(
        cc.current_count::integer + 1,
        cc.desired_count
    ) AS gs(slot_no)
),
unassigned_drivers AS (
    SELECT
        d.id,
        row_number() OVER (ORDER BY d.id) AS row_no
    FROM drivers d
    WHERE d.is_active
      AND d.garage_id IS NULL
),
driver_placements AS (
    SELECT ud.id, s.garage_id, s.driver_type
    FROM unassigned_drivers ud
    JOIN available_slots s USING (row_no)
)
UPDATE drivers d
SET garage_id = dp.garage_id,
    driver_type = dp.driver_type,
    availability_status = 'AVAILABLE'
FROM driver_placements dp
WHERE d.id = dp.id;

-- Dağıtımdan sonra her gruptaki eksik şoförleri üretir.
WITH current_counts AS (
    SELECT
        ddg.garage_id,
        ddg.garage_code,
        ddg.driver_type,
        ddg.desired_count,
        COUNT(d.id)::integer AS current_count
    FROM desired_driver_groups ddg
    LEFT JOIN drivers d
           ON d.garage_id = ddg.garage_id
          AND d.driver_type = ddg.driver_type
          AND d.is_active
    GROUP BY
        ddg.garage_id, ddg.garage_code,
        ddg.driver_type, ddg.desired_count
),
missing_drivers AS (
    SELECT
        cc.garage_id,
        cc.garage_code,
        cc.driver_type,
        gs.sequence_no,
        row_number() OVER (
            ORDER BY cc.garage_code, cc.driver_type, gs.sequence_no
        ) AS global_no
    FROM current_counts cc
    CROSS JOIN LATERAL generate_series(
        cc.current_count + 1,
        cc.desired_count
    ) AS gs(sequence_no)
)
INSERT INTO drivers (
    personnel_number,
    first_name,
    last_name,
    gender_code,
    garage_id,
    driver_type,
    availability_status,
    is_active
)
SELECT
    'DRV-' || md.garage_code || '-' ||
        CASE WHEN md.driver_type = 'NORMAL' THEN 'N-' ELSE 'R-' END ||
        lpad(md.sequence_no::text, 3, '0'),
    (ARRAY[
        'Ahmet', 'Mehmet', 'Mustafa', 'Ali', 'Hasan',
        'Ayşe', 'Fatma', 'Zeynep', 'Elif', 'Merve'
    ])[((md.global_no - 1) % 10) + 1],
    (ARRAY[
        'Yılmaz', 'Kaya', 'Demir', 'Çelik', 'Şahin',
        'Yıldız', 'Aydın', 'Öztürk', 'Arslan', 'Doğan'
    ])[((md.global_no - 1) % 10) + 1],
    CASE WHEN ((md.global_no - 1) % 10) + 1 <= 5 THEN 'MALE' ELSE 'FEMALE' END,
    md.garage_id,
    md.driver_type,
    'AVAILABLE',
    true
FROM missing_drivers md
ON CONFLICT (personnel_number) DO NOTHING;

-- NORMAL şoför yetkileri:
-- Otobüs garajında Otobüs, metrobüs garajında Metrobüs.
INSERT INTO driver_vehicle_type_authorizations (
    driver_id, vehicle_type_id, description
)
SELECT
    d.id,
    vt.id,
    'Normal şoför araç tipi yetkisi'
FROM drivers d
JOIN garages g ON g.id = d.garage_id
JOIN vehicle_types vt
  ON vt.name = CASE
      WHEN g.code IN ('BYM', 'CBM', 'ZKM', 'SLM') THEN 'Metrobüs'
      ELSE 'Otobüs'
  END
WHERE d.is_active
  AND d.driver_type = 'NORMAL'
ON CONFLICT (driver_id, vehicle_type_id) DO UPDATE
SET is_active = true,
    deactivated_at = NULL;

-- Normal garajlardaki yedek şoförler otobüs, hizmet aracı ve çekici kullanabilir.
INSERT INTO driver_vehicle_type_authorizations (
    driver_id, vehicle_type_id, description
)
SELECT
    d.id,
    vt.id,
    'Yedek şoför operasyon araç tipi yetkisi'
FROM drivers d
JOIN garages g ON g.id = d.garage_id
JOIN vehicle_types vt ON vt.name IN ('Otobüs', 'Hizmet Aracı', 'Çekici')
WHERE d.is_active
  AND d.driver_type = 'RESERVE'
  AND g.code <> 'ARV'
  AND g.code NOT IN ('BYM', 'CBM', 'ZKM', 'SLM')
ON CONFLICT (driver_id, vehicle_type_id) DO UPDATE
SET is_active = true,
    deactivated_at = NULL;

-- Metrobüs garajlarındaki yedekler metrobüs, hizmet aracı ve çekici kullanabilir.
INSERT INTO driver_vehicle_type_authorizations (
    driver_id, vehicle_type_id, description
)
SELECT
    d.id,
    vt.id,
    'Metrobüs yedek şoförü araç tipi yetkisi'
FROM drivers d
JOIN garages g ON g.id = d.garage_id
JOIN vehicle_types vt ON vt.name IN ('Metrobüs', 'Hizmet Aracı', 'Çekici')
WHERE d.is_active
  AND d.driver_type = 'RESERVE'
  AND g.code IN ('BYM', 'CBM', 'ZKM', 'SLM')
ON CONFLICT (driver_id, vehicle_type_id) DO UPDATE
SET is_active = true,
    deactivated_at = NULL;

-- ARV yedek şoförleri dört araç tipinin tamamını kullanabilir.
INSERT INTO driver_vehicle_type_authorizations (
    driver_id, vehicle_type_id, description
)
SELECT
    d.id,
    vt.id,
    'ARV çok amaçlı yedek şoför yetkisi'
FROM drivers d
JOIN garages g ON g.id = d.garage_id AND g.code = 'ARV'
JOIN vehicle_types vt ON vt.name IN ('Otobüs', 'Metrobüs', 'Hizmet Aracı', 'Çekici')
WHERE d.is_active
  AND d.driver_type = 'RESERVE'
ON CONFLICT (driver_id, vehicle_type_id) DO UPDATE
SET is_active = true,
    deactivated_at = NULL;

COMMIT;

-- Garaj ve şoför türüne göre dağılım kontrolü.
SELECT
    g.code,
    g.name,
    COUNT(d.id) FILTER (WHERE d.is_active AND d.driver_type = 'NORMAL') AS normal_driver_count,
    COUNT(d.id) FILTER (WHERE d.is_active AND d.driver_type = 'RESERVE') AS reserve_driver_count,
    COUNT(d.id) FILTER (WHERE d.is_active) AS total_active_driver_count
FROM garages g
LEFT JOIN drivers d ON d.garage_id = g.id
WHERE g.is_active
GROUP BY g.id, g.code, g.name
ORDER BY
    CASE
        WHEN g.code = 'ARV' THEN 2
        WHEN g.code IN ('BYM', 'CBM', 'ZKM', 'SLM') THEN 3
        ELSE 1
    END,
    g.code;

-- Genel toplam kontrolü.
SELECT
    COUNT(*) FILTER (WHERE is_active AND driver_type = 'NORMAL') AS normal_driver_total,
    COUNT(*) FILTER (WHERE is_active AND driver_type = 'RESERVE') AS reserve_driver_total,
    COUNT(*) FILTER (WHERE is_active) AS active_driver_total
FROM drivers;
-- Normal, yedek ve metrobüs sürücülerini hedef sayılara göre garajlara dengeli dağıtır.
