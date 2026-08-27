import { apiRequest } from './api-client.js';

// Oturumdaki kullanıcıya ait uygulama içi bildirim endpointlerini tek yerde toplar.
export const notificationsApi = {
  getAll: (unreadOnly = false) => apiRequest(`/api/notifications?unreadOnly=${unreadOnly}`),
  markAsRead: (id) => apiRequest(`/api/notifications/${id}/read`, { method: 'PUT' }),
  markAllAsRead: () => apiRequest('/api/notifications/read-all', { method: 'PUT' }),
};
