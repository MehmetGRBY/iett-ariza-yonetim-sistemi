import { apiRequest } from './api-client.js';

// Yönetim ekranındaki tanımlı sistem ayarlarının listeleme ve güncelleme çağrılarını kapsar.
export const systemApi = {
  getDatabaseHealth: () => apiRequest('/api/system/database-health'),
  getPageAccess: () => apiRequest('/api/system/page-access'),
  getSettings: () => apiRequest('/api/system/settings'),
  updateSetting: (id, payload) => apiRequest(`/api/system/settings/${id}`, { method: 'PUT', body: JSON.stringify(payload) }),
  // Adminin arıza formunda kullanılacak üst ve alt kategori tanımlarını yönetmesini sağlar.
  getFaultCategories: () => apiRequest('/api/admin/fault-categories'),
  createFaultCategory: (payload) => apiRequest('/api/admin/fault-categories', { method: 'POST', body: JSON.stringify(payload) }),
  updateFaultCategory: (id, payload) => apiRequest(`/api/admin/fault-categories/${id}`, { method: 'PUT', body: JSON.stringify(payload) }),
};
