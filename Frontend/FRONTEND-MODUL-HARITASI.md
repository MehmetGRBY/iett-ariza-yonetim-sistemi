# Frontend Modül Haritası

Bu envanter backend controller'ları, servisleri ve PostgreSQL `fault_management` şemasındaki 50 tablo ile 25 view incelenerek hazırlanmıştır.

| Modül | Roller | Backend / Veritabanı durumu | Frontend durumu |
|---|---|---|---|
| Dashboard | Admin, Merkez, Garaj | Hazır | Hazır |
| Araçlar | Admin, Merkez, Garaj | Hazır | Hazır |
| Arızalar ve müdahale | Admin, Merkez, Garaj | Hazır | Hazır |
| Görev ve hat planı | Admin, Merkez, Garaj | Temel liste/detay API hazır | Hazır |
| Personel olayları ve görev devri | Admin, Merkez, Garaj | Hazır | Hazır |
| Garajlar ve doluluk | Admin, Merkez, Garaj | Hazır | Hazır |
| Sürücüler | Admin, Garaj | Hazır | Hazır |
| Teknik ekipler | Admin, Garaj | Hazır | Hazır |
| Araç kontrolleri | Admin, Merkez, Garaj | Hazır; ekleme Admin/Garaj | Hazır |
| SLA takibi | Admin, Merkez, Garaj | `vw_fault_sla_status` ve API hazır | Frontend sırada |
| Tekrarlayan arızalar | Admin, Merkez, Garaj | View ve API hazır | Frontend sırada |
| Araç sağlık skoru | Admin, Merkez, Garaj | View ve API hazır | Frontend sırada |
| Bildirimler | Tüm oturumlu kullanıcılar | Hazır | Frontend sırada |
| Çözüm kütüphanesi | Admin, Merkez, Garaj | Hazır; ekleme Admin/Garaj | Frontend sırada |
| Operasyon olayları | Admin, Merkez, Garaj | Hazır; ekleme Admin/Merkez | Frontend sırada |
| Kullanıcı yönetimi | Admin | Hazır | Frontend sırada |
| Audit logları | Admin | Hazır | Frontend sırada |
| Dosya ekleri | İlgili roller | Hazır | Arıza/rapor detayına entegre edilecek |
| Garajlar arası destek | Belirlenecek | Veritabanı tablosu var, API eksik | Backend gerekli |
| Kaynak rezervasyonları | Belirlenecek | Veritabanı tablosu var, API eksik | Backend gerekli |
| Araç teslimatları | Belirlenecek | Tablo ve view var, API eksik | Backend gerekli |
| Görev transfer yönetimi | Belirlenecek | Tablo ve view var, yönetim API'si eksik | Backend gerekli |
| Sistem ayarları | Admin | Tablo var, API eksik | Backend gerekli |

## Otomatik arka plan servisleri

- Arıza müdahale otomasyonu
- Arıza SLA ve uyarı izleme
- Personel olayı ve rapor dönüş otomasyonu
- Görev durumlarını zamanla senkronize etme
- Her zaman üç günlük görev planı oluşturma

Frontend sayfaları geliştirilirken bu harita sıra ve kapsam kontrolü için kullanılacaktır.
