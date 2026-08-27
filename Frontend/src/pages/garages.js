import 'bootstrap/dist/js/bootstrap.bundle.min.js';
import 'bootstrap-icons/font/bootstrap-icons.css';
import 'admin-lte/dist/css/adminlte.min.css';
import 'admin-lte';
import { Modal } from 'bootstrap';
import Swal from 'sweetalert2';
import { authService } from '../auth/auth-service.js';
import { garagesApi } from '../api/garages-api.js';
import { renderNavigation } from '../ui/navigation.js';
import { translateDisplayValue } from '../ui/turkish-display.js';
import '../styles/app.css';

// API'den alınan ana liste bellekte tutulur; metin filtreleri için tekrar istek atılmaz.
let garages = [];
const detailModal = new Modal(document.querySelector('#garage-detail-modal'));
const editModal = new Modal(document.querySelector('#garage-edit-modal'));
let currentUser = null;
let selectedGarage = null;

// Doluluk yüzdesini iş kuralındaki yeşil, sarı ve kırmızı seviyelerinden birine dönüştürür.
function occupancyLevel(rate) {
  if (rate < 60) return { key: 'green', color: 'success', text: 'Uygun' };
  if (rate < 85) return { key: 'yellow', color: 'warning', text: 'Yoğun' };
  return { key: 'red', color: 'danger', text: rate > 100 ? 'Kapasite aşıldı' : 'Kritik' };
}

// Kartların üstündeki toplamlar yalnızca kullanıcının o anda filtreleyip gördüğü garajlardan hesaplanır.
function renderSummary(items) {
  document.querySelector('#garage-total').textContent = items.length;
  document.querySelector('#vehicle-total').textContent = items.reduce((sum, item) => sum + item.totalVehicles, 0).toLocaleString('tr-TR');
  document.querySelector('#capacity-total').textContent = items.reduce((sum, item) => sum + item.availableCapacity, 0).toLocaleString('tr-TR');
  document.querySelector('#technician-total').textContent = items.reduce((sum, item) => sum + item.technicians, 0).toLocaleString('tr-TR');
}

// Garaj özetini doluluk çubuğu ve temel personel göstergeleriyle güvenli DOM elemanlarına dönüştürür.
function createGarageCard(garage) {
  const level = occupancyLevel(garage.occupancyRate);
  const column = document.createElement('div');
  column.className = 'col-12 col-lg-6 col-xxl-4';
  const card = document.createElement('article');
  card.className = `garage-card card h-100 shadow-sm border-${level.color}`;
  card.innerHTML = `
    <div class="card-header d-flex align-items-start gap-3">
      <span class="garage-icon text-bg-${level.color}"><i class="bi bi-buildings"></i></span>
      <div class="flex-grow-1"><h2 class="h5 mb-1"></h2><div class="small text-secondary garage-code"></div></div>
      <span class="badge text-bg-${garage.isActive ? 'success' : 'secondary'}">${garage.isActive ? 'Aktif' : 'Pasif'}</span>
    </div>
    <div class="card-body">
      <div class="d-flex justify-content-between mb-2"><span class="fw-semibold">Doluluk</span><span class="fw-bold text-${level.color}">%${garage.occupancyRate.toLocaleString('tr-TR')}</span></div>
      <div class="progress garage-progress mb-3" role="progressbar" aria-label="Garaj doluluk oranı" aria-valuenow="${garage.occupancyRate}" aria-valuemin="0" aria-valuemax="100"><div class="progress-bar bg-${level.color}"></div></div>
      <div class="row g-3 text-center">
        <div class="col-4"><div class="fw-bold">${garage.totalVehicles}</div><small class="text-secondary">Araç</small></div>
        <div class="col-4"><div class="fw-bold">${garage.availableCapacity}</div><small class="text-secondary">Boş yer</small></div>
        <div class="col-4"><div class="fw-bold">${garage.drivers}</div><small class="text-secondary">Sürücü</small></div>
        <div class="col-4"><div class="fw-bold">${garage.passiveVehicles}</div><small class="text-secondary">Pasif araç</small></div>
        <div class="col-4"><div class="fw-bold">${garage.teams}</div><small class="text-secondary">Ekip</small></div>
        <div class="col-4"><div class="fw-bold">${garage.technicians}</div><small class="text-secondary">Teknisyen</small></div>
      </div>
    </div>
    <div class="card-footer d-flex align-items-center"><span class="small ${garage.hasManager ? 'text-success' : 'text-danger'}"><i class="bi ${garage.hasManager ? 'bi-person-check' : 'bi-person-x'} me-1"></i>${garage.hasManager ? 'Garaj yetkilisi mevcut' : 'Garaj yetkilisi yok'}</span><button class="btn btn-outline-primary btn-sm ms-auto" data-garage-id="${garage.id}"><i class="bi bi-eye me-1"></i>Detay</button></div>`;
  card.querySelector('h2').textContent = garage.name;
  card.querySelector('.garage-code').textContent = `${garage.code} · Kapasite ${garage.vehicleCapacity}`;
  card.querySelector('.progress-bar').style.width = `${Math.min(garage.occupancyRate, 100)}%`;
  column.appendChild(card);
  return column;
}

// Arama ve doluluk seviyesi birlikte uygulanarak görünür kartlar yeniden üretilir.
function applyFilters() {
  const search = document.querySelector('#garage-search').value.trim().toLocaleLowerCase('tr-TR');
  const level = document.querySelector('#occupancy-filter').value;
  const filtered = garages.filter((garage) => {
    const matchesText = `${garage.name} ${garage.code}`.toLocaleLowerCase('tr-TR').includes(search);
    return matchesText && (!level || occupancyLevel(garage.occupancyRate).key === level);
  });
  const grid = document.querySelector('#garage-grid');
  grid.replaceChildren(...filtered.map(createGarageCard));
  if (!filtered.length) {
    const empty = document.createElement('div');
    empty.className = 'col-12 text-center text-secondary py-5';
    empty.textContent = 'Filtreye uygun garaj bulunamadı.';
    grid.appendChild(empty);
  }
  renderSummary(filtered);
}

// Etiket ve değerden oluşan özet kutusu detay ekranındaki tekrarları azaltır.
function infoBox(label, value) {
  const column = document.createElement('div');
  column.className = 'col-6 col-lg-3';
  const box = document.createElement('div');
  box.className = 'border rounded p-3 h-100';
  const labelElement = document.createElement('div');
  labelElement.className = 'small text-secondary';
  labelElement.textContent = label;
  const valueElement = document.createElement('div');
  valueElement.className = 'fs-5 fw-semibold';
  valueElement.textContent = value ?? '-';
  box.append(labelElement, valueElement);
  column.appendChild(box);
  return column;
}

// Araç tipi veya durum dağılımını sade bir liste kartı olarak oluşturur.
function distributionCard(title, items, labelKey) {
  const card = document.createElement('section');
  card.className = 'card h-100';
  card.innerHTML = `<div class="card-header fw-semibold">${title}</div>`;
  const list = document.createElement('ul');
  list.className = 'list-group list-group-flush';
  items.forEach((item) => {
    const row = document.createElement('li');
    row.className = 'list-group-item d-flex justify-content-between';
    const label = document.createElement('span');
    label.textContent = item[labelKey];
    const count = document.createElement('span');
    count.className = 'badge text-bg-primary rounded-pill';
    count.textContent = item.count;
    row.append(label, count);
    list.appendChild(row);
  });
  card.appendChild(list);
  return card;
}

// Teknik ekipleri ve üyelerini tablo halinde gösterir; ekip lideri ayrıca işaretlenir.
function teamsTable(teams) {
  const section = document.createElement('section');
  section.className = 'card mt-4';
  section.innerHTML = '<div class="card-header fw-semibold">Teknik Ekipler</div>';
  const responsive = document.createElement('div');
  responsive.className = 'table-responsive';
  const table = document.createElement('table');
  table.className = 'table table-striped align-middle mb-0';
  table.innerHTML = '<thead><tr><th>Ekip</th><th>Müsaitlik</th><th>Üye</th><th>Sicil</th><th>Görev</th><th>Çalışma durumu</th></tr></thead>';
  const body = document.createElement('tbody');
  teams.forEach((team) => team.members.forEach((member, index) => {
    const row = document.createElement('tr');
    [index ? '' : team.name, index ? '' : (team.isAvailable ? 'Müsait' : 'Görevde'), member.fullName, member.personnelNumber, member.isTeamLeader ? 'Ekip lideri' : 'Teknisyen', translateDisplayValue(member.workStatus)].forEach((value) => {
      const cell = document.createElement('td'); cell.textContent = value; row.appendChild(cell);
    });
    body.appendChild(row);
  }));
  if (!body.children.length) body.innerHTML = '<tr><td colspan="6" class="text-center text-secondary py-4">Aktif teknik ekip bulunmuyor.</td></tr>';
  table.appendChild(body); responsive.appendChild(table); section.appendChild(responsive); return section;
}

// Seçilen garajın ayrıntısını API'den getirerek modal içerisine yerleştirir.
async function showGarageDetail(id) {
  const content = document.querySelector('#garage-detail-content');
  content.innerHTML = '<div class="text-center py-5"><div class="spinner-border text-primary" role="status"></div></div>';
  detailModal.show();
  try {
    const garage = await garagesApi.getById(id);
    selectedGarage = garage;
    document.querySelector('#garage-detail-title').textContent = `${garage.name} (${garage.code})`;
    const summary = document.createElement('div');
    summary.className = 'row g-3 mb-4';
    [
      ['Kapasite', garage.vehicleCapacity], ['Toplam araç', garage.totalVehicles], ['Boş yer', garage.availableCapacity], ['Doluluk', `%${garage.occupancyRate}`],
      ['Aktif araç', garage.activeVehicles], ['Pasif / servis dışı', garage.passiveVehicles], ['Normal sürücü', garage.normalDrivers], ['Yedek sürücü', garage.reserveDrivers],
      ['Teknisyen', garage.technicians], ['Yetkili', garage.manager ? `${garage.manager.fullName} · ${garage.manager.personnelNumber}` : 'Atanmamış'],
    ].forEach(([label, value]) => summary.appendChild(infoBox(label, value)));
    const distributions = document.createElement('div');
    distributions.className = 'row g-4';
    const types = document.createElement('div'); types.className = 'col-12 col-lg-6'; types.appendChild(distributionCard('Araç Tipleri', garage.vehicleTypes, 'type'));
    const statuses = document.createElement('div'); statuses.className = 'col-12 col-lg-6'; statuses.appendChild(distributionCard('Araç Durumları', garage.vehicleStatuses, 'status'));
    distributions.append(types, statuses);
    const address = document.createElement('div'); address.className = 'alert alert-light border mt-4 mb-0'; address.textContent = `Adres: ${garage.address ?? 'Adres bilgisi girilmemiş.'}`;
    content.replaceChildren(summary, distributions, teamsTable(garage.teams), address);
    if (currentUser?.role === 'Admin') {
      const editButton = document.querySelector('#garage-edit-button');
      const activeButton = document.querySelector('#garage-active-button');
      editButton.classList.remove('d-none'); activeButton.classList.remove('d-none');
      activeButton.textContent = garage.isActive ? 'Pasife Al' : 'Aktifleştir';
      activeButton.className = `btn btn-outline-${garage.isActive ? 'danger' : 'success'}`;
    }
  } catch (error) {
    const alert = document.createElement('div'); alert.className = 'alert alert-danger'; alert.textContent = error.message; content.replaceChildren(alert);
  }
}

// Oturum ve menü hazırlandıktan sonra rol kapsamındaki garaj listesi yüklenir.
async function initialize() {
  try {
    currentUser = await authService.requireAuthenticatedUser();
    if (!currentUser) return;
    document.querySelector('#current-user').textContent = `${currentUser.fullName} · ${currentUser.role}${currentUser.garageName ? ` · ${currentUser.garageName}` : ''}`;
    renderNavigation('garages', currentUser.role);
    garages = await garagesApi.getAll();
    applyFilters();
    document.querySelector('#garage-grid').classList.remove('d-none');
  } catch (error) {
    const box = document.querySelector('#garages-error'); box.textContent = error.message; box.classList.remove('d-none');
  } finally { document.querySelector('#garages-loading').classList.add('d-none'); }
}

document.querySelector('#garage-search').addEventListener('input', applyFilters);
document.querySelector('#occupancy-filter').addEventListener('change', applyFilters);
document.querySelector('#garage-grid').addEventListener('click', (event) => { const button = event.target.closest('[data-garage-id]'); if (button) showGarageDetail(button.dataset.garageId); });

// Garaj listesini API'den tekrar okuyup doluluk özetlerini günceller.
async function reloadGarages() { garages = await garagesApi.getAll(); applyFilters(); }

document.querySelector('#garage-edit-button').addEventListener('click', () => {
  if (!selectedGarage) return;
  document.querySelector('#edit-garage-id').value = selectedGarage.id;
  document.querySelector('#edit-garage-code').value = selectedGarage.code;
  document.querySelector('#edit-garage-name').value = selectedGarage.name;
  document.querySelector('#edit-garage-address').value = selectedGarage.address ?? '';
  document.querySelector('#edit-garage-capacity').value = selectedGarage.vehicleCapacity;
  document.querySelector('#edit-garage-capacity').min = Math.max(1, selectedGarage.totalVehicles);
  document.querySelector('#garage-capacity-help').textContent = `Garajda ${selectedGarage.totalVehicles} araç bulunduğu için kapasite bu sayıdan düşük olamaz.`;
  detailModal.hide(); editModal.show();
});

document.querySelector('#garage-edit-form').addEventListener('submit', async (event) => {
  event.preventDefault();
  const button = event.submitter ?? event.currentTarget.querySelector('[type="submit"]');
  if (button) button.disabled = true;
  const id = Number(document.querySelector('#edit-garage-id').value);
  const payload = { name: document.querySelector('#edit-garage-name').value.trim(),
    address: document.querySelector('#edit-garage-address').value.trim() || null,
    vehicleCapacity: Number(document.querySelector('#edit-garage-capacity').value) };
  try {
    await garagesApi.update(id, payload); editModal.hide();
    await Swal.fire({ icon: 'success', title: 'Garaj bilgileri güncellendi' }); await reloadGarages();
  } catch (error) { await Swal.fire({ icon: 'error', title: 'Garaj güncellenemedi', text: error.message }); }
  finally { if (button) button.disabled = false; }
});

document.querySelector('#garage-active-button').addEventListener('click', async () => {
  if (!selectedGarage) return;
  const targetActive = !selectedGarage.isActive;
  const result = await Swal.fire({ title: targetActive ? 'Garaj aktifleştirilsin mi?' : 'Garaj pasife alınsın mı?',
    input: 'textarea', inputLabel: 'İşlem nedeni', inputPlaceholder: 'Açıklama zorunludur', showCancelButton: true,
    confirmButtonText: targetActive ? 'Aktifleştir' : 'Pasife Al', cancelButtonText: 'Vazgeç',
    inputValidator: (value) => (!value?.trim() ? 'İşlem nedeni zorunludur.' : undefined) });
  if (!result.isConfirmed) return;
  try {
    await garagesApi.changeActive(selectedGarage.id, { isActive: targetActive, reason: result.value.trim() });
    detailModal.hide(); await Swal.fire({ icon: 'success', title: targetActive ? 'Garaj aktifleştirildi' : 'Garaj pasife alındı' }); await reloadGarages();
  } catch (error) { await Swal.fire({ icon: 'error', title: 'Garaj durumu değiştirilemedi', text: error.message }); }
});
document.querySelector('#logout-button').addEventListener('click', async () => {
  const result = await Swal.fire({ icon: 'question', title: 'Çıkış yapılsın mı?', showCancelButton: true, confirmButtonText: 'Çıkış Yap', cancelButtonText: 'Vazgeç' });
  if (result.isConfirmed) authService.logout();
});

initialize();
