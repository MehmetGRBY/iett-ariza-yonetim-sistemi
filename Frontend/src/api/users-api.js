import { apiRequest } from './api-client.js';

// Admin kullanıcı yönetimi endpointlerini sayfa kodundan ayırır.
export const usersApi = {
  getAll: () => apiRequest('/api/admin/users'),
  getRoles: () => apiRequest('/api/admin/users/roles'),
  create: (payload) => apiRequest('/api/admin/users', { method: 'POST', body: JSON.stringify(payload) }),
  update: (id, payload) => apiRequest(`/api/admin/users/${id}`, { method: 'PUT', body: JSON.stringify(payload) }),
  toggleActive: (id) => apiRequest(`/api/admin/users/${id}/active`, { method: 'PUT' }),
  unlock: (id) => apiRequest(`/api/admin/users/${id}/unlock`, { method: 'PUT' }),
  resetPassword: (id, newPassword) => apiRequest(`/api/admin/users/${id}/password`, {
    method: 'PUT', body: JSON.stringify({ newPassword }),
  }),
};
