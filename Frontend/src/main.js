import 'bootstrap/dist/js/bootstrap.bundle.min.js';
import 'bootstrap-icons/font/bootstrap-icons.css';
import 'admin-lte/dist/css/adminlte.min.css';
import 'admin-lte';
import Chart from 'chart.js/auto';
import Swal from 'sweetalert2';
import { authService } from './auth/auth-service.js';
import { dashboardApi } from './api/dashboard-api.js';
import { garagesApi } from './api/garages-api.js';
import { renderNavigation } from './ui/navigation.js';
import './styles/app.css';

// Bu dosya dashboard sayfasının başlangıç noktasıdır; bağımlılıkları yükler ve ekranı hazırlar.
document.documentElement.lang = 'tr';

// Sayılar Türkçe binlik ayırıcılarla (ör. 3.756) gösterilir.
const numberFormatter = new Intl.NumberFormat('tr-TR');
let currentUser = null;
let garageChart = null;
let statusChart = null;

// data-roles özelliğindeki izinlere göre kullanıcının görmemesi gereken menüleri gizler.
function applyRoleMenu(role) {
  document.querySelectorAll('[data-roles]').forEach((element) => {
    const roles = element.dataset.roles.split(',').map((item) => item.trim());
    element.classList.toggle('d-none', !roles.includes(role));
  });
}

// Dashboard kartındaki hedef alanı güvenli biçimde sayısal metinle doldurur.
function setText(selector, value) {
  document.querySelector(selector).textContent = numberFormatter.format(value ?? 0);
}

// Dinamik tablolara textContent kullanarak güvenli bir hücre ekler.
function appendCell(row, text, className = '') {
  const cell = document.createElement('td');
  cell.textContent = text;
  if (className) cell.className = className;
  row.appendChild(cell);
}

// En fazla arızalanan araçları sıralı tabloya dönüştürür.
function renderTopVehicles(items) {
  const body = document.querySelector('#top-vehicles-body');
  body.replaceChildren();

  if (!items.length) {
    // Veri yokken boş tablo yerine kullanıcıya açıklayıcı tek satır gösterilir.
    const row = document.createElement('tr');
    const cell = document.createElement('td');
    cell.colSpan = 3;
    cell.className = 'text-center text-secondary py-4';
    cell.textContent = 'Arıza kaydı bulunmuyor.';
    row.appendChild(cell);
    body.appendChild(row);
    return;
  }

  items.forEach((item, index) => {
    const row = document.createElement('tr');
    appendCell(row, String(index + 1));
    appendCell(row, item.doorNumber);
    appendCell(row, numberFormatter.format(item.count), 'text-end fw-semibold');
    body.appendChild(row);
  });
}

// En çok arıza bildiren sürücülerin ad, sicil ve bildirim sayılarını oluşturur.
function renderTopDrivers(items) {
  const body = document.querySelector('#top-drivers-body');
  body.replaceChildren();

  if (!items.length) {
    const row = document.createElement('tr');
    const cell = document.createElement('td');
    cell.colSpan = 4;
    cell.className = 'text-center text-secondary py-4';
    cell.textContent = 'Sürücü bildirimi bulunmuyor.';
    row.appendChild(cell);
    body.appendChild(row);
    return;
  }

  items.forEach((item, index) => {
    const row = document.createElement('tr');
    appendCell(row, String(index + 1));
    appendCell(row, item.fullName);
    appendCell(row, item.personnelNumber);
    appendCell(row, numberFormatter.format(item.count), 'text-end fw-semibold');
    body.appendChild(row);
  });
}

// En çok personel olayı kaydı bulunan sürücüleri ad, sicil ve olay sayısıyla listeler.
function renderTopPersonnelIncidentDrivers(items) {
  const body = document.querySelector('#top-personnel-incident-drivers-body');
  body.replaceChildren();

  if (!items.length) {
    const row = document.createElement('tr');
    const cell = document.createElement('td');
    cell.colSpan = 4;
    cell.className = 'text-center text-secondary py-4';
    cell.textContent = 'Personel olayı kaydı bulunmuyor.';
    row.appendChild(cell);
    body.appendChild(row);
    return;
  }

  items.forEach((item, index) => {
    const row = document.createElement('tr');
    appendCell(row, String(index + 1));
    appendCell(row, item.fullName);
    appendCell(row, item.personnelNumber);
    appendCell(row, numberFormatter.format(item.count), 'text-end fw-semibold');
    body.appendChild(row);
  });
}

// En sık açılan arıza alt kategorilerini ana kategori bağlamıyla birlikte listeler.
function renderTopFaultCategories(items) {
  const body = document.querySelector('#top-fault-categories-body');
  body.replaceChildren();

  if (!items.length) {
    const row = document.createElement('tr');
    const cell = document.createElement('td');
    cell.colSpan = 4;
    cell.className = 'text-center text-secondary py-4';
    cell.textContent = 'Kategori bazlı arıza kaydı bulunmuyor.';
    row.appendChild(cell);
    body.appendChild(row);
    return;
  }

  items.forEach((item, index) => {
    const row = document.createElement('tr');
    appendCell(row, String(index + 1));
    appendCell(row, item.parentName ?? 'Ana kategori');
    appendCell(row, item.name);
    appendCell(row, numberFormatter.format(item.count), 'text-end fw-semibold');
    body.appendChild(row);
  });
}

// Araç tiplerini büyük kartlara dönüştürmeden kompakt filo rozetleri halinde gösterir.
function renderFleetTypes(items) {
  const container = document.querySelector('#fleet-type-summary');
  container.replaceChildren();
  items.forEach((item) => {
    const badge = document.createElement('span');
    badge.className = 'badge rounded-pill text-bg-secondary fs-6 fw-normal px-3 py-2';
    badge.textContent = `${item.name}: ${numberFormatter.format(item.count)}`;
    container.appendChild(badge);
  });
}

// Backend özetinden bir sütun ve bir halka grafik üretir.
function renderCharts(summary) {
  const garageData = summary.faultsByGarage ?? [];
  // Garaj bazlı arıza sayıları karşılaştırmalı sütun grafik olarak gösterilir.
  garageChart?.destroy();
  garageChart = new Chart(document.querySelector('#garage-fault-chart'), {
    type: 'bar',
    data: {
      labels: garageData.map((item) => item.garage),
      datasets: [{
        label: 'Arıza sayısı',
        data: garageData.map((item) => item.count),
        // Mavi veri rengi genel navigasyonla uyumludur; kırmızı yalnızca arıza uyarısında kullanılır.
        backgroundColor: 'rgba(37, 99, 235, 0.78)',
        borderColor: '#1d4ed8',
        borderWidth: 1,
        borderRadius: 4,
      }],
    },
    options: {
      responsive: true,
      maintainAspectRatio: false,
      plugins: { legend: { display: false } },
      scales: { y: { beginAtZero: true, ticks: { precision: 0 } } },
    },
  });

  // Açık ve kapalı arızaların dağılımı halka grafikle özetlenir.
  const statusData = summary.faultsByStatus ?? [];
  statusChart?.destroy();
  statusChart = new Chart(document.querySelector('#fault-status-chart'), {
    type: 'doughnut',
    data: {
      labels: statusData.map((item) => item.name),
      datasets: [{
        data: statusData.map((item) => item.count),
        backgroundColor: ['#dc3545', '#fd7e14', '#ffc107', '#0dcaf0', '#0d6efd', '#6f42c1', '#20c997', '#198754', '#6c757d'],
        borderWidth: 0,
      }],
    },
    options: {
      responsive: true,
      maintainAspectRatio: false,
      plugins: { legend: { position: 'bottom' } },
    },
  });
}

// API'den gelen dashboard DTO'sunu bütün kart, tablo ve grafik bileşenlerine dağıtır.
function renderDashboard(summary) {
  setText('#total-vehicles', summary.totalVehicles);
  setText('#open-faults', summary.openFaults);
  setText('#repairing-vehicles', summary.repairingVehicles);
  setText('#active-tasks', summary.activeTasks);
  setText('#total-drivers', summary.totalDrivers);
  setText('#total-garages', summary.totalGarages);
  setText('#out-of-service-vehicles', summary.outOfServiceVehicles);
  setText('#waiting-inspection-faults', summary.waitingInspectionFaults);
  setText('#critical-health-vehicles', summary.criticalHealthVehicles);
  setText('#completed-faults', summary.completedFaults);
  setText('#waiting-team-faults', summary.waitingTeamFaults);
  setText('#open-personnel-incidents', summary.openPersonnelIncidents);
  setText('#faults-opened-today', summary.faultsOpenedToday);
  setText('#faults-closed-today', summary.faultsClosedToday);
  setText('#completed-tasks-today', summary.completedTasksToday);
  setText('#available-vehicles', summary.availableVehicles);
  setText('#available-drivers', summary.availableDrivers);
  setText('#available-technician-teams', summary.availableTechnicianTeams);
  renderFleetTypes(summary.fleetByType ?? []);
  renderTopVehicles(summary.topFaultyVehicles ?? []);
  renderTopDrivers(summary.topReportingDrivers ?? []);
  renderTopPersonnelIncidentDrivers(summary.topPersonnelIncidentDrivers ?? []);
  renderTopFaultCategories(summary.topFaultCategories ?? []);
  renderCharts(summary);
}

// Sayfa açıldığında oturumu doğrular, rol menüsünü kurar ve gerçek verileri getirir.
async function initializeDashboard() {
  const loading = document.querySelector('#dashboard-loading');
  const content = document.querySelector('#dashboard-content');
  const errorBox = document.querySelector('#dashboard-error');

  try {
    const user = await authService.requireAuthenticatedUser();
    if (!user) return;

    // Kullanıcının adı, rolü ve varsa bağlı garajı üst menüde gösterilir.
    const garage = user.garageName ? ` · ${user.garageName}` : '';
    document.querySelector('#current-user').textContent = `${user.fullName} · ${user.role}${garage}`;
    applyRoleMenu(user.role);
    renderNavigation('dashboard', user.role);

    currentUser = user;
    // Admin ve merkez bütün aktif garajları filtreleyebilir; garaj yetkilisinin kapsamı sabittir.
    if (user.role === 'Garaj Yetkilisi') {
      document.querySelector('#dashboard-garage-container').classList.add('d-none');
    } else {
      const garages = await garagesApi.getAll();
      const select = document.querySelector('#dashboard-garage');
      garages.forEach((garageItem) => select.appendChild(new Option(`${garageItem.code} · ${garageItem.name}`, garageItem.id)));
    }

    const summary = await dashboardApi.getSummary(readDashboardFilters());
    // Chart.js gizli bir kapsayıcıda doğru ölçü hesaplayamaz. İçerik önce görünür yapılır,
    // ardından grafikler çizilir; böylece Arıza Durumu ilk açılışta da görüntülenir.
    content.classList.remove('d-none');
    renderDashboard(summary);
  } catch (error) {
    // Kimlik veya API hatası dashboard içindeki uyarı alanında gösterilir.
    errorBox.textContent = error.message;
    errorBox.classList.remove('d-none');
  } finally {
    // Sonuç ne olursa olsun yükleniyor göstergesi kapatılır.
    loading.classList.add('d-none');
  }
}

// Dashboard filtre alanlarını API'nin beklediği query parametrelerine dönüştürür.
function readDashboardFilters() {
  return {
    startDate: document.querySelector('#dashboard-start-date').value,
    endDate: document.querySelector('#dashboard-end-date').value,
    garageId: currentUser?.role === 'Garaj Yetkilisi' ? null : document.querySelector('#dashboard-garage').value,
  };
}

// Dashboard varsayılan olarak sistemin açılış tarihinden bugüne kadar olan tüm veriyi gösterir.
// Bitiş tarihi tarayıcının açıldığı güne göre her gün otomatik güncellenir.
const today = new Date();
const systemOpeningDate = new Date(2026, 6, 20);
// UTC dönüşümünün gece saatlerinde tarihi bir gün geriye kaydırmaması için yerel tarih parçaları kullanılır.
const toLocalDateInput = (date) => `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`;
document.querySelector('#dashboard-end-date').value = toLocalDateInput(today);
document.querySelector('#dashboard-start-date').value = toLocalDateInput(systemOpeningDate);

document.querySelector('#dashboard-filter-form').addEventListener('submit', async (event) => {
  event.preventDefault();
  const button = event.submitter;
  button.disabled = true;
  try {
    renderDashboard(await dashboardApi.getSummary(readDashboardFilters()));
  } catch (error) {
    await Swal.fire({ icon: 'error', title: 'Dashboard yenilenemedi', text: error.message });
  } finally {
    button.disabled = false;
  }
});

// Yanlışlıkla çıkışı önlemek için SweetAlert2 ile kullanıcı onayı alınır.
document.querySelector('#logout-button').addEventListener('click', async () => {
  const result = await Swal.fire({
    icon: 'question',
    title: 'Çıkış yapılsın mı?',
    showCancelButton: true,
    confirmButtonText: 'Çıkış Yap',
    cancelButtonText: 'Vazgeç',
    confirmButtonColor: '#b00000',
  });
  if (result.isConfirmed) authService.logout();
});

// Tüm fonksiyonlar tanımlandıktan sonra dashboard yaşam döngüsü başlatılır.
initializeDashboard();
