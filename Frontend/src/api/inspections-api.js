import { apiRequest } from './api-client.js';

// Araç kontrol kayıtlarının listeleme, araç arama ve ekleme çağrılarını tek noktada toplar.
export const inspectionsApi = {
  getAll: (vehicleId = '') => apiRequest(`/api/decision-support/inspections${vehicleId ? `?vehicleId=${vehicleId}` : ''}`),
  // Tamiri tamamlanmış ve kullanıcı kontrolü bekleyen arızaları getirir.
  getQueue: () => apiRequest('/api/decision-support/inspection-queue'),
  searchVehicles: (search = '') => apiRequest(`/api/decision-support/inspection-vehicles?search=${encodeURIComponent(search)}`),
  create: (payload) => apiRequest('/api/decision-support/inspections', { method: 'POST', body: JSON.stringify(payload) }),
};
