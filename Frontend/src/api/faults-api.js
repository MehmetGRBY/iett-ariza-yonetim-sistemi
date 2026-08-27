import { apiDownload, apiRequest } from './api-client.js';

// Arıza ekranının kullandığı REST endpointlerini arayüz kodundan ayıran servis nesnesidir.
export const faultsApi = {
  // Sayfalama, arama ve durum filtresini backend'in beklediği sorgu parametrelerine çevirir.
  getPage(parameters) {
    const query = new URLSearchParams();

    Object.entries(parameters).forEach(([key, value]) => {
      // Boş bırakılan filtreler sorguya eklenmez ve backend varsayılan davranışını kullanır.
      if (value !== '' && value !== null && value !== undefined) query.set(key, value);
    });

    return apiRequest(`/api/faults?${query}`);
  },

  // Seçilen arızanın müdahale planı, kaynakları, geçmişi ve raporlarıyla detayını getirir.
  getById: (id) => apiRequest(`/api/faults/${id}`),

  // Durum filtresini doldurmak için aktif arıza durumlarını referans endpointinden getirir.
  getStatuses: () => apiRequest('/api/reference-data/fault-statuses'),

  // Merkez personelinin hızlı seçim yapabilmesi için o anda görevi bulunan araçları getirir.
  getActiveTaskVehicles: () => apiRequest('/api/faults/active-task-vehicles'),

  // Kapı numarasına göre araç, aktif görev sürücüsü veya seçilebilir garaj sürücülerini getirir.
  getVehicleContext: (doorNumber) => apiRequest(`/api/faults/vehicle-context/${encodeURIComponent(doorNumber)}`),

  // Arıza formundaki ana ve alt kategori seçimlerini doldurur.
  getCategories: () => apiRequest('/api/reference-data/fault-categories'),

  // Aynı arıza kategorisindeki başarı ve tamir sürelerine göre müsait ekipleri sıralar.
  getTeamRecommendations: (garageId, categoryId) => apiRequest(`/api/faults/team-recommendations?garageId=${garageId}&categoryId=${categoryId}`),

  // Seçilen aracın garajındaki boş çekici, hizmet aracı ve yedek yolcu araçlarını getirir.
  getResourceCandidates: (vehicleId) => apiRequest(`/api/faults/resource-candidates?vehicleId=${vehicleId}`),

  // Form verisini JSON olarak backend'e göndererek yeni arıza kaydı oluşturur.
  create(payload) {
    return apiRequest('/api/faults', { method: 'POST', body: JSON.stringify(payload) });
  },
  // Test sürüşü veya garaj kontrolü gibi aktif görevi olmayan araç arızasını kaydeder.
  createNonTask(payload) {
    return apiRequest('/api/faults/non-task', { method: 'POST', body: JSON.stringify(payload) });
  },

  // Admin veya merkez yetkilisinin açıklamalı durum geçişini backend yaşam döngüsüne gönderir.
  updateStatus(id, payload) {
    return apiRequest(`/api/faults/${id}/status`, { method: 'PUT', body: JSON.stringify(payload) });
  },

  // Garaj yetkilisinin teknik ekip adına hazırladığı tamir raporunu arızaya kaydeder.
  createRepairReport(id, payload) {
    return apiRequest(`/api/faults/${id}/reports`, { method: 'POST', body: JSON.stringify(payload) });
  },

  // Başarısız yerinde müdahale sonrasında merkez tarafından seçilen çekiciyi gönderir.
  dispatchTow(id, towTruckId) {
    return apiRequest(`/api/faults/${id}/dispatch-tow`, {
      method: 'POST', body: JSON.stringify({ towTruckId }),
    });
  },

  // Tamir sonrası kontrolü doğrudan arıza detay ekranından ilgili araç ve arıza ile ilişkilendirir.
  createInspection(payload) {
    return apiRequest('/api/decision-support/inspections', { method: 'POST', body: JSON.stringify(payload) });
  },

  // Teknik rapordaki kök neden seçimini referans verilerinden doldurur.
  getRootCauses: () => apiRequest('/api/reference-data/root-causes'),

  // Arıza oluşturulduktan sonra seçilen fotoğraf veya belge multipart/form-data olarak yüklenir.
  uploadAttachment(id, file) {
    const formData = new FormData();
    formData.append('file', file);
    return apiRequest(`/api/faults/${id}/attachments`, { method: 'POST', body: formData });
  },

  // Yetki korumalı eki tarayıcıda indirilebilecek Blob verisi olarak getirir.
  downloadAttachment: (id) => apiDownload(`/api/fault-attachments/${id}`),

};
