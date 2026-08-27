BEGIN;
SET search_path TO fault_management, public;

ALTER TABLE fault_categories
    ADD COLUMN IF NOT EXISTS estimated_repair_minutes integer NOT NULL DEFAULT 60,
    ADD COLUMN IF NOT EXISTS onsite_repair_minutes integer NOT NULL DEFAULT 20,
    ADD COLUMN IF NOT EXISTS auto_repair_result varchar(20) NOT NULL DEFAULT 'RESOLVED';

ALTER TABLE fault_response_plans
    ADD COLUMN IF NOT EXISTS automation_enabled boolean NOT NULL DEFAULT false,
    ADD COLUMN IF NOT EXISTS automation_status varchar(30) NOT NULL DEFAULT 'PENDING',
    ADD COLUMN IF NOT EXISTS next_automation_at timestamp with time zone,
    ADD COLUMN IF NOT EXISTS planned_repair_minutes integer NOT NULL DEFAULT 60,
    ADD COLUMN IF NOT EXISTS planned_repair_result varchar(20) NOT NULL DEFAULT 'RESOLVED',
    ADD COLUMN IF NOT EXISTS repair_started_at timestamp with time zone,
    ADD COLUMN IF NOT EXISTS automation_completed_at timestamp with time zone,
    ADD COLUMN IF NOT EXISTS last_automation_error text;

ALTER TABLE team_members
    ADD COLUMN IF NOT EXISTS work_status varchar(20) NOT NULL DEFAULT 'AVAILABLE';

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_fault_categories_repair_minutes') THEN
        ALTER TABLE fault_categories ADD CONSTRAINT ck_fault_categories_repair_minutes
            CHECK (estimated_repair_minutes BETWEEN 1 AND 720);
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_fault_categories_auto_result') THEN
        ALTER TABLE fault_categories ADD CONSTRAINT ck_fault_categories_auto_result
            CHECK (auto_repair_result IN ('RESOLVED', 'UNRESOLVED'));
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_fault_categories_onsite_minutes') THEN
        ALTER TABLE fault_categories ADD CONSTRAINT ck_fault_categories_onsite_minutes
            CHECK (onsite_repair_minutes BETWEEN 10 AND 45);
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_fault_response_plans_automation_status') THEN
        ALTER TABLE fault_response_plans ADD CONSTRAINT ck_fault_response_plans_automation_status
            CHECK (automation_status IN ('PENDING', 'DISPATCHED', 'REPAIRING', 'COMPLETED', 'FAILED'));
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_fault_response_plans_planned_minutes') THEN
        ALTER TABLE fault_response_plans ADD CONSTRAINT ck_fault_response_plans_planned_minutes
            CHECK (planned_repair_minutes BETWEEN 1 AND 720);
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_fault_response_plans_planned_result') THEN
        ALTER TABLE fault_response_plans ADD CONSTRAINT ck_fault_response_plans_planned_result
            CHECK (planned_repair_result IN ('RESOLVED', 'UNRESOLVED'));
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_team_members_work_status') THEN
        ALTER TABLE team_members ADD CONSTRAINT ck_team_members_work_status
            CHECK (work_status IN ('AVAILABLE', 'ON_DUTY', 'PASSIVE'));
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS ix_fault_response_plans_automation_due
    ON fault_response_plans (automation_status, next_automation_at)
    WHERE is_active AND automation_enabled AND automation_status NOT IN ('COMPLETED', 'FAILED');

-- Varsayılan: küçük arıza, bir saatte başarılı onarım.
UPDATE fault_categories
SET estimated_repair_minutes = 60,
    onsite_repair_minutes = CASE
        WHEN name IN ('Ayna Hasarı', 'Koltuk Arızası', 'Aydınlatma', 'Gösterge Paneli') THEN 10
        WHEN name IN ('Akü', 'Kapı Sensörü Arızası', 'Düşük Hava Basıncı') THEN 15
        ELSE 20
    END,
    auto_repair_result = 'RESOLVED'
WHERE parent_category_id IS NOT NULL;

-- Orta seviye arızalar: 3 saat.
UPDATE fault_categories
SET estimated_repair_minutes = 180
    , onsite_repair_minutes = 30
WHERE name IN
(
    'Lastik Patlaması', 'Jant Hasarı', 'Balata', 'Cam Hasarı',
    'Klima Çalışmıyor', 'Isıtma Çalışmıyor', 'Havalandırma Arızası',
    'Yağ Kaçağı', 'Anormal Ses', 'Kapı Açılmıyor', 'Kapı Kapanmıyor'
);

-- Ağır fakat normal koşullarda onarılabilir arızalar: 6 saat.
UPDATE fault_categories
SET estimated_repair_minutes = 360
    , onsite_repair_minutes = 45
WHERE name IN
(
    'ABS Arızası', 'Hava Basıncı', 'Hidrolik Kaçağı',
    'Amortisör Arızası', 'Araç Yükseklik Arızası', 'Körük Arızası',
    'Vites Geçiş Problemi', 'Şanzıman Yağ Kaçağı', 'Elektrik Tesisatı'
);

-- Kritik arızalar: 12 saat sonunda bu demo akışında çözülemedi kabul edilir.
UPDATE fault_categories
SET estimated_repair_minutes = 720,
    onsite_repair_minutes = 45,
    auto_repair_result = 'UNRESOLVED'
WHERE name IN
(
    'Motor Çalışmıyor', 'Şanzıman Arızası', 'Direksiyon Arızası',
    'Fren Tutmuyor', 'Hararet', 'Kaporta Hasarı'
);

-- Var olan planları kategori ayarlarıyla ilk defa otomasyona hazırla.
UPDATE fault_response_plans plan
SET planned_repair_minutes = category.estimated_repair_minutes,
    planned_repair_result = category.auto_repair_result,
    automation_status = 'PENDING',
    next_automation_at = COALESCE(plan.assessed_at, now()) + interval '5 minutes',
    last_automation_error = NULL,
    automation_enabled = false
FROM faults fault
JOIN fault_categories category ON category.id = fault.fault_category_id
WHERE plan.fault_id = fault.id
  AND plan.is_active
  AND plan.automation_completed_at IS NULL;

UPDATE team_members member
SET work_status = CASE
    WHEN NOT member.is_active THEN 'PASSIVE'
    WHEN EXISTS
    (
        SELECT 1 FROM fault_assignments assignment
        WHERE assignment.team_id = member.team_id
          AND assignment.is_active
          AND assignment.completed_at IS NULL
    ) THEN 'ON_DUTY'
    ELSE 'AVAILABLE'
END;

COMMIT;
-- Arıza müdahale planı, otomatik kaynak ataması ve zaman tabanlı operasyon akışı için gerekli nesneleri ekler.
