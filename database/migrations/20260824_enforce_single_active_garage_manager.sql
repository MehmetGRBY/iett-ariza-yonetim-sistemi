-- Her garajda en fazla bir aktif Garaj Yetkilisi bulunmasını hem veride hem şemada garanti eder.
BEGIN;

SET LOCAL search_path TO fault_management, public;

-- Geçmiş bir uygulama hatasıyla oluşmuş tekrarları temizler; en eski hesap aktif kalır.
WITH ranked_managers AS (
    SELECT u.id,
           row_number() OVER (
               PARTITION BY u.garage_id
               ORDER BY u.created_at, u.id) AS manager_order
      FROM app_users u
      JOIN roles r ON r.id = u.role_id
     WHERE u.is_active
       AND r.name = 'Garaj Yetkilisi'
       AND u.garage_id IS NOT NULL
),
deactivated AS (
    UPDATE app_users u
       SET is_active = false,
           deactivated_at = clock_timestamp(),
           deactivation_reason = 'Aynı garajda birden fazla aktif yetkili bulunduğu için sistem tarafından pasife alındı.',
           failed_login_count = 0,
           locked_until = NULL,
           security_stamp = gen_random_uuid()
      FROM ranked_managers rm
     WHERE u.id = rm.id
       AND rm.manager_order > 1
    RETURNING u.id, u.personnel_number, u.role_id
)
INSERT INTO audit_logs
    (user_id, role_id, action, entity_type, entity_id,
     old_values, new_values, description, created_at)
SELECT admin_user.id,
       admin_user.role_id,
       'DUPLICATE_GARAGE_MANAGER_DEACTIVATED',
       'app_users',
       d.id,
       jsonb_build_object('isActive', true),
       jsonb_build_object('isActive', false, 'personnelNumber', d.personnel_number),
       d.personnel_number || ' hesabı, tek aktif garaj yetkilisi kuralı nedeniyle pasife alındı.',
       clock_timestamp()
  FROM deactivated d
  CROSS JOIN LATERAL (
      SELECT u.id, u.role_id
        FROM app_users u
        JOIN roles r ON r.id = u.role_id
       WHERE u.is_active AND r.name = 'Admin'
       ORDER BY u.id
       LIMIT 1
  ) admin_user;

-- Uygulama kontrolü atlatılsa veya iki istek aynı anda gelse bile PostgreSQL ikinci
-- aktif yetkiliyi reddeder. Rol kimliği kurulumdan dinamik olarak alınır.
DO $$
DECLARE
    garage_manager_role_id bigint;
BEGIN
    SELECT id INTO STRICT garage_manager_role_id
      FROM roles
     WHERE name = 'Garaj Yetkilisi';

    EXECUTE format(
        'CREATE UNIQUE INDEX IF NOT EXISTS uq_app_users_one_active_garage_manager
           ON fault_management.app_users (garage_id)
        WHERE is_active AND role_id = %s AND garage_id IS NOT NULL',
        garage_manager_role_id);
END
$$;

COMMIT;

-- Doğrulama: sonuç dönmemesi her garajda en fazla bir aktif yetkili olduğunu gösterir.
SELECT g.code, g.name, COUNT(*) AS active_manager_count
  FROM fault_management.app_users u
  JOIN fault_management.roles r ON r.id = u.role_id
  JOIN fault_management.garages g ON g.id = u.garage_id
 WHERE u.is_active AND r.name = 'Garaj Yetkilisi'
 GROUP BY g.id, g.code, g.name
HAVING COUNT(*) > 1;

