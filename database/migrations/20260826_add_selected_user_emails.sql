BEGIN;

SET LOCAL search_path TO fault_management, public;

-- E-posta zorunlu değildir. Yalnızca e-posta bildirimi alacak uygulama
-- kullanıcılarında doldurulur; operasyon personeli kayıtları NULL kalır.
ALTER TABLE app_users
    ADD COLUMN IF NOT EXISTS email varchar(254);

-- Aynı adresin iki farklı aktif kullanıcıya yanlışlıkla verilmesini, büyük-küçük
-- harf farkından bağımsız olarak engeller.
CREATE UNIQUE INDEX IF NOT EXISTS uq_app_users_email_normalized
    ON app_users (lower(email))
    WHERE email IS NOT NULL;

COMMIT;

-- Kişisel e-posta adresleri kaynak kodda tutulmaz. Bildirim alacak kullanıcıların
-- adresleri uygulamanın kullanıcı yönetimi ekranından veya yerel ortamda girilir.
