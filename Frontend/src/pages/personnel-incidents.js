import 'bootstrap/dist/js/bootstrap.bundle.min.js';
import 'bootstrap-icons/font/bootstrap-icons.css';
import 'admin-lte/dist/css/adminlte.min.css';
import 'admin-lte';
import { Modal } from 'bootstrap';
import Swal from 'sweetalert2';
import { authService } from '../auth/auth-service.js';
import { personnelIncidentsApi } from '../api/personnel-incidents-api.js';
import { renderNavigation } from '../ui/navigation.js';
import { translateDisplayValue } from '../ui/turkish-display.js';
import '../styles/app.css';

// Yeni olay ve sağlık raporu formları birbirinden bağımsız Bootstrap modallarıdır.
const createModal = new Modal(document.querySelector('#incident-create-modal'));
const reportModal = new Modal(document.querySelector('#incident-report-modal'));
const dateFormatter = new Intl.DateTimeFormat('tr-TR', { dateStyle: 'short' });
const dateTimeFormatter = new Intl.DateTimeFormat('tr-TR', { dateStyle: 'short', timeStyle: 'short' });

// Yerel tarih ve tarih-saat alanları için tarayıcının beklediği ISO parçalarını üretir.
function localDateValue(date = new Date()) {
  const local = new Date(date.getTime() - date.getTimezoneOffset() * 60_000);
  return local.toISOString().slice(0, 10);
}

function localDateTimeValue(date = new Date()) {
  const local = new Date(date.getTime() - date.getTimezoneOffset() * 60_000);
  return local.toISOString().slice(0, 16);
}

// Boş veya hatalı tarihlerin arayüzde JavaScript hatası oluşturmasını engeller.
function formatDate(value, includeTime = false) {
  if (!value) return '-';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return '-';
  return includeTime ? dateTimeFormatter.format(date) : dateFormatter.format(date);
}

// Backend olay türü kodlarını personelin anlayacağı Türkçe adlara dönüştürür.
function incidentTypeName(type) {
  return { ILLNESS: 'Hastalık / Fenalaşma', EMERGENCY: 'Acil durum', UNFIT_FOR_DUTY: 'Göreve uygun değil' }[type] ?? translateDisplayValue(type);
}

// Operasyon durumuna hem okunabilir metin hem de semantik Bootstrap rengi verir.
function incidentStatus(status) {
  const values = {
    DISPATCHED: ['Yedek yönlendirildi', 'primary'],
    WAITING_REPLACEMENT: ['Yedek bekleniyor', 'warning'],
    COMPLETED: ['Tamamlandı', 'success'],
    CLOSED: ['Kapatıldı', 'success'],
    CANCELLED: ['İptal', 'secondary'],
  };
  const [text, color] = values[status] ?? [translateDisplayValue(status), 'secondary'];
  return { text, color };
}

// Dinamik tablo içeriği textContent ile güvenli biçimde oluşturulur.
function appendCell(row, value, className = '') {
  const cell = document.createElement('td');
  cell.textContent = value ?? '-';
  if (className) cell.className = className;
  row.appendChild(cell);
  return cell;
}

function createBadge(text, color) {
  const badge = document.createElement('span');
  badge.className = `badge text-bg-${color}`;
  badge.textContent = text;
  return badge;
}

// Liste verilerinden toplam olay, bekleyen işlem ve görev devri sayaçlarını hesaplar.
function renderSummary(items) {
  document.querySelector('#incident-total').textContent = items.length;
  document.querySelector('#incident-report-pending').textContent = items.filter((item) => item.reportStatus === 'PENDING').length;
  document.querySelector('#incident-replacement-pending').textContent = items.filter((item) => item.status === 'WAITING_REPLACEMENT').length;
  document.querySelector('#incident-transferred-tasks').textContent = items.reduce((total, item) => total + (item.transferredTaskCount ?? 0), 0);
}

// Personel olaylarını yedek sürücü, görev devri ve sağlık raporu bilgileriyle tabloya aktarır.
function renderIncidents(items) {
  const body = document.querySelector('#incidents-body');
  body.replaceChildren();

  if (!items.length) {
    const row = document.createElement('tr');
    const cell = document.createElement('td');
    cell.colSpan = 10;
    cell.className = 'text-center text-secondary py-5';
    cell.textContent = 'Personel olayı bulunmuyor.';
    row.appendChild(cell);
    body.appendChild(row);
  }

  items.forEach((incident) => {
    const row = document.createElement('tr');
    appendCell(row, incident.eventNumber, 'fw-semibold text-nowrap');
    appendCell(row, `${incident.driver.fullName} · ${incident.driver.personnelNumber}`);
    appendCell(row, incident.garage);
    appendCell(row, incidentTypeName(incident.eventType));
    const status = incidentStatus(incident.status);
    appendCell(row, '').appendChild(createBadge(status.text, status.color));
    appendCell(row, incident.replacementDriver ? `${incident.replacementDriver.fullName} · ${incident.replacementDriver.personnelNumber}` : 'Atanmadı');
    appendCell(row, `${incident.transferredTaskCount ?? 0} görev`, 'text-nowrap');
    const reportCell = appendCell(row, '');
    reportCell.appendChild(createBadge(incident.reportStatus === 'SUBMITTED' ? 'Rapor girildi' : 'Rapor bekleniyor', incident.reportStatus === 'SUBMITTED' ? 'success' : 'warning'));
    appendCell(row, formatDate(incident.expectedReturnAt), 'text-nowrap');
    const actionCell = appendCell(row, '', 'text-end');

    // Rapor girilmemiş olaylarda sonradan sağlık raporu ekleme düğmesi gösterilir.
    if (incident.reportStatus !== 'SUBMITTED' && incident.status !== 'CANCELLED') {
      const reportButton = document.createElement('button');
      reportButton.type = 'button';
      reportButton.className = 'btn btn-outline-primary btn-sm';
      reportButton.dataset.reportIncidentId = incident.id;
      reportButton.dataset.incidentNumber = incident.eventNumber;
      reportButton.innerHTML = '<i class="bi bi-file-earmark-medical me-1"></i>Rapor Gir';
      actionCell.appendChild(reportButton);
    } else {
      actionCell.textContent = '-';
    }
    body.appendChild(row);
  });

  document.querySelector('#incident-count').textContent = `${items.length} kayıt`;
  renderSummary(items);
}

// Olay listesinin yükleme, başarı ve hata görünüm durumlarını yönetir.
async function loadIncidents() {
  const loading = document.querySelector('#incidents-loading');
  const table = document.querySelector('#incidents-table-container');
  const errorBox = document.querySelector('#incidents-error');
  loading.classList.remove('d-none');
  table.classList.add('d-none');
  errorBox.classList.add('d-none');
  try {
    const items = await personnelIncidentsApi.getAll();
    renderIncidents(items);
    table.classList.remove('d-none');
  } catch (error) {
    errorBox.textContent = error.message;
    errorBox.classList.remove('d-none');
  } finally {
    loading.classList.add('d-none');
  }
}

// Şu anın planlanan görev aralığına düştüğü sürücüleri yeni olay seçim kutusuna ekler.
async function loadActiveDrivers() {
  const tasks = await personnelIncidentsApi.getTodayTasks(localDateValue());
  const now = Date.now();
  const select = document.querySelector('#incident-driver');
  select.replaceChildren(new Option('Sürücü ve görev seçin', ''));
  const seen = new Set();

  tasks.filter((task) => task.assignment && new Date(task.plannedDepartureAt).getTime() <= now && new Date(task.plannedArrivalAt).getTime() >= now)
    .forEach((task) => {
      const driver = task.assignment.driver;
      if (seen.has(driver.id)) return;
      seen.add(driver.id);
      select.appendChild(new Option(`${driver.fullName} · ${driver.personnelNumber} · ${task.route.code} · ${task.taskNumber}`, driver.id));
    });

  if (!seen.size) select.appendChild(new Option('Şu anda aktif görevde sürücü bulunmuyor', '', false, false));
}

// Sayfa açılışında oturum, ortak menü ve olay listesi hazırlanır.
async function initialize() {
  try {
    const user = await authService.requireAuthenticatedUser();
    if (!user) return;
    const garage = user.garageName ? ` · ${user.garageName}` : '';
    document.querySelector('#current-user').textContent = `${user.fullName} · ${user.role}${garage}`;
    renderNavigation('personnel-incidents', user.role);
    await loadIncidents();
  } catch (error) {
    const errorBox = document.querySelector('#incidents-error');
    errorBox.textContent = error.message;
    errorBox.classList.remove('d-none');
    document.querySelector('#incidents-loading').classList.add('d-none');
  }
}

// Yeni olay modalı açılırken aktif sürücü listesi güncel görevlerden yeniden hesaplanır.
document.querySelector('#open-incident-form').addEventListener('click', async () => {
  const errorBox = document.querySelector('#incident-create-error');
  document.querySelector('#incident-create-form').reset();
  document.querySelector('#incident-time').value = localDateTimeValue();
  errorBox.classList.add('d-none');
  createModal.show();
  try { await loadActiveDrivers(); } catch (error) {
    errorBox.textContent = error.message;
    errorBox.classList.remove('d-none');
  }
});

// Olay oluşturulduğunda backend yedek sürücü/hizmet aracı atar ve ileri görevleri devreder.
document.querySelector('#incident-create-form').addEventListener('submit', async (event) => {
  event.preventDefault();
  const button = document.querySelector('#incident-create-button');
  const spinner = document.querySelector('#incident-create-spinner');
  const errorBox = document.querySelector('#incident-create-error');
  button.disabled = true;
  spinner.classList.remove('d-none');
  errorBox.classList.add('d-none');
  try {
    const result = await personnelIncidentsApi.create({
      driverId: Number(document.querySelector('#incident-driver').value),
      eventType: document.querySelector('#incident-type').value,
      description: document.querySelector('#incident-description').value.trim(),
      occurredAt: new Date().toISOString(),
    });
    createModal.hide();
    await loadIncidents();
    await Swal.fire({ icon: 'success', title: 'Personel olayı oluşturuldu', text: `${result.eventNumber} · ${result.transferredTaskCount} görev devredildi`, confirmButtonColor: '#2563eb' });
  } catch (error) {
    errorBox.textContent = error.message;
    errorBox.classList.remove('d-none');
  } finally {
    button.disabled = false;
    spinner.classList.add('d-none');
  }
});

// Dinamik Rapor Gir düğmesi ilgili olay kimliğiyle sağlık raporu modalını açar.
document.querySelector('#incidents-body').addEventListener('click', (event) => {
  const button = event.target.closest('[data-report-incident-id]');
  if (!button) return;
  document.querySelector('#incident-report-form').reset();
  document.querySelector('#incident-report-id').value = button.dataset.reportIncidentId;
  document.querySelector('#incident-report-title').textContent = `${button.dataset.incidentNumber} · Sağlık Raporu`;
  document.querySelector('#report-start-date').value = localDateValue();
  document.querySelector('#report-end-date').value = localDateValue();
  document.querySelector('#incident-report-error').classList.add('d-none');
  reportModal.show();
});

// Sağlık raporu kaydedildiğinde backend personelin dönüş tarihini hesaplar ve görev dışı tutar.
document.querySelector('#incident-report-form').addEventListener('submit', async (event) => {
  event.preventDefault();
  const errorBox = document.querySelector('#incident-report-error');
  errorBox.classList.add('d-none');
  try {
    await personnelIncidentsApi.submitReport(document.querySelector('#incident-report-id').value, {
      reportStartDate: document.querySelector('#report-start-date').value,
      reportEndDate: document.querySelector('#report-end-date').value,
      reportNumber: document.querySelector('#medical-report-number').value.trim() || null,
      notes: document.querySelector('#report-notes').value.trim() || null,
    });
    reportModal.hide();
    await loadIncidents();
    await Swal.fire({ icon: 'success', title: 'Sağlık raporu kaydedildi', confirmButtonColor: '#2563eb' });
  } catch (error) {
    errorBox.textContent = error.message;
    errorBox.classList.remove('d-none');
  }
});

// Oturum kapatılmadan önce kullanıcıdan onay alınır.
document.querySelector('#logout-button').addEventListener('click', async () => {
  const result = await Swal.fire({ icon: 'question', title: 'Çıkış yapılsın mı?', showCancelButton: true, confirmButtonText: 'Çıkış Yap', cancelButtonText: 'Vazgeç', confirmButtonColor: '#2563eb' });
  if (result.isConfirmed) authService.logout();
});

initialize();
