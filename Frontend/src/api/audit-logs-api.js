import { apiRequest } from './api-client.js';

// Audit ekranının sayfalama ve filtre endpointlerini merkezi olarak tanımlar.
export const auditLogsApi = {
  getAll: (filters = {}) => {
    const query = new URLSearchParams();
    Object.entries(filters).forEach(([key, value]) => { if (value !== '' && value != null) query.set(key, value); });
    return apiRequest(`/api/admin/audit-logs?${query.toString()}`);
  },
  getFilters: () => apiRequest('/api/admin/audit-logs/filters'),
};
