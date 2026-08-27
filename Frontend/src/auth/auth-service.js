import { apiRequest } from '../api/api-client.js';
import { tokenStore } from './token-store.js';
import { applicationModules } from '../ui/navigation.js';

// Kimlik doğrulamayla ilgili işlemleri sayfalardan ayıran servis katmanıdır.
export const authService = {
  // Sicil ve parola backend'e gönderilir; başarılı sonuç tarayıcı oturumuna kaydedilir.
  async login(personnelNumber, password) {
    const result = await apiRequest('/api/auth/login', {
      method: 'POST',
      body: JSON.stringify({ personnelNumber, password }),
    });
    tokenStore.setSession(result.accessToken, result.user);
    return result.user;
  },

  // Adminin önceden tanımladığı sicil için personelin ilk parolasını oluşturur.
  async activateAccount(personnelNumber, newPassword, confirmPassword) {
    return apiRequest('/api/auth/activate', {
      method: 'POST',
      body: JSON.stringify({ personnelNumber, newPassword, confirmPassword }),
    });
  },

  // Kullanıcı oturum açmadan sicil ve mevcut parolasıyla yeni parola belirleyebilir.
  async changePasswordFromLogin(personnelNumber, currentPassword, newPassword, confirmPassword) {
    return apiRequest('/api/auth/change-password', {
      method: 'PUT',
      body: JSON.stringify({ personnelNumber, currentPassword, newPassword, confirmPassword }),
    });
  },

  // Token'ın hâlâ geçerli olduğunu backend üzerinden doğrular ve güncel kullanıcıyı getirir.
  async getCurrentUser() {
    const user = await apiRequest('/api/auth/me');
    const access = await apiRequest('/api/system/page-access');
    const enrichedUser = { ...user, allowedPages: access.allowedPages ?? ['dashboard'] };
    tokenStore.setSession(tokenStore.getToken(), enrichedUser);
    return enrichedUser;
  },

  // Senkron ekran ihtiyaçları için daha önce saklanan kullanıcı ve token bilgisi okunur.
  getStoredUser: () => tokenStore.getUser(),
  isAuthenticated: () => Boolean(tokenStore.getToken()),

  // Yerel oturum temizlenir ve kullanıcı giriş sayfasına yönlendirilir.
  logout() {
    tokenStore.clear();
    window.location.replace('/login.html');
  },

  // Korunan her sayfa açılırken çağrılır; giriş yoksa içeriğin görüntülenmesini engeller.
  async requireAuthenticatedUser() {
    if (!this.isAuthenticated()) {
      window.location.replace('/login.html');
      return null;
    }

    try {
      const user = await this.getCurrentUser();
      const fileName = window.location.pathname.split('/').pop() || 'index.html';
      const currentModule = applicationModules.find((module) => module.href.endsWith(fileName));
      if (currentModule && !user.allowedPages.includes(currentModule.key)) {
        window.location.replace('/index.html?accessDenied=1');
        return null;
      }
      return user;
    } catch (error) {
      // 401 giriş yapılmadığını, 403 ise erişim yetkisinin bulunmadığını ifade eder.
      if (error.status === 401 || error.status === 403) this.logout();
      throw error;
    }
  },
};
