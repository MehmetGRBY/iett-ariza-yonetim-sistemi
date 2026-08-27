BEGIN;

SET search_path TO fault_management, public;

-- 1) 30 şoför: 28 erkek, 2 kadın
INSERT INTO drivers (personnel_number, first_name, last_name, gender_code)
SELECT
    'SFR-' || lpad(n::text, 4, '0'),
    (ARRAY[
        'Ahmet','Mehmet','Mustafa','Ali','Hüseyin','Hasan','İbrahim',
        'Murat','Ömer','Yusuf','Emre','Burak','Serkan','Onur'
    ])[((n - 1) % 14) + 1],
    (ARRAY[
        'Yılmaz','Kaya','Demir','Şahin','Çelik','Yıldız','Aydın',
        'Arslan','Doğan','Kılıç','Aslan','Çetin','Kara','Koç'
    ])[((n - 1) % 14) + 1],
    'MALE'
FROM generate_series(1, 28) AS n
ON CONFLICT (personnel_number) DO NOTHING;

INSERT INTO drivers (personnel_number, first_name, last_name, gender_code)
VALUES
    ('SFR-0029', 'Ayşe', 'Demir', 'FEMALE'),
    ('SFR-0030', 'Zeynep', 'Kaya', 'FEMALE')
ON CONFLICT (personnel_number) DO NOTHING;

-- 2) Ana arıza kategorileri
INSERT INTO fault_categories (name, parent_category_id)
VALUES
    ('Motor', NULL),
    ('Elektrik ve Elektronik', NULL),
    ('Fren Sistemi', NULL),
    ('Şanzıman', NULL),
    ('Kapılar', NULL),
    ('Lastik ve Tekerlek', NULL),
    ('Klima ve Havalandırma', NULL),
    ('Direksiyon', NULL),
    ('Süspansiyon', NULL),
    ('Kaporta ve İç Donanım', NULL)
ON CONFLICT DO NOTHING;

-- 3) Alt kategoriler
INSERT INTO fault_categories (name, parent_category_id)
SELECT x.child_name, p.id
FROM (
    VALUES
        ('Motor', 'Hararet'),
        ('Motor', 'Yağ Kaçağı'),
        ('Motor', 'Motor Çalışmıyor'),
        ('Motor', 'Anormal Ses'),
        ('Elektrik ve Elektronik', 'Akü'),
        ('Elektrik ve Elektronik', 'Aydınlatma'),
        ('Elektrik ve Elektronik', 'Gösterge Paneli'),
        ('Elektrik ve Elektronik', 'Elektrik Tesisatı'),
        ('Fren Sistemi', 'Fren Tutmuyor'),
        ('Fren Sistemi', 'Hava Basıncı'),
        ('Fren Sistemi', 'Balata'),
        ('Fren Sistemi', 'ABS Arızası'),
        ('Şanzıman', 'Vites Geçiş Problemi'),
        ('Şanzıman', 'Şanzıman Yağ Kaçağı'),
        ('Şanzıman', 'Şanzıman Arızası'),
        ('Kapılar', 'Kapı Açılmıyor'),
        ('Kapılar', 'Kapı Kapanmıyor'),
        ('Kapılar', 'Kapı Sensörü Arızası'),
        ('Lastik ve Tekerlek', 'Lastik Patlaması'),
        ('Lastik ve Tekerlek', 'Düşük Hava Basıncı'),
        ('Lastik ve Tekerlek', 'Jant Hasarı'),
        ('Klima ve Havalandırma', 'Klima Çalışmıyor'),
        ('Klima ve Havalandırma', 'Isıtma Çalışmıyor'),
        ('Klima ve Havalandırma', 'Havalandırma Arızası'),
        ('Direksiyon', 'Direksiyon Sertleşmesi'),
        ('Direksiyon', 'Hidrolik Kaçağı'),
        ('Direksiyon', 'Direksiyon Arızası'),
        ('Süspansiyon', 'Körük Arızası'),
        ('Süspansiyon', 'Amortisör Arızası'),
        ('Süspansiyon', 'Araç Yükseklik Arızası'),
        ('Kaporta ve İç Donanım', 'Cam Hasarı'),
        ('Kaporta ve İç Donanım', 'Koltuk Arızası'),
        ('Kaporta ve İç Donanım', 'Ayna Hasarı'),
        ('Kaporta ve İç Donanım', 'Kaporta Hasarı')
) AS x(parent_name, child_name)
JOIN fault_categories p
  ON p.name = x.parent_name AND p.parent_category_id IS NULL
ON CONFLICT DO NOTHING;

-- Aşağıdaki hesaplar SQL ile giriş yapamaz. Parola hash'i .NET Identity ile
-- oluşturulana kadar güvenlik amacıyla pasif durumdadırlar.

-- 4) Bir demo admin hesabı
INSERT INTO app_users
    (personnel_number, first_name, last_name, gender_code, password_hash,
     role_id, garage_id, is_active, deactivated_at, deactivation_reason)
SELECT
    'ADM-0001', 'Sistem', 'Yöneticisi', 'MALE',
    'DEMO_ACCOUNT_NOT_ACTIVATED', r.id, NULL,
    false, now(), 'Şifre .NET Identity üzerinden atanmayı bekliyor.'
FROM roles r
WHERE r.name = 'Admin'
ON CONFLICT (personnel_number) DO NOTHING;

-- 5) 20 merkez yetkilisi: ilk 10 erkek isimli, sonraki 10 kadın isimli
WITH center_role AS (
    SELECT id FROM roles WHERE name = 'Merkez Yetkilisi'
),
male_names AS (
    SELECT ARRAY['Ahmet','Mehmet','Mustafa','Ali','Hüseyin','Hasan','Murat','Ömer','Yusuf','Emre'] AS values
),
female_names AS (
    SELECT ARRAY['Ayşe','Fatma','Zeynep','Elif','Merve','Esra','Büşra','Ece','Selin','Gizem'] AS values
),
last_names AS (
    SELECT ARRAY['Yılmaz','Kaya','Demir','Şahin','Çelik','Yıldız','Aydın','Arslan','Doğan','Kılıç'] AS values
)
INSERT INTO app_users
    (personnel_number, first_name, last_name, gender_code, password_hash,
     role_id, garage_id, is_active, deactivated_at, deactivation_reason)
SELECT
    'MRK-' || lpad(n::text, 3, '0'),
    mn.values[((n - 1) % 10) + 1],
    ln.values[((n * 3 - 1) % 10) + 1],
    'MALE',
    'DEMO_ACCOUNT_NOT_ACTIVATED', cr.id, NULL::bigint,
    false, now(), 'Şifre .NET Identity üzerinden atanmayı bekliyor.'
FROM generate_series(1, 10) n
CROSS JOIN center_role cr CROSS JOIN male_names mn CROSS JOIN last_names ln
UNION ALL
SELECT
    'MRK-' || lpad((n + 10)::text, 3, '0'),
    fn.values[((n - 1) % 10) + 1],
    ln.values[((n * 3 - 1) % 10) + 1],
    'FEMALE',
    'DEMO_ACCOUNT_NOT_ACTIVATED', cr.id, NULL::bigint,
    false, now(), 'Şifre .NET Identity üzerinden atanmayı bekliyor.'
FROM generate_series(1, 10) n
CROSS JOIN center_role cr CROSS JOIN female_names fn CROSS JOIN last_names ln
ON CONFLICT (personnel_number) DO NOTHING;

-- 6) Her aktif garaja bir erkek garaj yetkilisi
WITH garage_manager_role AS (
    SELECT id FROM roles WHERE name = 'Garaj Yetkilisi'
)
INSERT INTO app_users
    (personnel_number, first_name, last_name, gender_code, password_hash,
     role_id, garage_id, is_active, deactivated_at, deactivation_reason)
SELECT
    'GRJ-' || g.code,
    (ARRAY['Murat','Serkan','Onur','Burak','Emre'])[((((row_number() OVER (ORDER BY g.id) - 1) % 5) + 1)::integer)],
    (ARRAY['Yılmaz','Kaya','Demir','Şahin','Çelik'])[((((row_number() OVER (ORDER BY g.id) - 1) % 5) + 1)::integer)],
    'MALE',
    'DEMO_ACCOUNT_NOT_ACTIVATED', r.id, g.id,
    false, now(), 'Şifre .NET Identity üzerinden atanmayı bekliyor.'
FROM garages g
CROSS JOIN garage_manager_role r
WHERE g.is_active = true
ON CONFLICT (personnel_number) DO NOTHING;

-- 7) Her aktif garaja 6 erkek teknisyen
WITH technician_role AS (
    SELECT id FROM roles WHERE name = 'Teknisyen'
)
INSERT INTO app_users
    (personnel_number, first_name, last_name, gender_code, password_hash,
     role_id, garage_id, is_active, deactivated_at, deactivation_reason)
SELECT
    'TKN-' || g.code || '-' || lpad(n::text, 3, '0'),
    (ARRAY['Ahmet','Mehmet','Mustafa','Ali','Hüseyin','Hasan'])[((n - 1) % 6) + 1],
    (ARRAY['Yılmaz','Kaya','Demir','Şahin','Çelik','Aydın'])[((g.id + n - 2) % 6) + 1],
    'MALE',
    'DEMO_ACCOUNT_NOT_ACTIVATED', r.id, g.id,
    false, now(), 'Şifre .NET Identity üzerinden atanmayı bekliyor.'
FROM garages g
CROSS JOIN generate_series(1, 6) n
CROSS JOIN technician_role r
WHERE g.is_active = true
ON CONFLICT (personnel_number) DO NOTHING;

-- 8) Her aktif garaja üç teknisyen ekibi
INSERT INTO technician_teams (name, garage_id, is_available)
SELECT 'Ekip ' || n, g.id, true
FROM garages g
CROSS JOIN generate_series(1, 3) n
WHERE g.is_active = true
ON CONFLICT (garage_id, name) DO NOTHING;

-- 9) Teknisyenleri ekip başına iki kişi olacak şekilde dağıt
WITH numbered_technicians AS (
    SELECT
        u.id AS user_id,
        u.garage_id,
        row_number() OVER (PARTITION BY u.garage_id ORDER BY u.personnel_number) AS technician_no
    FROM app_users u
    JOIN roles r ON r.id = u.role_id
    WHERE r.name = 'Teknisyen'
      AND u.personnel_number LIKE 'TKN-%'
)
INSERT INTO team_members (team_id, user_id, is_team_leader)
SELECT
    t.id,
    nt.user_id,
    ((nt.technician_no - 1) % 2 = 0)
FROM numbered_technicians nt
JOIN technician_teams t
  ON t.garage_id = nt.garage_id
 AND t.name = 'Ekip ' || (((nt.technician_no - 1) / 2) + 1)
WHERE nt.technician_no <= 6
ON CONFLICT DO NOTHING;

COMMIT;

-- Kontrol sonuçları
SELECT gender_code, COUNT(*) AS driver_count
FROM fault_management.drivers
GROUP BY gender_code
ORDER BY gender_code;

SELECT r.name AS role_name, u.gender_code, COUNT(*) AS user_count
FROM fault_management.app_users u
JOIN fault_management.roles r ON r.id = u.role_id
GROUP BY r.name, u.gender_code
ORDER BY r.name, u.gender_code;

SELECT g.name AS garage, COUNT(DISTINCT t.id) AS team_count, COUNT(tm.id) AS member_count
FROM fault_management.garages g
LEFT JOIN fault_management.technician_teams t ON t.garage_id = g.id
LEFT JOIN fault_management.team_members tm ON tm.team_id = t.id AND tm.is_active = true
WHERE g.is_active = true
GROUP BY g.id, g.name
ORDER BY g.name;
-- Demo kullanıcı, sürücü, teknisyen ekibi ve hiyerarşik arıza kategorisi verilerini doldurur.
