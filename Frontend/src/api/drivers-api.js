import { apiRequest } from './api-client.js';

// Sürücü yönetim ekranının liste, detay, ekleme ve aktiflik çağrılarını merkezileştirir.
export const driversApi = {
  getAll(garageId = '') { const query = garageId ? `?garageId=${garageId}` : ''; return apiRequest(`/api/employees/drivers${query}`); },
  getById: (id) => apiRequest(`/api/employees/drivers/${id}`),
  create: (payload) => apiRequest('/api/employees/drivers', { method: 'POST', body: JSON.stringify(payload) }),
  toggleActive: (id) => apiRequest(`/api/employees/drivers/${id}/active`, { method: 'PUT' }),
};
