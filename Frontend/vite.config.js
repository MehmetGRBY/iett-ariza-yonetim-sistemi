import { resolve } from 'node:path';
import { defineConfig } from 'vite';

// Vite yapılandırması geliştirme sunucusunu ve üretim derlemesini merkezi olarak yönetir.
export default defineConfig({
  server: {
    // Frontend her çalıştırıldığında aynı adresi kullansın; bu adres backend CORS listesinde tanımlıdır.
    port: 5173,
    // 5173 doluysa rastgele başka porta geçmek yerine açık bir hata üretilir.
    strictPort: true,
  },
  build: {
    rollupOptions: {
      // Uygulama çok sayfalı olduğu için her HTML sayfası ayrı bir derleme giriş noktasıdır.
      input: {
        dashboard: resolve(import.meta.dirname, 'index.html'),
        login: resolve(import.meta.dirname, 'login.html'),
        vehicles: resolve(import.meta.dirname, 'vehicles.html'),
        faults: resolve(import.meta.dirname, 'faults.html'),
        tasks: resolve(import.meta.dirname, 'tasks.html'),
        personnelIncidents: resolve(import.meta.dirname, 'personnel-incidents.html'),
        garages: resolve(import.meta.dirname, 'garages.html'),
        drivers: resolve(import.meta.dirname, 'drivers.html'),
        technicians: resolve(import.meta.dirname, 'technicians.html'),
        inspections: resolve(import.meta.dirname, 'inspections.html'),
        monitoring: resolve(import.meta.dirname, 'monitoring.html'),
        users: resolve(import.meta.dirname, 'users.html'),
        auditLogs: resolve(import.meta.dirname, 'audit-logs.html'),
        solutions: resolve(import.meta.dirname, 'solutions.html'),
        operationalEvents: resolve(import.meta.dirname, 'operational-events.html'),
        systemSettings: resolve(import.meta.dirname, 'system-settings.html'),
      },
    },
  },
});
