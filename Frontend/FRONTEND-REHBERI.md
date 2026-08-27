# İETT Arıza Yönetim Sistemi Frontend Rehberi

Bu proje, ASP.NET Core Web API'den bağımsız çalışan Vite tabanlı çok sayfalı bir frontend uygulamasıdır. Frontend doğrudan PostgreSQL veritabanına bağlanmaz; bütün verileri HTTPS ve JSON üzerinden backend API'den alır.

## Çalıştırma komutları

- `npm install`: `package.json` içindeki bağımlılıkları indirerek `node_modules` klasörünü oluşturur.
- `npm run dev`: Vite geliştirme sunucusunu `http://localhost:5173` adresinde başlatır.
- `npm run build`: HTML, CSS ve JavaScript dosyalarını üretim için optimize ederek `dist` klasörüne yazar.
- `npm run preview`: Oluşturulan üretim paketini yerel olarak önizler.

## Kullanılan paketler

- `vite`: Geliştirme sunucusu ve üretim derleyicisidir.
- `admin-lte`: Yönetim panelinin sayfa iskeletini ve bileşenlerini sağlar.
- `bootstrap`: Duyarlı ızgara, form, tablo ve modal bileşenlerini sağlar.
- `bootstrap-icons`: Arayüzde kullanılan simgeleri sağlar.
- `chart.js`: Dashboard grafiklerini oluşturur.
- `sweetalert2`: Başarı, hata ve kullanıcı onay pencerelerini oluşturur.
- `overlayscrollbars`: AdminLTE kenar çubuğunun kaydırma davranışını destekler.

## Kaynak yapısı

- `src/api`: Fetch API çağrılarını ve ortak HTTP hata yönetimini içerir.
- `src/auth`: JWT oturumu, giriş, çıkış ve kullanıcı doğrulama işlemlerini içerir.
- `src/config`: Backend adresi gibi merkezi uygulama ayarlarını içerir.
- `src/pages`: Her HTML sayfasına özel kullanıcı etkileşimi ve ekran oluşturma kodlarını içerir.
- `src/styles`: AdminLTE ve Bootstrap üzerine yazılan projeye özel CSS kurallarını içerir.
- `index.html`: Dashboard sayfasıdır.
- `login.html`: Kullanıcı giriş sayfasıdır.
- `vehicles.html`: Araç listesi, filtreleme, sayfalama ve detay sayfasıdır.
- `faults.html`: Arıza listesi, durum filtresi, müdahale planı, kaynaklar ve süreç geçmişi sayfasıdır.
- `tasks.html`: Tarih ve hatta göre günlük görevleri, araç/sürücü atamasını ve değişiklik geçmişini gösterir.
- `personnel-incidents.html`: Aktif sürücü olayını, yedek sürücüyle görev devrini ve sağlık raporunu yönetir.
- `vite.config.js`: Geliştirme portunu ve çok sayfalı üretim girişlerini tanımlar.

## Yorumlama standardı

Uygulamanın kendi HTML, CSS ve JavaScript kodlarında her önemli bölümün amacı, veri akışı ve backend ilişkisi Türkçe yorumlarla açıklanır. Otomatik üretilen `package-lock.json`, `dist`, `node_modules` ve üçüncü taraf kütüphane dosyaları değiştirilmez. Yeni kod eklenirken aynı açıklama standardı korunur.
