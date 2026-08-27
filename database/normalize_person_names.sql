BEGIN;

CREATE TEMP TABLE tmp_people ON COMMIT DROP AS
SELECT 'app_users'::text AS source, id, first_name, last_name, gender_code
FROM fault_management.app_users
UNION ALL
SELECT 'drivers'::text, id, first_name, last_name, gender_code
FROM fault_management.drivers;

CREATE TEMP TABLE tmp_name_targets ON COMMIT DROP AS
WITH ranked AS (
    SELECT *, row_number() OVER (
        PARTITION BY lower(trim(first_name)), lower(trim(last_name))
        ORDER BY source, id
    ) AS duplicate_rank
    FROM tmp_people
)
SELECT source, id, gender_code,
       row_number() OVER (PARTITION BY gender_code ORDER BY source, id) AS assignment_no
FROM ranked
WHERE duplicate_rank > 1;

CREATE TEMP TABLE tmp_name_candidates ON COMMIT DROP AS
WITH raw_candidates AS (
    SELECT 'MALE'::text AS gender_code, n.first_name, s.last_name,
           n.name_order, s.surname_order
    FROM unnest(ARRAY[
        'Ahmet','Mehmet','Mustafa','Ali','Hasan','Hüseyin','İbrahim','Ömer','Yusuf','Murat',
        'Emre','Burak','Serkan','Onur','Kaan','Kerem','Tolga','Cem','Can','Eren',
        'Oğuz','Barış','Sinan','Volkan','Uğur','Deniz','Hakan','Selim','Furkan','Gökhan'
    ]) WITH ORDINALITY AS n(first_name, name_order)
    CROSS JOIN unnest(ARRAY[
        'Yılmaz','Kaya','Demir','Şahin','Çelik','Aydın','Yıldız','Arslan','Öztürk','Doğan',
        'Kılıç','Aslan','Çetin','Kara','Koç','Kurt','Özdemir','Erdoğan','Polat','Aksoy',
        'Güneş','Bulut','Taş','Kaplan','Avcı','Tekin','Keskin','Bozkurt','Erdem','Sezer',
        'Acar','Duman','Karaca','Özer','Tunç','Ekinci','Yalçın','Şen','Işık','Sarı'
    ]) WITH ORDINALITY AS s(last_name, surname_order)
    UNION ALL
    SELECT 'FEMALE'::text, n.first_name, s.last_name,
           n.name_order, s.surname_order
    FROM unnest(ARRAY[
        'Ayşe','Fatma','Zeynep','Elif','Merve','Emine','Hatice','Esra','Büşra','Seda',
        'Ceren','Ece','Derya','Gizem','İrem','Selin','Sibel','Pınar','Aylin','Nisa',
        'Melis','Nazlı','Tuğçe','Yasemin','Gül','Özge','Damla','Aslı','Cansu','Sevgi'
    ]) WITH ORDINALITY AS n(first_name, name_order)
    CROSS JOIN unnest(ARRAY[
        'Yılmaz','Kaya','Demir','Şahin','Çelik','Aydın','Yıldız','Arslan','Öztürk','Doğan',
        'Kılıç','Aslan','Çetin','Kara','Koç','Kurt','Özdemir','Erdoğan','Polat','Aksoy',
        'Güneş','Bulut','Taş','Kaplan','Avcı','Tekin','Keskin','Bozkurt','Erdem','Sezer',
        'Acar','Duman','Karaca','Özer','Tunç','Ekinci','Yalçın','Şen','Işık','Sarı'
    ]) WITH ORDINALITY AS s(last_name, surname_order)
), available_candidates AS (
    SELECT candidate.*
    FROM raw_candidates candidate
    WHERE NOT EXISTS (
        SELECT 1 FROM tmp_people person
        WHERE lower(trim(person.first_name)) = lower(candidate.first_name)
          AND lower(trim(person.last_name)) = lower(candidate.last_name)
    )
)
SELECT gender_code, first_name, last_name,
       row_number() OVER (
           PARTITION BY gender_code ORDER BY name_order, surname_order
       ) AS assignment_no
FROM available_candidates;

DO $$
DECLARE insufficient_gender text;
BEGIN
    SELECT target.gender_code INTO insufficient_gender
    FROM (
        SELECT gender_code, count(*) AS needed
        FROM tmp_name_targets GROUP BY gender_code
    ) target
    LEFT JOIN (
        SELECT gender_code, count(*) AS available
        FROM tmp_name_candidates GROUP BY gender_code
    ) candidate USING (gender_code)
    WHERE coalesce(candidate.available, 0) < target.needed
    LIMIT 1;

    IF insufficient_gender IS NOT NULL THEN
        RAISE EXCEPTION 'Yeterli benzersiz isim üretilemedi: %', insufficient_gender;
    END IF;
END $$;

CREATE TEMP TABLE tmp_name_assignments ON COMMIT DROP AS
SELECT target.source, target.id, candidate.first_name, candidate.last_name
FROM tmp_name_targets target
JOIN tmp_name_candidates candidate
  ON candidate.gender_code = target.gender_code
 AND candidate.assignment_no = target.assignment_no;

UPDATE fault_management.app_users user_record
SET first_name = assignment.first_name,
    last_name = assignment.last_name
FROM tmp_name_assignments assignment
WHERE assignment.source = 'app_users' AND assignment.id = user_record.id;

UPDATE fault_management.drivers driver_record
SET first_name = assignment.first_name,
    last_name = assignment.last_name
FROM tmp_name_assignments assignment
WHERE assignment.source = 'drivers' AND assignment.id = driver_record.id;

DO $$
DECLARE remaining_duplicates integer;
BEGIN
    WITH people AS (
        SELECT first_name, last_name FROM fault_management.app_users
        UNION ALL
        SELECT first_name, last_name FROM fault_management.drivers
    )
    SELECT count(*) INTO remaining_duplicates
    FROM (
        SELECT lower(trim(first_name)), lower(trim(last_name))
        FROM people GROUP BY 1, 2 HAVING count(*) > 1
    ) duplicate_names;

    IF remaining_duplicates > 0 THEN
        RAISE EXCEPTION 'İşlem sonrasında % tekrar kaldı.', remaining_duplicates;
    END IF;
END $$;

COMMIT;

WITH people AS (
    SELECT first_name, last_name FROM fault_management.app_users
    UNION ALL
    SELECT first_name, last_name FROM fault_management.drivers
)
SELECT count(*) AS total_people,
       count(DISTINCT lower(trim(first_name)) || '|' || lower(trim(last_name))) AS unique_full_names
FROM people;
