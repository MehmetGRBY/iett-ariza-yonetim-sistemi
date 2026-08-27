import { appConfig } from '../config/app-config.js';
import { tokenStore } from '../auth/token-store.js';

// Backend'den dönen HTTP hatalarını standart JavaScript Error nesnesinden daha ayrıntılı taşır.
export class ApiError extends Error {
  constructor(message, status = 0, details = null) {
    super(message);
    this.name = 'ApiError';
    // HTTP durum kodu (400, 401, 404 gibi) ekranların hataya göre karar vermesini sağlar.
    this.status = status;
    // Backend'in ProblemDetails veya doğrulama cevabı gerektiğinde incelenmek üzere saklanır.
    this.details = details;
  }
}

// Hata cevabı JSON ise gövdesini güvenli biçimde okumayı dener.
async function readError(response) {
  const contentType = response.headers.get('content-type') ?? '';
  if (!contentType.includes('json')) return null;

  try {
    return await response.json();
  } catch {
    // Bozuk veya boş JSON yüzünden asıl HTTP hatasının kaybolması engellenir.
    return null;
  }
}

// Backend ayrıntılı bir mesaj döndürmediğinde HTTP durum kodunu kullanıcı dostu
// bir Türkçe açıklamaya çevirir. Böylece teknik durum kodları ekrana yansımaz.
function defaultErrorMessage(status) {
  const messages = {
    400: 'Girdiğiniz bilgileri kontrol edip tekrar deneyin.',
    401: 'Oturumunuz geçersiz veya süresi dolmuş. Lütfen yeniden giriş yapın.',
    403: 'Bu işlemi yapmak için yetkiniz bulunmuyor.',
    404: 'Aradığınız kayıt bulunamadı veya artık mevcut değil.',
    409: 'İşlem mevcut kayıtlarla çakıştığı için tamamlanamadı.',
    422: 'Gönderilen bilgiler iş kuralına uygun değil.',
    500: 'Sunucuda beklenmeyen bir hata oluştu. Lütfen daha sonra tekrar deneyin.',
  };

  return messages[status] ?? `İşlem tamamlanamadı (HTTP ${status}).`;
}

// Başarılı cevapta JSON bulunmayabilir. Content-Type ve gövde kontrolü yapmak,
// boş bir 200 cevabının yanlışlıkla hata gibi algılanmasını engeller.
async function readSuccess(response) {
  if (response.status === 204) return null;
  const contentType = response.headers.get('content-type') ?? '';
  if (!contentType.includes('json')) return response.text();

  const text = await response.text();
  return text ? JSON.parse(text) : null;
}

// Tüm Fetch istekleri bu fonksiyondan geçer; adres, JWT ve hata yönetimi tek yerde tutulur.
export async function apiRequest(path, options = {}) {
  const token = tokenStore.getToken();
  const headers = new Headers(options.headers ?? {});

  // Kullanıcı giriş yaptıysa JWT, backend'in doğrulayacağı Bearer başlığına eklenir.
  if (token) headers.set('Authorization', `Bearer ${token}`);

  // FormData kendi sınır bilgisini üretir; diğer gövdeler JSON olarak işaretlenir.
  if (options.body && !(options.body instanceof FormData) && !headers.has('Content-Type')) {
    headers.set('Content-Type', 'application/json');
  }

  let response;
  try {
    // Göreli endpoint, merkezi API adresiyle birleştirilerek gerçek HTTP isteği gönderilir.
    // Operasyon ekranlarındaki araç, görev ve kaynak listeleri sürekli değişir.
    // GET cevaplarının tarayıcı önbelleğinden dönmesi eski görevlerin ekranda
    // kalmasına yol açabileceği için aksi özellikle belirtilmedikçe önbellek kullanılmaz.
    response = await fetch(`${appConfig.apiBaseUrl}${path}`, {
      cache: 'no-store',
      ...options,
      headers,
    });
  } catch {
    // DNS, sertifika, CORS veya kapalı backend gibi ağ hataları kullanıcıya anlaşılır sunulur.
    throw new ApiError('Backend sunucusuna ulaşılamadı. Web API’nin çalıştığını kontrol edin.');
  }

  if (!response.ok) {
    const error = await readError(response);
    // ASP.NET Core doğrulama hatalarındaki alan mesajları tek okunabilir metne çevrilir.
    const validationMessages = error?.errors
      ? Object.values(error.errors).flat().join('\n')
      : null;
    const message = validationMessages ?? error?.detail ?? error?.message ?? error?.title
      ?? defaultErrorMessage(response.status);

    // Token geçersizse yerel oturum temizlenir; login hatasında mevcut giriş formu korunur.
    if (response.status === 401 && path !== '/api/auth/login') tokenStore.clear();
    throw new ApiError(message, response.status, error);
  }

  return readSuccess(response);
}

// JWT gerektiren dosya endpointlerini Blob olarak indirir; JSON bekleyen apiRequest'ten ayrı tutulur.
export async function apiDownload(path) {
  const token = tokenStore.getToken();
  const headers = new Headers();
  if (token) headers.set('Authorization', `Bearer ${token}`);

  let response;
  try {
    response = await fetch(`${appConfig.apiBaseUrl}${path}`, { headers });
  } catch {
    throw new ApiError('Dosya indirilirken backend sunucusuna ulaşılamadı.');
  }

  if (!response.ok) {
    const error = await readError(response);
    throw new ApiError(error?.detail ?? error?.message ?? `Dosya indirilemedi (${response.status}).`, response.status, error);
  }

  return response.blob();
}
