// Ortamdan bağımsız uygulama ayarları tek yerde tutulur. API adresi değişirse diğer dosyalar düzenlenmez.
export const appConfig = Object.freeze({
  // `dotnet run` varsayılan HTTP profili 5043 portunu kullanır; HTTP tercih edilerek yerel sertifika hataları da önlenir.
  apiBaseUrl: 'http://localhost:5043',
  // Başlık ve ileride bildirim gibi alanlarda kullanılabilecek ortak uygulama adıdır.
  appName: 'İETT Arıza Yönetim Sistemi',
});
