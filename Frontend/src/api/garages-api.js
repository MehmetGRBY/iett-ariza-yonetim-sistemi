import { apiRequest } from './api-client.js';

// Garaj liste ve detay ekranlarının REST çağrılarını tek servis altında toplar.
export const garagesApi = {
  // Kullanıcının rol kapsamındaki garajları doluluk ve personel özetleriyle getirir.
  getAll: () => apiRequest('/api/garages'),
  // Seçilen garajın araç tipleri, durumları, yöneticisi ve teknik ekiplerini getirir.
  getById: (id) => apiRequest(`/api/garages/${id}`),
  // Admin garaj adını, adresini ve kapasitesini düzenler.
  update: (id, payload) => apiRequest(`/api/garages/${id}`, { method: 'PUT', body: JSON.stringify(payload) }),
  // Garajı silmeden aktif veya pasif yapar.
  changeActive: (id, payload) => apiRequest(`/api/garages/${id}/active`, { method: 'PUT', body: JSON.stringify(payload) }),
};
