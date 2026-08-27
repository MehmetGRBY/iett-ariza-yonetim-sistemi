import 'bootstrap/dist/js/bootstrap.bundle.min.js';
import 'bootstrap-icons/font/bootstrap-icons.css';
import 'admin-lte/dist/css/adminlte.min.css';
import 'admin-lte';
import { Modal } from 'bootstrap';
import Swal from 'sweetalert2';
import { authService } from '../auth/auth-service.js';
import { driversApi } from '../api/drivers-api.js';
import { garagesApi } from '../api/garages-api.js';
import { renderNavigation } from '../ui/navigation.js';
import { translateDisplayValue } from '../ui/turkish-display.js';
import '../styles/app.css';

let drivers = []; let currentUser = null;
const detailModal = new Modal('#driver-detail-modal'); const createModal = new Modal('#driver-create-modal');
const statusMap = { AVAILABLE: ['Müsait', 'success'], ON_DUTY: ['Görevde', 'primary'], ON_LEAVE: ['İzinli / Raporlu', 'warning'], PASSIVE: ['Pasif', 'secondary'] };
const dateTimeFormatter = new Intl.DateTimeFormat('tr-TR', { dateStyle: 'short', timeStyle: 'short' });

// API tarihini kullanıcıya yerel ve okunabilir biçimde gösterir.
function formatDateTime(value) { return value ? dateTimeFormatter.format(new Date(value)) : 'Rapor bekleniyor'; }

// Kod değerini personele uygun Türkçe rozet olarak üretir.
function badge(code, map = statusMap) { const [text, color] = map[code] ?? [translateDisplayValue(code), 'secondary']; const element = document.createElement('span'); element.className = `badge text-bg-${color}`; element.textContent = text; return element; }
function cell(row, value) { const item = document.createElement('td'); item.textContent = value ?? '-'; row.appendChild(item); return item; }

// Seçili filtreleri uygular ve özet sayaçlarla tabloyu birlikte yeniler.
function render() {
  const search = document.querySelector('#driver-search').value.toLocaleLowerCase('tr-TR'); const garage = document.querySelector('#garage-filter').value; const type = document.querySelector('#type-filter').value; const selectedStatus = document.querySelector('#status-filter').value; const status = selectedStatus === 'INACTIVE' ? 'PASSIVE' : selectedStatus;
  const items = drivers.filter((d) => `${d.personnelNumber} ${d.firstName} ${d.lastName}`.toLocaleLowerCase('tr-TR').includes(search) && (!garage || String(d.garageId) === garage) && (!type || d.driverType === type) && (!status || d.availabilityStatus === status));
  document.querySelector('#driver-total').textContent = items.length; document.querySelector('#driver-available').textContent = items.filter((d) => d.availabilityStatus === 'AVAILABLE').length; document.querySelector('#driver-duty').textContent = items.filter((d) => d.availabilityStatus === 'ON_DUTY').length; document.querySelector('#driver-reserve').textContent = items.filter((d) => d.driverType === 'RESERVE').length;
  const body = document.querySelector('#drivers-body'); body.replaceChildren();
  items.forEach((driver) => { const row = document.createElement('tr'); cell(row, driver.personnelNumber).className = 'fw-semibold'; cell(row, `${driver.firstName} ${driver.lastName}`); cell(row, driver.garage); cell(row, '').appendChild(badge(driver.driverType, { NORMAL: ['Normal', 'info'], RESERVE: ['Yedek', 'warning'] })); const statusBadge = badge(driver.availabilityStatus); if (driver.availabilityStatus === 'ON_LEAVE') statusBadge.title = driver.leaveEventNumber ? `${driver.leaveEventNumber}: ${driver.leaveReason ?? 'Açıklama yok'} · Dönüş: ${formatDateTime(driver.leaveUntil)}` : 'Aktif izin/rapor kaydı denetleniyor'; cell(row, '').appendChild(statusBadge); cell(row, '').appendChild(badge(driver.isActive ? 'A' : 'P', { A: ['Aktif', 'success'], P: ['Pasif', 'secondary'] })); const actions = cell(row, ''); actions.className = 'text-nowrap'; actions.innerHTML = `<button class="btn btn-outline-primary btn-sm me-1" data-detail="${driver.id}">Detay</button><button class="btn btn-outline-${driver.isActive ? 'danger' : 'success'} btn-sm" data-toggle="${driver.id}">${driver.isActive ? 'Pasife al' : 'Aktifleştir'}</button>`; body.appendChild(row); });
  if (!items.length) body.innerHTML = '<tr><td colspan="7" class="text-center text-secondary py-5">Filtreye uygun sürücü bulunamadı.</td></tr>';
}

// Sürücünün görev ve arıza geçmişini API'den alıp okunabilir tablolara dönüştürür.
async function showDetail(id) { detailModal.show(); const content = document.querySelector('#driver-detail-content'); content.innerHTML = '<div class="text-center py-5"><div class="spinner-border"></div></div>'; try { const d = await driversApi.getById(id); document.querySelector('#driver-detail-title').textContent = `${d.firstName} ${d.lastName} · ${d.personnelNumber}`; const rows = (items, columns, translatedColumns = []) => items.length ? items.map((item) => `<tr>${columns.map((column) => `<td>${translatedColumns.includes(column) ? translateDisplayValue(item[column]) : (item[column] ?? '-')}</td>`).join('')}</tr>`).join('') : `<tr><td colspan="${columns.length}" class="text-center text-secondary">Kayıt yok</td></tr>`; const incident = d.currentIncident ? `<div class="alert alert-warning"><strong>${d.currentIncident.eventNumber} · ${translateDisplayValue(d.currentIncident.eventType)}</strong><div>${d.currentIncident.description}</div><small>Rapor: ${d.currentIncident.reportStatus === 'SUBMITTED' ? 'Girildi' : 'Bekleniyor'} · Dönüş: ${formatDateTime(d.currentIncident.expectedReturnAt)}</small></div>` : ''; content.innerHTML = `${incident}<div class="row g-3 mb-4"><div class="col-md-3"><strong>Garaj</strong><div>${d.garage ?? '-'}</div></div><div class="col-md-3"><strong>Tür</strong><div>${translateDisplayValue(d.driverType)}</div></div><div class="col-md-3"><strong>Toplam görev</strong><div>${d.taskCount}</div></div><div class="col-md-3"><strong>Bildirilen arıza</strong><div>${d.faultCount}</div></div></div><h3 class="h6">Son görevler</h3><div class="table-responsive mb-4"><table class="table table-sm"><thead><tr><th>Görev</th><th>Hat</th><th>Araç</th><th>Atama</th></tr></thead><tbody>${rows(d.recentTasks, ['taskNumber','route','vehicle','assignmentType'], ['assignmentType'])}</tbody></table></div><h3 class="h6">Son arızalar</h3><div class="table-responsive"><table class="table table-sm"><thead><tr><th>Arıza</th><th>Araç</th><th>Kategori</th><th>Durum</th></tr></thead><tbody>${rows(d.recentFaults, ['faultNumber','vehicle','category','status'], ['status'])}</tbody></table></div>`; } catch (error) { content.innerHTML = `<div class="alert alert-danger">${error.message}</div>`; } }

async function initialize() { try { currentUser = await authService.requireAuthenticatedUser(); if (!currentUser) return; document.querySelector('#current-user').textContent = `${currentUser.fullName} · ${currentUser.role}`; renderNavigation('drivers', currentUser.role); const garages = await garagesApi.getAll(); const filter = document.querySelector('#garage-filter'); const create = document.querySelector('#create-garage'); garages.forEach((g) => { filter.appendChild(new Option(`${g.code} · ${g.name}`, g.id)); create.appendChild(new Option(`${g.code} · ${g.name}`, g.id)); }); if (currentUser.role !== 'Admin') { filter.closest('.col-12').classList.add('d-none'); create.value = currentUser.garageId; create.disabled = true; } drivers = await driversApi.getAll(); render(); document.querySelector('#drivers-table-container').classList.remove('d-none'); } catch (error) { const box = document.querySelector('#drivers-error'); box.textContent = error.message; box.classList.remove('d-none'); } finally { document.querySelector('#drivers-loading').classList.add('d-none'); } }

['#driver-search','#garage-filter','#type-filter','#status-filter'].forEach((selector) => document.querySelector(selector).addEventListener(selector === '#driver-search' ? 'input' : 'change', render));
document.querySelector('#create-driver-button').addEventListener('click', () => createModal.show());
document.querySelector('#driver-create-form').addEventListener('submit', async (event) => {
  event.preventDefault();
  const formElement = event.currentTarget;
  const submitButton = event.submitter ?? formElement.querySelector('[type="submit"]');
  const form = new FormData(formElement);
  const payload = Object.fromEntries(form);
  payload.garageId = Number(currentUser.role === 'Admin' ? payload.garageId : currentUser.garageId);

  // Ağ gecikmesinde çift tıklamayla aynı sürücünün iki kez eklenmesini engeller.
  if (submitButton) submitButton.disabled = true;
  try {
    const result = await driversApi.create(payload);
    createModal.hide();
    await Swal.fire({ icon: 'success', title: 'Sürücü eklendi', text: `Otomatik sicil: ${result.personnelNumber}` });
    drivers = await driversApi.getAll();
    render();
    formElement.reset();
  } catch (error) {
    await Swal.fire({ icon: 'error', title: 'İşlem başarısız', text: error.message });
  } finally {
    if (submitButton) submitButton.disabled = false;
  }
});

document.querySelector('#drivers-body').addEventListener('click', async (event) => {
  const detail = event.target.closest('[data-detail]');
  if (detail) {
    await showDetail(detail.dataset.detail);
    return;
  }

  const toggle = event.target.closest('[data-toggle]');
  if (!toggle) return;
  const driver = drivers.find((item) => String(item.id) === toggle.dataset.toggle);
  const confirmation = await Swal.fire({
    icon: 'question',
    title: driver?.isActive ? 'Sürücü pasife alınsın mı?' : 'Sürücü aktifleştirilsin mi?',
    text: driver ? `${driver.personnelNumber} · ${driver.firstName} ${driver.lastName}` : undefined,
    showCancelButton: true,
    confirmButtonText: 'Evet',
    cancelButtonText: 'Vazgeç',
  });
  if (!confirmation.isConfirmed) return;

  toggle.disabled = true;
  try {
    await driversApi.toggleActive(toggle.dataset.toggle);
    drivers = await driversApi.getAll();
    render();
  } catch (error) {
    await Swal.fire({ icon: 'error', title: 'Durum değiştirilemedi', text: error.message });
  } finally {
    toggle.disabled = false;
  }
});
document.querySelector('#logout-button').addEventListener('click', () => authService.logout()); initialize();
