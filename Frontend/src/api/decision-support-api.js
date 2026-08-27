import { apiRequest } from './api-client.js';

// Karar destek modülündeki çözüm makaleleri ve operasyon olayları için REST çağrılarını toplar.
export const decisionSupportApi = {
  getSolutions: (categoryId = '') => apiRequest(`/api/decision-support/solutions${categoryId ? `?categoryId=${categoryId}` : ''}`),
  createSolution: (payload) => apiRequest('/api/decision-support/solutions', { method: 'POST', body: JSON.stringify(payload) }),
  approveSolution: (id) => apiRequest(`/api/decision-support/solutions/${id}/approve`, { method: 'PUT' }),
  getOperationalEvents: () => apiRequest('/api/decision-support/operational-events'),
  createOperationalEvent: (payload) => apiRequest('/api/decision-support/operational-events', { method: 'POST', body: JSON.stringify(payload) }),
  updateOperationalEvent: (id, payload) => apiRequest(`/api/decision-support/operational-events/${id}`, { method: 'PUT', body: JSON.stringify(payload) }),
};

// Çözüm formundaki kategori ve kök neden seçenekleri merkezi referans endpointlerinden alınır.
export const decisionReferenceApi = {
  getCategories: () => apiRequest('/api/reference-data/fault-categories'),
  getRootCauses: () => apiRequest('/api/reference-data/root-causes'),
};
