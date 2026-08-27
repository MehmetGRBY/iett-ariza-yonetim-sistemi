// Backend ve PostgreSQL iş kurallarında kullanılan sabit kodları değiştirmeden,
// yalnızca kullanıcı arayüzünde anlaşılır Türkçe karşılıklarla gösterir.
const displayNames = {
  ACTIVE: 'Aktif', INACTIVE: 'Pasif', PASSIVE: 'Pasif', OPEN: 'Açık', CLOSED: 'Kapalı',
  AVAILABLE: 'Müsait', ON_DUTY: 'Görevde', ON_LEAVE: 'İzinli / Raporlu',
  PENDING: 'Bekliyor', SUBMITTED: 'Gönderildi', DISPATCHED: 'Yönlendirildi',
  ARRIVED: 'Ulaştı', COMPLETED: 'Tamamlandı', CANCELLED: 'İptal', CANCELED: 'İptal',
  PLANNED: 'Planlı', IN_PROGRESS: 'Devam ediyor', ON_ROUTE: 'Seferde',
  PASSED: 'Başarılı', FAILED: 'Başarısız', CONDITIONAL: 'Koşullu geçti',
  REPAIRED: 'Tamir edildi', UNRESOLVED: 'Çözülemedi', TEMPORARY_REPAIR: 'Geçici çözüm uygulandı',
  RESOLVED: 'Çözüldü', APPROVED: 'Onaylı', DRAFT: 'Taslak', REJECTED: 'Reddedildi',
  NORMAL: 'Normal', RESERVE: 'Yedek', PRIMARY: 'Asıl atama', ORIGINAL: 'İlk atama',
  REPLACEMENT: 'Yedek atama',
  // Eski sürümün RETURN kayıtları yalnızca geçmiş uyumluluğu için ilk atama
  // şeklinde gösterilir. Backend artık görevi tamir edilen araca geri vermez.
  RETURN: 'İlk atama',
  AUTOMATIC: 'Otomatik', MANUAL: 'Manuel',
  YES: 'Evet', NO: 'Hayır', REQUIRED: 'Gerekli', NOT_REQUIRED: 'Gerekli değil',
  ACTIVE_TASK: 'Aktif görev', NON_ACTIVE_TASK: 'Görev dışı araç', GARAGE: 'Garajda',
  TECHNICIAN_ASSESSMENT: 'Teknisyen değerlendirmesi', ON_SITE_REPAIR: 'Yerinde müdahale',
  ILLNESS: 'Hastalık / Fenalaşma', EMERGENCY: 'Acil durum', UNFIT_FOR_DUTY: 'Göreve uygun değil',
  POST_REPAIR: 'Tamir sonrası', TEST_DRIVE: 'Test sürüşü', RETURN_TO_SERVICE: 'Servise dönüş',
  ROAD_CLOSURE: 'Yol kapanması', ACCIDENT: 'Kaza', WEATHER: 'Olumsuz hava',
  TRAFFIC_DENSITY: 'Trafik yoğunluğu', GARAGE_OPERATION: 'Garaj operasyonu', OTHER: 'Diğer',
  MOVABLE: 'Hareket edebilir', IMMOBILE: 'Hareket edemez',
  TOW_TRUCK: 'Çekici', SERVICE_VEHICLE: 'Hizmet aracı',
  REPLACEMENT_VEHICLE: 'Yedek araç', REPLACEMENT_DRIVER: 'Yedek sürücü',
  ASSIGNED: 'Atandı', EN_ROUTE: 'Yolda', RESOURCE_DEPARTING: 'Kaynaklar hazırlanıyor',
  RESOURCE_EN_ROUTE: 'Kaynaklar yolda', RESOURCE_ARRIVED: 'Kaynaklar ulaştı',
  WAITING_CURRENT_TASK_END: 'Mevcut görevin bitmesi bekleniyor',
  WAITING_TODAYS_TASKS_END: 'Bugünkü görevlerin bitmesi bekleniyor',
  VEHICLE_RETURNING_TO_GARAGE: 'Araç garaja doğru yolda',
  TOW_SELECTION_REQUIRED: 'Çekici seçimi bekleniyor',
  AWAITING_TOW_SELECTION: 'Çekici seçimi bekleniyor',
  ON_SITE_REPAIRED_RETURNING: 'Yerinde tamir edildi, garaja dönüyor',
  VEHICLE_DELIVERED: 'Araç garaja getirildi', WAITING_TEAM: 'Ekip bekliyor',
  ASSIGNED_TO_TEAM: 'Ekibe atandı', WAITING_REPAIR: 'Tamir bekliyor',
  REPAIR_IN_PROGRESS: 'Tamir devam ediyor', WAITING_INSPECTION: 'Kontrol bekliyor',
  SENT_TO_GARAGE: 'Garaja gönderildi', FAULTY: 'Arızalı', UNDER_REPAIR: 'Tamirde',
  WAITING_REPLACEMENT: 'Yedek bekleniyor',
  WAITING: 'Bekliyor', PROCESSING: 'İşleniyor', SENT: 'Gönderildi', RETRY: 'Yeniden denenecek',
  RESERVED: 'Rezerve edildi', FULFILLED: 'Karşılandı', READY: 'Hazır',
  OUT_OF_SERVICE: 'Servis dışı', IN_SERVICE: 'Hizmette', IN_USE: 'Kullanımda',
  TEAM_ASSIGNED: 'Ekip atandı', REPAIRING: 'Tamir ediliyor', READY_TO_CLOSE: 'Kapatılmaya hazır',
  MANUAL_REPAIR_REQUIRED: 'Tamir işlemi bekleniyor',
};

// Bilinen teknik kodu çevirir. Bilinmeyen TAMAMI_BÜYÜK kodların İngilizce olarak
// ekrana sızması yerine güvenli ve Türkçe bir ifade gösterir; normal kullanıcı metnini korur.
export function translateDisplayValue(value, emptyValue = '-') {
  if (value === null || value === undefined || value === '') return emptyValue;
  if (typeof value === 'boolean') return value ? 'Evet' : 'Hayır';
  const text = String(value).trim();
  const code = text.toUpperCase();
  if (displayNames[code]) return displayNames[code];
  // Yeni bir teknik kod sözlüğe eklenmeden gelirse İngilizceyi veya yanıltıcı bir
  // "Tanımsız durum" metnini göstermeyiz. Geliştirici konsolundaki uyarı sayesinde
  // kod sözlüğe eklenebilir; kullanıcı ekranı ise sade kalır.
  if (/^[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)*$/.test(text)) {
    console.warn(`Türkçe gösterim karşılığı eksik: ${text}`);
    return 'Belirtilmemiş';
  }
  return text;
}

// Cümle içinde bulunan teknik kodları da Türkçeleştirir.
// Örnek: "Bağlam: ACTIVE_TASK" -> "Bağlam: Aktif görev".
export function translateDisplayText(value, emptyValue = '-') {
  if (value === null || value === undefined || value === '') return emptyValue;

  let text = String(value);
  const codes = Object.keys(displayNames).sort((left, right) => right.length - left.length);
  for (const code of codes) {
    const escapedCode = code.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
    text = text.replace(new RegExp(`\\b${escapedCode}\\b`, 'g'), displayNames[code]);
  }

  return text;
}

export { displayNames };
