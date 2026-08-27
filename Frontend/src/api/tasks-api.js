import { apiRequest } from './api-client.js';

// Görev ve hat planı ekranının bütün REST çağrılarını tek servis altında toplar.
export const tasksApi = {
  // Seçilen tarih ve varsa hat kimliğini sorgu parametrelerine dönüştürerek görevleri getirir.
  getByDate(date, routeId = '') {
    const query = new URLSearchParams({ date });
    if (routeId) query.set('routeId', routeId);
    return apiRequest(`/api/tasks?${query}`);
  },

  // Görevin plan bilgileriyle birlikte eski ve aktif bütün atamalarını getirir.
  getById: (id) => apiRequest(`/api/tasks/${id}`),

  // Tarih filtresinin yanında kullanılacak aktif hat listesini getirir.
  getRoutes: () => apiRequest('/api/tasks/routes'),
};
