import { apiRequest } from './api-client.js';

// Operasyon izleme ekranının üç rapor endpointini tek servis altında toplar.
export const monitoringApi = {
  getSla: () => apiRequest('/api/monitoring/sla'),
  getRecurringFaults: () => apiRequest('/api/monitoring/recurring-faults'),
  getVehicleHealth: (take = 100) => apiRequest(`/api/monitoring/vehicle-health?take=${take}`),
};
