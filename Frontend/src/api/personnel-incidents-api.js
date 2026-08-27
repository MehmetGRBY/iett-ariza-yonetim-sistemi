import { apiRequest } from './api-client.js';

// Personel olayı, görev devri ve sağlık raporu endpointlerini tek servis altında toplar.
export const personnelIncidentsApi = {
  // Kullanıcının rol/garaj kapsamına göre görebildiği bütün personel olaylarını getirir.
  getAll: () => apiRequest('/api/personnel-incidents'),

  // Yeni hastalık, acil durum veya göreve uygun olmama olayını backend'e gönderir.
  create(payload) {
    return apiRequest('/api/personnel-incidents', { method: 'POST', body: JSON.stringify(payload) });
  },

  // Doktor raporu alındığında başlangıç, bitiş ve rapor numarası bilgilerini kaydeder.
  submitReport(id, payload) {
    return apiRequest(`/api/personnel-incidents/${id}/report`, { method: 'PUT', body: JSON.stringify(payload) });
  },

  // Aktif görevdeki sürücüleri belirlemek için bugünün görev ve atama verilerini getirir.
  getTodayTasks: (date) => apiRequest(`/api/tasks?${new URLSearchParams({ date })}`),
};
