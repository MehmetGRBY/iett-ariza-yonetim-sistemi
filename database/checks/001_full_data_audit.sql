SET search_path TO fault_management, public;

-- 1) Genel kayıt sayıları
SELECT *
FROM (
    SELECT 1 AS order_no, 'Araç' AS data_type, COUNT(*) AS record_count FROM vehicles
    UNION ALL SELECT 2, 'Aktif araç', COUNT(*) FROM vehicles WHERE is_active
    UNION ALL SELECT 3, 'Garaj', COUNT(*) FROM garages
    UNION ALL SELECT 4, 'Aktif garaj', COUNT(*) FROM garages WHERE is_active
    UNION ALL SELECT 5, 'Şoför', COUNT(*) FROM drivers
    UNION ALL SELECT 6, 'Kullanıcı', COUNT(*) FROM app_users
    UNION ALL SELECT 7, 'Teknisyen ekibi', COUNT(*) FROM technician_teams
    UNION ALL SELECT 8, 'Aktif ekip üyeliği', COUNT(*) FROM team_members WHERE is_active
    UNION ALL SELECT 9, 'Ana arıza kategorisi', COUNT(*) FROM fault_categories WHERE parent_category_id IS NULL
    UNION ALL SELECT 10, 'Alt arıza kategorisi', COUNT(*) FROM fault_categories WHERE parent_category_id IS NOT NULL
) summary
ORDER BY order_no;

-- 2) Kullanıcıların rol ve cinsiyet dağılımı
SELECT
    r.name AS role_name,
    u.gender_code,
    COUNT(*) AS user_count,
    COUNT(*) FILTER (WHERE u.is_active) AS active_user_count,
    COUNT(*) FILTER (WHERE NOT u.is_active) AS inactive_user_count
FROM app_users u
JOIN roles r ON r.id = u.role_id
GROUP BY r.id, r.name, u.gender_code
ORDER BY r.name, u.gender_code;

-- 3) Şoför dağılımı
SELECT
    gender_code,
    COUNT(*) AS driver_count,
    COUNT(*) FILTER (WHERE is_active) AS active_driver_count
FROM drivers
GROUP BY gender_code
ORDER BY gender_code;

-- 4) Garaj kapasitesi, araç sayısı ve doluluk
SELECT
    g.id,
    g.code,
    g.name,
    g.is_active,
    g.vehicle_capacity,
    COUNT(v.id) FILTER (WHERE v.is_active) AS active_vehicle_count,
    g.vehicle_capacity - COUNT(v.id) FILTER (WHERE v.is_active) AS remaining_capacity,
    ROUND(
        COUNT(v.id) FILTER (WHERE v.is_active) * 100.0
        / NULLIF(g.vehicle_capacity, 0),
        2
    ) AS occupancy_percent,
    CASE
        WHEN g.vehicle_capacity = 0 THEN 'KAPASİTE GİRİLMEMİŞ'
        WHEN COUNT(v.id) FILTER (WHERE v.is_active) > g.vehicle_capacity THEN 'KAPASİTE AŞILDI'
        WHEN COUNT(v.id) FILTER (WHERE v.is_active) = g.vehicle_capacity THEN 'DOLU'
        ELSE 'UYGUN'
    END AS capacity_status
FROM garages g
LEFT JOIN vehicles v ON v.garage_id = g.id
GROUP BY g.id, g.code, g.name, g.is_active, g.vehicle_capacity
ORDER BY g.name;

-- 5) Garaj başına yetkili, teknisyen, ekip ve ekip üyesi
SELECT
    g.code,
    g.name,
    g.is_active,
    COUNT(DISTINCT u.id) FILTER (WHERE r.name = 'Garaj Yetkilisi') AS manager_count,
    COUNT(DISTINCT u.id) FILTER (WHERE r.name = 'Teknisyen') AS technician_count,
    COUNT(DISTINCT t.id) FILTER (WHERE t.is_active) AS active_team_count,
    COUNT(DISTINCT tm.id) FILTER (WHERE tm.is_active) AS active_membership_count,
    CASE
        WHEN NOT g.is_active THEN 'PASİF GARAJ'
        WHEN COUNT(DISTINCT u.id) FILTER (WHERE r.name = 'Garaj Yetkilisi') <> 1 THEN 'YETKİLİ SAYISI HATALI'
        WHEN COUNT(DISTINCT u.id) FILTER (WHERE r.name = 'Teknisyen') <> 6 THEN 'TEKNİSYEN SAYISI HATALI'
        WHEN COUNT(DISTINCT t.id) FILTER (WHERE t.is_active) <> 3 THEN 'EKİP SAYISI HATALI'
        WHEN COUNT(DISTINCT tm.id) FILTER (WHERE tm.is_active) <> 6 THEN 'ÜYELİK SAYISI HATALI'
        ELSE 'UYGUN'
    END AS organization_status
FROM garages g
LEFT JOIN app_users u ON u.garage_id = g.id
LEFT JOIN roles r ON r.id = u.role_id
LEFT JOIN technician_teams t ON t.garage_id = g.id
LEFT JOIN team_members tm ON tm.team_id = t.id
GROUP BY g.id, g.code, g.name, g.is_active
ORDER BY g.name;

-- 6) Kullanıcı-garaj bağlantısı kural ihlalleri
SELECT
    u.personnel_number,
    u.first_name,
    u.last_name,
    r.name AS role_name,
    g.name AS garage_name,
    CASE
        WHEN r.name IN ('Admin', 'Merkez Yetkilisi') AND u.garage_id IS NOT NULL
            THEN 'Bu rol bir garaja bağlı olmamalı'
        WHEN r.name IN ('Garaj Yetkilisi', 'Teknisyen') AND u.garage_id IS NULL
            THEN 'Bu rol bir garaja bağlı olmalı'
        ELSE 'UYGUN'
    END AS validation_result
FROM app_users u
JOIN roles r ON r.id = u.role_id
LEFT JOIN garages g ON g.id = u.garage_id
WHERE
    (r.name IN ('Admin', 'Merkez Yetkilisi') AND u.garage_id IS NOT NULL)
    OR
    (r.name IN ('Garaj Yetkilisi', 'Teknisyen') AND u.garage_id IS NULL)
ORDER BY u.personnel_number;

-- 7) Teknisyen-ekip bağlantısı sorunları
SELECT
    u.personnel_number,
    u.first_name,
    u.last_name,
    g.name AS user_garage,
    t.name AS team_name,
    tg.name AS team_garage,
    CASE
        WHEN tm.id IS NULL THEN 'AKTİF EKİBİ YOK'
        WHEN u.garage_id <> t.garage_id THEN 'KULLANICI VE EKİP GARAJI FARKLI'
        ELSE 'UYGUN'
    END AS validation_result
FROM app_users u
JOIN roles r ON r.id = u.role_id AND r.name = 'Teknisyen'
LEFT JOIN team_members tm ON tm.user_id = u.id AND tm.is_active
LEFT JOIN technician_teams t ON t.id = tm.team_id
LEFT JOIN garages g ON g.id = u.garage_id
LEFT JOIN garages tg ON tg.id = t.garage_id
WHERE tm.id IS NULL OR u.garage_id <> t.garage_id
ORDER BY u.personnel_number;

-- 8) Ekiplerdeki üye ve lider sayıları
SELECT
    g.name AS garage,
    t.name AS team,
    COUNT(tm.id) FILTER (WHERE tm.is_active) AS member_count,
    COUNT(tm.id) FILTER (WHERE tm.is_active AND tm.is_team_leader) AS leader_count,
    CASE
        WHEN COUNT(tm.id) FILTER (WHERE tm.is_active) <> 2 THEN 'ÜYE SAYISI HATALI'
        WHEN COUNT(tm.id) FILTER (WHERE tm.is_active AND tm.is_team_leader) <> 1 THEN 'LİDER SAYISI HATALI'
        ELSE 'UYGUN'
    END AS team_status
FROM technician_teams t
JOIN garages g ON g.id = t.garage_id
LEFT JOIN team_members tm ON tm.team_id = t.id
WHERE t.is_active
GROUP BY g.id, g.name, t.id, t.name
ORDER BY g.name, t.name;

-- 9) Kategori ve alt kategori sayıları
SELECT
    p.name AS main_category,
    COUNT(c.id) AS subcategory_count,
    string_agg(c.name, ', ' ORDER BY c.name) AS subcategories
FROM fault_categories p
LEFT JOIN fault_categories c
    ON c.parent_category_id = p.id AND c.is_active
WHERE p.parent_category_id IS NULL AND p.is_active
GROUP BY p.id, p.name
ORDER BY p.name;

-- 10) Demo araçların model bazında beklenen ve gerçekleşen adetleri
WITH expected(brand, model, expected_count) AS (
    VALUES
        ('Akia', 'LF25', 132),
        ('BMC', 'Procity', 48),
        ('BMC', 'Procity TR', 381),
        ('Cleanvac', 'Emicro', 60),
        ('Green Car', 'LSV 4 Kabinli', 20),
        ('Green Car', 'S 14 Kabinli', 40),
        ('Karsan', 'Avancity CNG', 245),
        ('Karsan', 'Avancity S Plus', 305),
        ('Mercedes-Benz', 'Capacity (Körüklü)', 249),
        ('Mercedes-Benz', 'Citaro 0530 G', 88),
        ('Mercedes-Benz', 'Citaro 0530', 356),
        ('Mercedes-Benz', 'Conecto G', 389),
        ('Mercedes-Benz', 'Conecto', 13),
        ('Otokar', 'Kent 290 LF', 933),
        ('Otokar', 'Kent XL', 120),
        ('Temsa', 'Avenue LF CNG', 107),
        ('SGMS', 'MASTIFF M4', 60),
        ('Akia', 'Ultra LF 12', 150),
        ('Karsan', 'E-JEST', 60)
),
actual AS (
    SELECT brand, model, COUNT(*) AS actual_count
    FROM vehicles
    WHERE door_number LIKE 'DEMO-%'
    GROUP BY brand, model
)
SELECT
    e.brand,
    e.model,
    e.expected_count,
    COALESCE(a.actual_count, 0) AS actual_count,
    COALESCE(a.actual_count, 0) - e.expected_count AS difference,
    CASE
        WHEN COALESCE(a.actual_count, 0) = e.expected_count THEN 'UYGUN'
        ELSE 'ADET FARKI VAR'
    END AS validation_result
FROM expected e
LEFT JOIN actual a ON a.brand = e.brand AND a.model = e.model
ORDER BY e.brand, e.model;

-- 11) Benzersiz olması gereken alanların tekrar kontrolü
SELECT 'vehicles.door_number' AS checked_field, door_number AS duplicate_value, COUNT(*) AS duplicate_count
FROM vehicles GROUP BY door_number HAVING COUNT(*) > 1
UNION ALL
SELECT 'vehicles.plate', plate, COUNT(*) FROM vehicles GROUP BY plate HAVING COUNT(*) > 1
UNION ALL
SELECT 'drivers.personnel_number', personnel_number, COUNT(*) FROM drivers GROUP BY personnel_number HAVING COUNT(*) > 1
UNION ALL
SELECT 'app_users.personnel_number', personnel_number, COUNT(*) FROM app_users GROUP BY personnel_number HAVING COUNT(*) > 1
UNION ALL
SELECT 'garages.code', code, COUNT(*) FROM garages GROUP BY code HAVING COUNT(*) > 1;

-- 12) Sonuç özeti: sorun sayıları sıfır olmalıdır.
WITH garage_load AS (
    SELECT g.id, g.vehicle_capacity, COUNT(v.id) FILTER (WHERE v.is_active) AS vehicle_count
    FROM garages g
    LEFT JOIN vehicles v ON v.garage_id = g.id
    WHERE g.is_active
    GROUP BY g.id, g.vehicle_capacity
),
technician_membership_issues AS (
    SELECT u.id
    FROM app_users u
    JOIN roles r ON r.id = u.role_id AND r.name = 'Teknisyen'
    LEFT JOIN team_members tm ON tm.user_id = u.id AND tm.is_active
    LEFT JOIN technician_teams t ON t.id = tm.team_id
    WHERE tm.id IS NULL OR u.garage_id <> t.garage_id
),
team_issues AS (
    SELECT t.id
    FROM technician_teams t
    LEFT JOIN team_members tm ON tm.team_id = t.id
    WHERE t.is_active
    GROUP BY t.id
    HAVING COUNT(tm.id) FILTER (WHERE tm.is_active) <> 2
       OR COUNT(tm.id) FILTER (WHERE tm.is_active AND tm.is_team_leader) <> 1
)
SELECT 'Kapasitesi aşılan aktif garaj' AS check_name, COUNT(*) AS issue_count
FROM garage_load WHERE vehicle_count > vehicle_capacity
UNION ALL
SELECT 'Ekipsiz/yanlış ekipli teknisyen', COUNT(*) FROM technician_membership_issues
UNION ALL
SELECT 'Üye veya lider sayısı hatalı ekip', COUNT(*) FROM team_issues
UNION ALL
SELECT 'Garajsız aktif araç', COUNT(*) FROM vehicles v LEFT JOIN garages g ON g.id = v.garage_id WHERE v.is_active AND g.id IS NULL;
-- VERİ KALİTE KONTROLÜ: Yetim FK, kapasite, müsaitlik, görev ve arıza tutarlılığı sorunlarını raporlar.
-- Bu script veri değiştirmez; teslim/test öncesi read-only denetim amacıyla çalıştırılır.
