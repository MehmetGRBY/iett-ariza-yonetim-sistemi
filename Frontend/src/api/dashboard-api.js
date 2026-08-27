import { apiRequest } from './api-client.js';

// Dashboard sayfasının backend endpoint bilgisini arayüz kodundan ayırır.
export const dashboardApi = {
  // Kart, grafik ve sıralama verilerinin tamamını tek HTTP isteğiyle getirir.
  getSummary: (filters = {}) => {
    const query = new URLSearchParams();
    if (filters.startDate) query.set('startDate', filters.startDate);
    if (filters.endDate) query.set('endDate', filters.endDate);
    if (filters.garageId) query.set('garageId', filters.garageId);
    return apiRequest(`/api/dashboard?${query.toString()}`);
  },
};
