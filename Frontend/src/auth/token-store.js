// sessionStorage içinde kullanılacak anahtarlar sabit tutularak yazım farklılıkları engellenir.
const accessTokenKey = 'iett_ays_access_token';
const currentUserKey = 'iett_ays_current_user';

// sessionStorage sekme kapatıldığında temizlenir; ortak bilgisayarda kalıcı oturum bırakmaz.
export const tokenStore = {
  // API isteklerinde kullanılacak JWT değerini döndürür.
  getToken: () => sessionStorage.getItem(accessTokenKey),

  // Başarılı girişten gelen token ve kullanıcı özeti aynı oturumda saklanır.
  setSession(token, user) {
    sessionStorage.setItem(accessTokenKey, token);
    sessionStorage.setItem(currentUserKey, JSON.stringify(user));
  },

  // JSON metni olarak saklanan kullanıcı bilgisi tekrar JavaScript nesnesine çevrilir.
  getUser() {
    const value = sessionStorage.getItem(currentUserKey);
    if (!value) return null;

    try {
      return JSON.parse(value);
    } catch {
      // Depodaki veri bozulmuşsa hatalı oturumla devam etmek yerine tamamı temizlenir.
      this.clear();
      return null;
    }
  },

  // Çıkış veya yetkisiz cevap durumunda tarayıcıdaki bütün oturum verisini siler.
  clear() {
    sessionStorage.removeItem(accessTokenKey);
    sessionStorage.removeItem(currentUserKey);
  },
};
