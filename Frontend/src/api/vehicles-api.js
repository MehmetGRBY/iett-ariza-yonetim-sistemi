import { apiRequest } from './api-client.js';

// Araç ekranının kullandığı tüm backend çağrıları bu servis nesnesinde toplanır.
export const vehiclesApi = {
  // Sayfalama ve filtre değerlerini URL sorgu parametrelerine dönüştürür.
  getPage(parameters) {
    const query = new URLSearchParams();
    Object.entries(parameters).forEach(([key, value]) => {
      // Boş filtreler URL'ye eklenmez; backend yalnızca gönderilen ölçütleri uygular.
      if (value !== '' && value !== null && value !== undefined) query.set(key, value);
    });
    return apiRequest(`/api/vehicles?${query}`);
  },
  // Seçilen aracın temel bilgileriyle birlikte arıza, garaj ve durum geçmişini getirir.
  getById: (id) => apiRequest(`/api/vehicles/${id}`),
  // Admin araç düzenleme formundaki garaj, tip, yakıt ve durum seçeneklerini getirir.
  getManagementOptions: () => apiRequest('/api/vehicles/management-options'),
  // Kapı numarası haricindeki araç bilgilerini günceller.
  update: (id, payload) => apiRequest(`/api/vehicles/${id}`, { method: 'PUT', body: JSON.stringify(payload) }),
  // Fiziksel kaydı silmeden aracı aktif veya pasif hale getirir.
  changeActive: (id, payload) => apiRequest(`/api/vehicles/${id}/active`, { method: 'PUT', body: JSON.stringify(payload) }),
  // Admin ve merkez yetkilisinin filtre kutusunu doldurmak için aktif garajları getirir.
  getGarages: () => apiRequest('/api/garages'),
};
