BEGIN;

SET search_path TO fault_management, public;

DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM system_settings
        WHERE setting_key = 'demo_vehicle_identifiers_v1_applied'
    ) THEN
        RAISE EXCEPTION 'Demo araç kimlik normalizasyonu daha önce uygulanmış.';
    END IF;
END $$;

-- Otobüs kapı numaralarındaki DEMO yer tutucusunu kaldırır.
WITH numbered_buses AS (
    SELECT
        v.id,
        row_number() OVER (ORDER BY v.id) AS sequence_no
    FROM vehicles v
    JOIN vehicle_types vt ON vt.id = v.vehicle_type_id
    WHERE vt.name = 'Otobüs'
)
UPDATE vehicles v
SET door_number = 'OTB-' || lpad(nb.sequence_no::text, 6, '0')
FROM numbered_buses nb
WHERE v.id = nb.id;

-- 6.270 aracın tamamına benzersiz ve tutarlı örnek İstanbul plakası verir.
-- Örnek sıra: 34 AAA 001 ... 34 AAG 276
WITH numbered_vehicles AS (
    SELECT
        v.id,
        row_number() OVER (ORDER BY v.id) AS sequence_no
    FROM vehicles v
),
generated_plates AS (
    SELECT
        nv.id,
        '34 ' ||
        chr(65 + ((((nv.sequence_no - 1) / 999) / 676) % 26)::integer) ||
        chr(65 + ((((nv.sequence_no - 1) / 999) / 26) % 26)::integer) ||
        chr(65 + (((nv.sequence_no - 1) / 999) % 26)::integer) ||
        ' ' ||
        lpad((((nv.sequence_no - 1) % 999) + 1)::text, 3, '0') AS new_plate
    FROM numbered_vehicles nv
)
UPDATE vehicles v
SET plate = gp.new_plate
FROM generated_plates gp
WHERE v.id = gp.id;

INSERT INTO system_settings (
    setting_key,
    setting_value,
    description,
    is_active,
    updated_by_user_id
)
VALUES (
    'demo_vehicle_identifiers_v1_applied',
    jsonb_build_object('appliedAt', now(), 'format', '34 AAA 001'),
    'Demo kapı numarası yer tutucuları kaldırıldı ve araç plakaları normalize edildi.',
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
    'BULK_IDENTIFIER_NORMALIZATION',
    'vehicles',
    jsonb_build_object(
        'vehicleCount', (SELECT COUNT(*) FROM vehicles),
        'busDoorNumberFormat', 'OTB-000001',
        'plateFormat', '34 AAA 001',
        'executedAt', now()
    ),
    'Demo araç kapı numaraları ve plakaları benzersiz, tutarlı biçime getirildi.'
FROM app_users u
WHERE u.personnel_number = 'ADM-0001';

COMMIT;

-- Son kontrol: dört değer de 0 olmalıdır.
SELECT
    COUNT(*) FILTER (WHERE door_number ILIKE '%DEMO%') AS demo_door_count,
    COUNT(*) FILTER (WHERE plate ILIKE '%DEMO%') AS demo_plate_count,
    COUNT(*) - COUNT(DISTINCT door_number) AS duplicate_door_count,
    COUNT(*) - COUNT(DISTINCT plate) AS duplicate_plate_count
FROM fault_management.vehicles;

SELECT
    vt.name AS vehicle_type,
    MIN(v.door_number) AS first_door_number,
    MIN(v.plate) AS first_plate,
    COUNT(*) AS quantity
FROM fault_management.vehicles v
JOIN fault_management.vehicle_types vt ON vt.id = v.vehicle_type_id
GROUP BY vt.id, vt.name
ORDER BY vt.name;
-- Demo kapı numarası ve plakalarını benzersiz, tutarlı ve aranabilir bir formata dönüştürür.
