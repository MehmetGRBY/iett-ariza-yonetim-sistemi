BEGIN;

SET search_path TO fault_management, public;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM garages WHERE code = 'ARV' AND is_active) THEN
        RAISE EXCEPTION 'ARV garajı bulunamadı veya pasif.';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM app_users WHERE personnel_number = 'ADM-0001') THEN
        RAISE EXCEPTION 'ADM-0001 kullanıcısı bulunamadı.';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM vehicle_statuses WHERE code = 'AVAILABLE' AND is_active) THEN
        RAISE EXCEPTION 'AVAILABLE araç durumu bulunamadı.';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM vehicle_statuses WHERE code = 'OUT_OF_SERVICE' AND is_active) THEN
        RAISE EXCEPTION 'OUT_OF_SERVICE araç durumu bulunamadı.';
    END IF;

    IF (SELECT COUNT(DISTINCT name) FROM vehicle_types
        WHERE name IN ('Otobüs', 'Metrobüs', 'Hizmet Aracı', 'Çekici') AND is_active) <> 4 THEN
        RAISE EXCEPTION 'Gerekli dört araç tipinin tamamı aktif değil.';
    END IF;

    IF EXISTS (
        SELECT 1 FROM audit_logs
        WHERE action = 'ARV_HALF_DEACTIVATION_COMPLETED'
          AND entity_type = 'Garage'
    ) THEN
        RAISE EXCEPTION 'Bu ARV yarıya indirme işlemi daha önce çalıştırılmış. Tekrar çalıştırılmadı.';
    END IF;
END $$;

-- ARV'ye ilave 90 hizmet aracı: mevcut 001-010 kayıtlarıyla çakışmaması için 011-100.
WITH definitions AS (
    SELECT
        (SELECT id FROM garages WHERE code = 'ARV' AND is_active) AS garage_id,
        (SELECT id FROM vehicle_types WHERE name = 'Hizmet Aracı' AND is_active ORDER BY id LIMIT 1) AS vehicle_type_id,
        (SELECT id FROM fuel_types WHERE name = 'Dizel' AND is_active ORDER BY id LIMIT 1) AS fuel_type_id,
        (SELECT id FROM vehicle_statuses WHERE code = 'AVAILABLE' AND is_active ORDER BY id LIMIT 1) AS status_id
)
INSERT INTO vehicles
(
    door_number, plate, brand, model, model_year,
    vehicle_type_id, fuel_type_id, current_mileage,
    garage_id, vehicle_status_id, duty_type, capacity
)
SELECT
    'HZM-ARV-' || lpad(n::text, 3, '0'),
    '34 HARV ' || lpad(n::text, 6, '0'),
    'Demo', 'Hizmet Aracı', 2020,
    d.vehicle_type_id, d.fuel_type_id, 0,
    d.garage_id, d.status_id, 'Hizmet Aracı', NULL
FROM generate_series(11, 100) n
CROSS JOIN definitions d
ON CONFLICT (door_number) DO NOTHING;

-- ARV'ye ilave 90 çekici.
WITH definitions AS (
    SELECT
        (SELECT id FROM garages WHERE code = 'ARV' AND is_active) AS garage_id,
        (SELECT id FROM vehicle_types WHERE name = 'Çekici' AND is_active ORDER BY id LIMIT 1) AS vehicle_type_id,
        (SELECT id FROM fuel_types WHERE name = 'Dizel' AND is_active ORDER BY id LIMIT 1) AS fuel_type_id,
        (SELECT id FROM vehicle_statuses WHERE code = 'AVAILABLE' AND is_active ORDER BY id LIMIT 1) AS status_id
)
INSERT INTO vehicles
(
    door_number, plate, brand, model, model_year,
    vehicle_type_id, fuel_type_id, current_mileage,
    garage_id, vehicle_status_id, duty_type, capacity
)
SELECT
    'CKC-ARV-' || lpad(n::text, 3, '0'),
    '34 CARV ' || lpad(n::text, 6, '0'),
    'Demo', 'Çekici', 2020,
    d.vehicle_type_id, d.fuel_type_id, 0,
    d.garage_id, d.status_id, 'Çekici', NULL
FROM generate_series(11, 100) n
CROSS JOIN definitions d
ON CONFLICT (door_number) DO NOTHING;

-- ARV'ye 500 yedek metrobüs; dört model eşit olarak dağıtılır (125'er adet).
WITH definitions AS (
    SELECT
        (SELECT id FROM garages WHERE code = 'ARV' AND is_active) AS garage_id,
        (SELECT id FROM vehicle_types WHERE name = 'Metrobüs' AND is_active ORDER BY id LIMIT 1) AS vehicle_type_id,
        (SELECT id FROM fuel_types WHERE name = 'Dizel' AND is_active ORDER BY id LIMIT 1) AS fuel_type_id,
        (SELECT id FROM vehicle_statuses WHERE code = 'AVAILABLE' AND is_active ORDER BY id LIMIT 1) AS status_id
)
INSERT INTO vehicles
(
    door_number, plate, brand, model, model_year,
    vehicle_type_id, fuel_type_id, current_mileage,
    garage_id, vehicle_status_id, duty_type, capacity
)
SELECT
    'MB-ARV-' || lpad(n::text, 4, '0'),
    '34 MARV ' || lpad(n::text, 6, '0'),
    CASE ((n - 1) % 4)
        WHEN 0 THEN 'Mercedes-Benz'
        WHEN 1 THEN 'Mercedes-Benz'
        WHEN 2 THEN 'Otokar'
        ELSE 'Akia'
    END,
    CASE ((n - 1) % 4)
        WHEN 0 THEN 'Capacity'
        WHEN 1 THEN 'Conecto'
        WHEN 2 THEN 'Kent XL'
        ELSE 'Ultra LF 25'
    END,
    2020,
    d.vehicle_type_id, d.fuel_type_id, 0,
    d.garage_id, d.status_id, 'Yedek Metrobüs', 193
FROM generate_series(1, 500) n
CROSS JOIN definitions d
ON CONFLICT (door_number) DO NOTHING;

-- Her araç tipindeki aktif kayıtların yarısı seçilir.
CREATE TEMP TABLE arv_vehicles_to_deactivate
ON COMMIT DROP
AS
WITH ranked AS (
    SELECT
        v.id,
        v.vehicle_status_id AS old_status_id,
        vt.name AS vehicle_type,
        ROW_NUMBER() OVER (PARTITION BY vt.id ORDER BY v.id DESC) AS row_number,
        COUNT(*) OVER (PARTITION BY vt.id) AS type_total
    FROM vehicles v
    JOIN garages g ON g.id = v.garage_id
    JOIN vehicle_types vt ON vt.id = v.vehicle_type_id
    WHERE g.code = 'ARV'
      AND v.is_active = true
      AND vt.name IN ('Otobüs', 'Metrobüs', 'Hizmet Aracı', 'Çekici')
)
SELECT id, old_status_id, vehicle_type
FROM ranked
WHERE row_number <= FLOOR(type_total / 2.0);

UPDATE vehicles v
SET
    is_active = false,
    vehicle_status_id = (
        SELECT id FROM vehicle_statuses WHERE code = 'OUT_OF_SERVICE'
    ),
    deactivated_at = now(),
    deactivation_reason = 'ARV yedek garajındaki araç türü stokunun yarısı pasife alındı.'
FROM arv_vehicles_to_deactivate selected
WHERE v.id = selected.id;

INSERT INTO vehicle_status_histories
(
    vehicle_id, old_status_id, new_status_id,
    changed_by_user_id, changed_at, description
)
SELECT
    selected.id,
    selected.old_status_id,
    status.id,
    admin_user.id,
    now(),
    'ARV yedek garajında araç pasife alınarak servis dışı durumuna geçirildi.'
FROM arv_vehicles_to_deactivate selected
CROSS JOIN (SELECT id FROM vehicle_statuses WHERE code = 'OUT_OF_SERVICE') status
CROSS JOIN (SELECT id FROM app_users WHERE personnel_number = 'ADM-0001') admin_user;

INSERT INTO audit_logs
(
    user_id, role_id, action, entity_type, entity_id,
    old_values, new_values, description, created_at
)
SELECT
    admin_user.id,
    admin_user.role_id,
    'DEACTIVATE',
    'Vehicle',
    selected.id,
    jsonb_build_object('is_active', true, 'vehicle_status_id', selected.old_status_id),
    jsonb_build_object('is_active', false, 'vehicle_status_id', status.id),
    'ARV yedek garajında araç türü stokunun yarısı pasife alındı.',
    now()
FROM arv_vehicles_to_deactivate selected
CROSS JOIN (SELECT id FROM vehicle_statuses WHERE code = 'OUT_OF_SERVICE') status
CROSS JOIN (SELECT id, role_id FROM app_users WHERE personnel_number = 'ADM-0001') admin_user;

-- Dosyanın yanlışlıkla ikinci kez çalıştırılmasını engelleyen işlem işareti.
INSERT INTO audit_logs
(
    user_id, role_id, action, entity_type, entity_id,
    old_values, new_values, description, created_at
)
SELECT
    id,
    role_id,
    'ARV_HALF_DEACTIVATION_COMPLETED',
    'Garage',
    (SELECT id FROM garages WHERE code = 'ARV'),
    NULL,
    jsonb_build_object('completed', true),
    'ARV genişletme ve araç türlerini yarıya indirme işlemi tamamlandı.',
    now()
FROM app_users
WHERE personnel_number = 'ADM-0001';

COMMIT;

-- Son durum: her araç türü için aktif, pasif ve toplam sayıları.
SELECT
    vt.name AS vehicle_type,
    COUNT(v.id) FILTER (WHERE v.is_active) AS active_count,
    COUNT(v.id) FILTER (WHERE NOT v.is_active) AS inactive_count,
    COUNT(v.id) AS total_count
FROM fault_management.vehicle_types vt
LEFT JOIN fault_management.vehicles v
  ON v.vehicle_type_id = vt.id
 AND v.garage_id = (SELECT id FROM fault_management.garages WHERE code = 'ARV')
WHERE vt.name IN ('Otobüs', 'Metrobüs', 'Hizmet Aracı', 'Çekici')
GROUP BY vt.id, vt.name
ORDER BY vt.name;
-- ARV yedek garajını genişletir ve yedek filonun yarısını servis dışı/pasif senaryosu için ayırır.
