# İETT Arıza Yönetim Sistemi Web API

ASP.NET Core 8 Web API, Entity Framework Core ve PostgreSQL kullanan bağımsız backend projesidir. MVC uygulamasından ayrıdır ve tamamlanan AdminLTE/Vite frontend uygulamasına JSON tabanlı REST servisleri sunar.

## Çalıştırma

```powershell
dotnet restore .\IettFaultManagement.slnx
dotnet run --project .\IettFaultManagement.Api\IettFaultManagement.Api.csproj
```

Development ortamında Swagger adresi `/swagger`, genel sağlık kontrolü `/health`, veritabanı kontrolü ise `GET /api/system/database-health` yoludur.

## Kimlik doğrulama

`POST /api/auth/login` isteğinden alınan token sonraki isteklerde `Authorization: Bearer {accessToken}` başlığıyla gönderilir. Yalnızca Admin, Merkez Yetkilisi ve Garaj Yetkilisi giriş yapabilir. Garaj yetkilisinin sorguları JWT içindeki `garageId` bilgisiyle sınırlandırılır. Kullanıcı pasife alındığında, hesabı kilitlendiğinde veya parolası değiştirildiğinde eski token geçersiz olur.

## Modüller

- `/api/auth`: giriş, mevcut kullanıcı ve parola değiştirme
- `/api/dashboard`: rol kapsamlı yönetim özeti
- `/api/vehicles`, `/api/garages`, `/api/employees`: filo, garaj ve çalışanlar
- `/api/admin/users`, `/api/admin/audit-logs`: kullanıcı ve denetim işlemleri
- `/api/faults`: arıza, müdahale planı, kaynak atama, durum ve teknik rapor
- `/api/tasks`: hat, görev ve atama bilgileri
- `/api/personnel-incidents`: personel olayı, yedek şoför ve rapor süreci
- `/api/notifications`: kullanıcı bildirimleri
- `/api/monitoring`: tekrar eden arıza ve araç sağlık puanı
- `/api/reference-data`: kategori, durum, araç tipi ve kök neden listeleri
- `/api/decision-support`: çözüm bankası, araç kontrolü ve operasyon olayları
- `/api/fault-attachments`: yetkili arıza eki indirme

## Otomasyonlar

Arıza operasyonunun süreli adımları, FIFO ekip kuyruğu, görev durum senkronizasyonu, üç günlük ileri planlama, personel rapor süresi, operasyon olayı kapanışı ve araç sağlık bildirimleri API içerisinde arka plan servisleri olarak çalışır. Teknik rapor, kontrol ve kaynak seçimi gibi karar adımları yetkili kullanıcı tarafından tamamlanır.

## Yerel geliştirme sırları

Veritabanı parolası ve JWT anahtarı kaynak dosyada tutulmaz. Yerel bilgisayarda bir defa aşağıdaki komutlarla User Secrets alanına kaydedilir:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=iett_fault_management;Username=iett_fault_app;Password=PAROLANIZ" --project .\IettFaultManagement.Api\IettFaultManagement.Api.csproj
dotnet user-secrets set "Jwt:Key" "EN-AZ-32-KARAKTER-GUVENLI-BIR-ANAHTAR" --project .\IettFaultManagement.Api\IettFaultManagement.Api.csproj
```

## Dosya ekleri

- Desteklenen dosyalar: JPG, PNG, WEBP, PDF ve MP4
- En fazla dosya boyutu: 20 MB
- Dosyalar `App_Data/Uploads` altında benzersiz isimle saklanır.
- Orijinal dosya adı yalnızca gösterim amacıyla veritabanında tutulur.

## Testler

```powershell
dotnet test .\IettFaultManagement.slnx
```

Otomatik test projesi arıza müdahale karar tablosunun kritik senaryolarını kontrol eder. Swagger üzerinden gerçek PostgreSQL entegrasyon testleri de gerçekleştirilebilir.
