import 'bootstrap/dist/js/bootstrap.bundle.min.js';
import 'bootstrap-icons/font/bootstrap-icons.css';
import 'admin-lte/dist/css/adminlte.min.css';
import 'admin-lte';
import { Modal } from 'bootstrap';
import Swal from 'sweetalert2';
import { authService } from '../auth/auth-service.js';
import { tasksApi } from '../api/tasks-api.js';
import { renderNavigation } from '../ui/navigation.js';
import { translateDisplayValue } from '../ui/turkish-display.js';
import '../styles/app.css';

// Modal örneği bir kez oluşturulur ve bütün görev detaylarında tekrar kullanılır.
const detailModal = new Modal(document.querySelector('#task-detail-modal'));
const dateTimeFormatter = new Intl.DateTimeFormat('tr-TR', { dateStyle: 'short', timeStyle: 'short' });
const timeFormatter = new Intl.DateTimeFormat('tr-TR', { hour: '2-digit', minute: '2-digit' });

// Tarih filtresine kullanıcının yerel takvim gününü YYYY-MM-DD biçiminde yazar.
function localDateValue(date = new Date()) {
  const local = new Date(date.getTime() - date.getTimezoneOffset() * 60_000);
  return local.toISOString().slice(0, 10);
}

// API tarih değerini güvenli biçimde Türkiye tarih-saat formatına dönüştürür.
function formatDateTime(value) {
  if (!value) return '-';
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? '-' : dateTimeFormatter.format(date);
}

// Görev listesinde yalnızca saat bilgisi gerektiğinde kısa biçim kullanılır.
function formatTime(value) {
  if (!value) return '-';
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? '-' : timeFormatter.format(date);
}

// Backend durum kodlarını personelin okuyacağı metin ve semantik renge dönüştürür.
function taskStatus(status) {
  const value = (status ?? '').toUpperCase();
  if (['COMPLETED', 'TAMAMLANDI'].includes(value)) return { text: 'Tamamlandı', color: 'success', group: 'completed' };
  if (['ACTIVE', 'IN_PROGRESS', 'ON_ROUTE', 'SEFERDE'].includes(value)) return { text: 'Aktif / Seferde', color: 'warning', group: 'active' };
  if (['CANCELLED', 'CANCELED', 'İPTAL'].includes(value)) return { text: 'İptal', color: 'secondary', group: 'other' };
  return { text: 'Planlı', color: 'primary', group: 'planned' };
}

// Güvenli DOM üretimi için tabloya düz metin hücresi ekler.
function appendCell(row, value, className = '') {
  const cell = document.createElement('td');
  cell.textContent = value ?? '-';
  if (className) cell.className = className;
  row.appendChild(cell);
  return cell;
}

// Görev durumunu Bootstrap rozeti olarak üretir.
function createBadge(text, color) {
  const badge = document.createElement('span');
  badge.className = `badge text-bg-${color}`;
  badge.textContent = text;
  return badge;
}

// Günlük görev listesinden üst bölümdeki toplam ve durum sayaçlarını hesaplar.
function renderSummary(items) {
  const counts = { planned: 0, active: 0, completed: 0 };
  items.forEach((item) => {
    const group = taskStatus(item.status).group;
    if (Object.hasOwn(counts, group)) counts[group] += 1;
  });
  document.querySelector('#task-total').textContent = items.length;
  document.querySelector('#task-planned').textContent = counts.planned;
  document.querySelector('#task-active').textContent = counts.active;
  document.querySelector('#task-completed').textContent = counts.completed;
}

// API görevlerini hat, saat, araç, sürücü ve durum bilgileriyle tabloya aktarır.
function renderTasks(items) {
  const body = document.querySelector('#tasks-body');
  body.replaceChildren();

  if (!items.length) {
    const row = document.createElement('tr');
    const cell = document.createElement('td');
    cell.colSpan = 10;
    cell.className = 'text-center text-secondary py-5';
    cell.textContent = 'Seçilen tarih ve hatta ait görev bulunamadı.';
    row.appendChild(cell);
    body.appendChild(row);
  }

  items.forEach((task) => {
    const row = document.createElement('tr');
    appendCell(row, task.taskNumber, 'fw-semibold text-nowrap');
    const routeCell = appendCell(row, '');
    const routeCode = document.createElement('div');
    routeCode.className = 'fw-semibold';
    routeCode.textContent = task.route.code;
    const routeName = document.createElement('div');
    routeName.className = 'small text-secondary';
    routeName.textContent = task.route.name;
    routeCell.append(routeCode, routeName);
    appendCell(row, task.sequenceNumber);
    appendCell(row, `${formatTime(task.plannedDepartureAt)} – ${formatTime(task.plannedArrivalAt)}`, 'text-nowrap');
    appendCell(row, task.actualDepartureAt || task.actualArrivalAt ? `${formatTime(task.actualDepartureAt)} – ${formatTime(task.actualArrivalAt)}` : '-', 'text-nowrap');
    appendCell(row, task.assignment ? `${task.assignment.vehicle.doorNumber} · ${task.assignment.vehicle.plate}` : 'Atama yok');
    appendCell(row, task.assignment ? `${task.assignment.driver.fullName} · ${task.assignment.driver.personnelNumber}` : 'Atama yok');
    appendCell(row, translateDisplayValue(task.assignment?.assignmentType));
    const status = taskStatus(task.status);
    appendCell(row, '').appendChild(createBadge(status.text, status.color));
    const actionCell = appendCell(row, '', 'text-end');
    const detailButton = document.createElement('button');
    detailButton.type = 'button';
    detailButton.className = 'btn btn-outline-primary btn-sm';
    detailButton.dataset.taskId = task.id;
    detailButton.innerHTML = '<i class="bi bi-eye me-1"></i>Detay';
    actionCell.appendChild(detailButton);
    body.appendChild(row);
  });

  document.querySelector('#task-count').textContent = `${items.length} kayıt`;
  renderSummary(items);
}

// Filtre seçimine göre görevleri yükler ve ekranın yüklenme/hata durumlarını yönetir.
async function loadTasks() {
  const loading = document.querySelector('#tasks-loading');
  const table = document.querySelector('#tasks-table-container');
  const errorBox = document.querySelector('#tasks-error');
  loading.classList.remove('d-none');
  table.classList.add('d-none');
  errorBox.classList.add('d-none');
  try {
    const items = await tasksApi.getByDate(document.querySelector('#task-date').value, document.querySelector('#task-route').value);
    renderTasks(items);
    table.classList.remove('d-none');
  } catch (error) {
    errorBox.textContent = error.message;
    errorBox.classList.remove('d-none');
  } finally {
    loading.classList.add('d-none');
  }
}

// Aktif hatları kod ve ad bilgisiyle filtre seçim kutusuna ekler.
async function loadRoutes() {
  const routes = await tasksApi.getRoutes();
  const select = document.querySelector('#task-route');
  routes.forEach((route) => select.appendChild(new Option(`${route.code} · ${route.name}`, route.id)));
}

// Görev planının temel bilgilerini duyarlı Bootstrap ızgarasında gösterir.
function createInfoGrid(task) {
  const grid = document.createElement('div');
  grid.className = 'row g-3 mb-4';
  const status = taskStatus(task.status);
  const values = [
    ['Görev numarası', task.taskNumber], ['Hizmet tarihi', task.serviceDate],
    ['Hat', `${task.route.code} · ${task.route.name}`], ['Sıra', task.sequenceNumber],
    ['Planlanan kalkış', formatDateTime(task.plannedDepartureAt)], ['Planlanan varış', formatDateTime(task.plannedArrivalAt)],
    ['Gerçek kalkış', formatDateTime(task.actualDepartureAt)], ['Gerçek varış', formatDateTime(task.actualArrivalAt)],
    ['Durum', status.text],
  ];
  values.forEach(([label, value]) => {
    const column = document.createElement('div');
    column.className = 'col-12 col-sm-6 col-lg-3';
    const labelElement = document.createElement('div');
    labelElement.className = 'small text-secondary';
    labelElement.textContent = label;
    const valueElement = document.createElement('div');
    valueElement.className = 'fw-semibold';
    valueElement.textContent = String(value ?? '-');
    column.append(labelElement, valueElement);
    grid.appendChild(column);
  });
  return grid;
}

// Arıza veya personel olayı sonrası değişen araç/sürücü atamalarını geçmiş tablosunda gösterir.
function createAssignmentHistory(assignments) {
  const section = document.createElement('section');
  section.className = 'card';
  section.innerHTML = '<div class="card-header fw-semibold">Araç ve Sürücü Atama Geçmişi</div>';
  const body = document.createElement('div');
  body.className = 'table-responsive';
  const table = document.createElement('table');
  table.className = 'table table-striped align-middle mb-0';
  table.innerHTML = '<thead><tr><th>Araç</th><th>Sürücü</th><th>Atama türü</th><th>Atanma</th><th>Bitiş</th><th>Durum</th><th>Açıklama</th></tr></thead>';
  const tableBody = document.createElement('tbody');
  assignments.forEach((assignment) => {
    const row = document.createElement('tr');
    appendCell(row, `${assignment.vehicle.doorNumber} · ${assignment.vehicle.plate}`);
    appendCell(row, `${assignment.driver.fullName} · ${assignment.driver.personnelNumber}`);
    appendCell(row, translateDisplayValue(assignment.assignmentType));
    appendCell(row, formatDateTime(assignment.assignedAt));
    appendCell(row, formatDateTime(assignment.endedAt));
    const activeCell = appendCell(row, '');
    activeCell.appendChild(createBadge(assignment.isActive ? 'Aktif' : 'Sona erdi', assignment.isActive ? 'success' : 'secondary'));
    appendCell(row, assignment.description ?? '-');
    tableBody.appendChild(row);
  });
  if (!assignments.length) {
    const row = document.createElement('tr');
    const cell = document.createElement('td');
    cell.colSpan = 7;
    cell.className = 'text-center text-secondary py-4';
    cell.textContent = 'Göreve araç ve sürücü atanmamış.';
    row.appendChild(cell);
    tableBody.appendChild(row);
  }
  table.appendChild(tableBody);
  body.appendChild(table);
  section.appendChild(body);
  return section;
}

// Seçilen görevin plan ve atama geçmişini API'den getirerek modalda gösterir.
async function showTaskDetail(id) {
  const content = document.querySelector('#task-detail-content');
  content.innerHTML = '<div class="text-center py-5"><div class="spinner-border text-primary" role="status"></div></div>';
  detailModal.show();
  try {
    const task = await tasksApi.getById(id);
    document.querySelector('#task-detail-title').textContent = task.taskNumber;
    content.replaceChildren(createInfoGrid(task), createAssignmentHistory(task.assignments ?? []));
  } catch (error) {
    const alert = document.createElement('div');
    alert.className = 'alert alert-danger';
    alert.textContent = error.message;
    content.replaceChildren(alert);
  }
}

// Oturumu doğrulayıp ortak menüyü, hatları ve bugünün görevlerini hazırlar.
async function initialize() {
  try {
    const user = await authService.requireAuthenticatedUser();
    if (!user) return;
    const garage = user.garageName ? ` · ${user.garageName}` : '';
    document.querySelector('#current-user').textContent = `${user.fullName} · ${user.role}${garage}`;
    renderNavigation('tasks', user.role);
    document.querySelector('#task-date').value = localDateValue();
    await loadRoutes();
    await loadTasks();
  } catch (error) {
    const errorBox = document.querySelector('#tasks-error');
    errorBox.textContent = error.message;
    errorBox.classList.remove('d-none');
    document.querySelector('#tasks-loading').classList.add('d-none');
  }
}

// Filtre formu klasik sayfa yenilemesi yerine yeni API sorgusu çalıştırır.
document.querySelector('#task-filter-form').addEventListener('submit', (event) => { event.preventDefault(); loadTasks(); });

// Dinamik Detay düğmeleri event delegation ile tek olay dinleyicisinden yönetilir.
document.querySelector('#tasks-body').addEventListener('click', (event) => {
  const button = event.target.closest('[data-task-id]');
  if (button) showTaskDetail(button.dataset.taskId);
});

// Oturum kapatılmadan önce kullanıcıdan onay alınır.
document.querySelector('#logout-button').addEventListener('click', async () => {
  const result = await Swal.fire({ icon: 'question', title: 'Çıkış yapılsın mı?', showCancelButton: true, confirmButtonText: 'Çıkış Yap', cancelButtonText: 'Vazgeç', confirmButtonColor: '#2563eb' });
  if (result.isConfirmed) authService.logout();
});

initialize();
