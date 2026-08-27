import 'bootstrap/dist/js/bootstrap.bundle.min.js';
import 'bootstrap-icons/font/bootstrap-icons.css';
import 'admin-lte/dist/css/adminlte.min.css';
import 'admin-lte';
import { Modal } from 'bootstrap';
import Swal from 'sweetalert2';
import { authService } from '../auth/auth-service.js';
import { decisionSupportApi } from '../api/decision-support-api.js';
import { garagesApi } from '../api/garages-api.js';
import { tasksApi } from '../api/tasks-api.js';
import { renderNavigation } from '../ui/navigation.js';
import { translateDisplayValue } from '../ui/turkish-display.js';
import '../styles/app.css';

let events = []; let currentUser = null; const createModal = new Modal('#create-modal');
const typeNames = { ROAD_CLOSURE: 'Yol kapanması', ACCIDENT: 'Kaza', WEATHER: 'Olumsuz hava', TRAFFIC_DENSITY: 'Trafik yoğunluğu', GARAGE_OPERATION: 'Garaj operasyonu', OTHER: 'Diğer' };
const formatter = new Intl.DateTimeFormat('tr-TR', { dateStyle: 'short', timeStyle: 'short' });
function formatDate(value) { return value ? formatter.format(new Date(value)) : '-'; }
function toLocalInput(value) {
  if (!value) return '';
  const date = new Date(value);
  return new Date(date.getTime() - date.getTimezoneOffset() * 60000).toISOString().slice(0, 16);
}

function canManageEvents() { return currentUser?.role === 'Admin' || currentUser?.role === 'Merkez Yetkilisi'; }

// Olay kayıtlarını arama ve durum süzgecine göre tablo ile sayaçlara yansıtır.
function render() {
  const search = document.querySelector('#search').value.toLocaleLowerCase('tr-TR'); const status = document.querySelector('#status-filter').value;
  const items = events.filter((item) => (!status || item.status === status) && `${item.eventNumber} ${item.title} ${item.garage ?? ''} ${item.route ?? ''}`.toLocaleLowerCase('tr-TR').includes(search));
  document.querySelector('#total').textContent = items.length; document.querySelector('#open').textContent = items.filter((item) => item.status === 'OPEN').length; document.querySelector('#garage-count').textContent = items.filter((item) => item.garageId).length; document.querySelector('#route-count').textContent = items.filter((item) => item.routeId).length;
  const body = document.querySelector('#event-body'); body.replaceChildren();
  items.forEach((item) => {
    const row = document.createElement('tr');
    row.innerHTML = `<td class="fw-semibold text-nowrap"></td><td></td><td><strong data-title></strong><div class="small text-secondary" data-description></div></td><td></td><td></td><td class="text-nowrap"></td><td class="text-nowrap"></td><td><span class="badge text-bg-${item.status === 'OPEN' ? 'danger' : 'success'}">${item.status === 'OPEN' ? 'Açık' : 'Kapalı'}</span></td><td></td>`;
    const cells = row.querySelectorAll('td');
    cells[0].textContent = item.eventNumber; cells[1].textContent = typeNames[item.eventType] ?? translateDisplayValue(item.eventType);
    row.querySelector('[data-title]').textContent = item.title; row.querySelector('[data-description]').textContent = item.description;
    cells[3].textContent = item.garage ?? '-'; cells[4].textContent = item.route ?? '-';
    cells[5].textContent = formatDate(item.startsAt); cells[6].textContent = formatDate(item.endsAt);
    if (canManageEvents()) {
      const button = document.createElement('button'); button.type = 'button'; button.className = 'btn btn-outline-primary btn-sm text-nowrap';
      button.innerHTML = '<i class="bi bi-pencil me-1"></i>Düzenle'; button.addEventListener('click', () => openEditModal(item));
      cells[8].appendChild(button);
    } else cells[8].textContent = '-';
    body.appendChild(row);
  });
  if (!items.length) body.innerHTML = '<tr><td colspan="9" class="text-center text-secondary py-5">Operasyon olayı bulunamadı.</td></tr>';
}

// Seçilen olayın mevcut bilgilerini ortak forma taşır; bitiş ve durum alanları düzenlemede açılır.
function openEditModal(item) {
  const form = document.querySelector('#create-form'); form.reset();
  form.elements.id.value = item.id; form.elements.eventType.value = item.eventType;
  form.elements.title.value = item.title; form.elements.garageId.value = item.garageId ?? '';
  form.elements.routeId.value = item.routeId ?? ''; form.elements.startsAt.value = toLocalInput(item.startsAt);
  form.elements.endsAt.value = toLocalInput(item.endsAt);
  // Backend kapanmış olayı RESOLVED koduyla tutar; formda kullanıcıya Kapalı gösterilir.
  form.elements.status.value = item.status === 'OPEN' ? 'OPEN' : 'CLOSED';
  form.elements.description.value = item.description;
  document.querySelector('#event-modal-title').textContent = `${item.eventNumber} · Düzenle`;
  document.querySelector('#event-save-button').textContent = 'Değişiklikleri Kaydet';
  document.querySelector('#event-status-row').classList.remove('d-none');
  createModal.show();
}

async function initialize() {
  try { currentUser = await authService.requireAuthenticatedUser(); if (!currentUser) return; document.querySelector('#current-user').textContent = `${currentUser.fullName} · ${currentUser.role}`; renderNavigation('operational-events', currentUser.role); if (currentUser.role === 'Garaj Yetkilisi') document.querySelector('#create-button').classList.add('d-none'); const [eventItems, garages, routes] = await Promise.all([decisionSupportApi.getOperationalEvents(), garagesApi.getAll(), tasksApi.getRoutes()]); events = eventItems; garages.forEach((item) => document.querySelector('#event-garage').appendChild(new Option(`${item.code} · ${item.name}`, item.id))); routes.forEach((item) => document.querySelector('#event-route').appendChild(new Option(`${item.code} · ${item.name}`, item.id))); render(); document.querySelector('#table-container').classList.remove('d-none'); }
  catch (error) { const box = document.querySelector('#page-error'); box.textContent = error.message; box.classList.remove('d-none'); }
  finally { document.querySelector('#loading').classList.add('d-none'); }
}

document.querySelector('#search').addEventListener('input', render); document.querySelector('#status-filter').addEventListener('change', render);
document.querySelector('#create-button').addEventListener('click', () => {
  const form = document.querySelector('#create-form'); form.reset(); form.elements.id.value = '';
  document.querySelector('#event-start').value = toLocalInput(new Date());
  document.querySelector('#event-modal-title').textContent = 'Yeni Operasyon Olayı';
  document.querySelector('#event-save-button').textContent = 'Kaydet';
  document.querySelector('#event-status-row').classList.add('d-none'); createModal.show();
});

document.querySelector('#create-form').elements.status.addEventListener('change', (event) => {
  const endsAt = document.querySelector('#create-form').elements.endsAt;
  if (event.target.value === 'CLOSED' && !endsAt.value) endsAt.value = toLocalInput(new Date());
});

document.querySelector('#create-form').addEventListener('submit', async (event) => {
  event.preventDefault(); const form = event.currentTarget; const raw = Object.fromEntries(new FormData(form)); const editing = Boolean(raw.id);
  const payload = { eventType: raw.eventType, title: raw.title, description: raw.description,
    garageId: raw.garageId ? Number(raw.garageId) : null, routeId: raw.routeId ? Number(raw.routeId) : null,
    startsAt: new Date(raw.startsAt).toISOString(), endsAt: raw.endsAt ? new Date(raw.endsAt).toISOString() : null,
    ...(editing ? { status: raw.status } : {}) };
  try {
    if (editing) await decisionSupportApi.updateOperationalEvent(Number(raw.id), payload);
    else await decisionSupportApi.createOperationalEvent(payload);
    createModal.hide(); form.reset(); events = await decisionSupportApi.getOperationalEvents(); render();
    await Swal.fire({ icon: 'success', title: editing ? 'Operasyon olayı güncellendi' : 'Operasyon olayı oluşturuldu' });
  } catch (error) { Swal.fire({ icon: 'error', title: editing ? 'Olay güncellenemedi' : 'Olay oluşturulamadı', text: error.message }); }
});
document.querySelector('#logout-button').addEventListener('click', () => authService.logout()); initialize();

// Backend süresi dolan olayları kapattığında tabloyu kullanıcı yenilemeden günceller.
window.setInterval(async () => {
  if (document.hidden || document.querySelector('#create-modal.show')) return;
  try { events = await decisionSupportApi.getOperationalEvents(); render(); } catch { /* Ana yükleme hata alanını korur. */ }
}, 15000);
