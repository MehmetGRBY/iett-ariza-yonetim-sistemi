import { apiRequest } from './api-client.js';

// Teknik ekip ekranının personel, ekip ve durum değiştirme isteklerini merkezileştirir.
export const techniciansApi = {
  getAll: (garageId = '') => apiRequest(`/api/employees/technicians${garageId ? `?garageId=${garageId}` : ''}`),
  getTeams: (garageId = '') => apiRequest(`/api/employees/technician-teams${garageId ? `?garageId=${garageId}` : ''}`),
  create: (payload) => apiRequest('/api/employees/technicians', { method: 'POST', body: JSON.stringify(payload) }),
  toggleActive: (memberId) => apiRequest(`/api/employees/technicians/${memberId}/active`, { method: 'PUT' }),
};
