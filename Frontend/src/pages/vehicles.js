import 'bootstrap/dist/js/bootstrap.bundle.min.js';
import 'bootstrap-icons/font/bootstrap-icons.css';
import 'admin-lte/dist/css/adminlte.min.css';
import 'admin-lte';
import { Modal } from 'bootstrap';
import Swal from 'sweetalert2';
import { authService } from '../auth/auth-service.js';
import { vehiclesApi } from '../api/vehicles-api.js';
import { renderNavigation } from '../ui/navigation.js';
import { translateDisplayValue } from '../ui/turkish-display.js';
import '../styles/app.css';

// Sayfalama bilgisi tek nesnede tutulur; filtre değişince yalnızca sayfa numarası sıfırlanır.
const state = { page: 1, pageSize: 100, totalPages: 1 };
// Kilometre ve kayıt sayıları Türkçe sayı biçiminde gösterilir.
const numberFormatter = new Intl.NumberFormat('tr-TR');
// Geçmiş kayıtlarının tarih ve saatini Türkiye yerel biçimine dönüştürür.
const dateFormatter = new Intl.DateTimeFormat('tr-TR', { dateStyle: 'short', timeStyle: 'short' });
// Bootstrap modal örneği bir kez oluşturularak her detay isteğinde yeniden kullanılır.
const detailModal = new Modal(document.querySelector('#vehicle-detail-modal'));
const editModal = new Modal(document.querySelector('#vehicle-edit-modal'));
let currentUser = null;
let selectedVehicle = null;
let managementOptions = null;

// API'den gelen seçenekleri tekrar istek atmadan düzenleme formundaki select alanlarına yerleştirir.
function fillSelect(selector, items, label) {
  const select = document.querySelector(selector);
  select.replaceChildren(...items.map((item) => new Option(label(item), item.id)));
}

// Kullanıcının rolüne uymayan data-roles menülerini arayüzden gizler.
function applyRoleMenu(role) {
  document.querySelectorAll('[data-roles]').forEach((element) => {
    const roles = element.dataset.roles.split(',').map((item) => item.trim());
    element.classList.toggle('d-none', !roles.includes(role));
  });
}

// Dinamik tabloya XSS riski oluşturmadan düz metin hücresi ekler.
function appendCell(row, value, className = '') {
  const cell = document.createElement('td');
  cell.textContent = value ?? '-';
  if (className) cell.className = className;
  row.appendChild(cell);
  return cell;
}

// Araç durumu gibi kısa değerleri Bootstrap renkli rozeti şeklinde oluşturur.
function createBadge(text, color) {
  const badge = document.createElement('span');
  badge.className = `badge text-bg-${color}`;
  badge.textContent = text;
  return badge;
}

// Server-side sayfalama cevabını araç tablosu ve sayfa kontrollerine yansıtır.
function renderVehicles(result) {
  const body = document.querySelector('#vehicles-body');
  body.replaceChildren();

  if (!result.items.length) {
    // Filtre sonucu boşsa tablonun içinde anlaşılır bir bilgilendirme gösterilir.
    const row = document.createElement('tr');
    const cell = document.createElement('td');
    cell.colSpan = 10;
    cell.className = 'text-center text-secondary py-5';
    cell.textContent = 'Filtreye uygun araç bulunamadı.';
    row.appendChild(cell);
    body.appendChild(row);
  }

  result.items.forEach((vehicle) => {
    // Her araç için yeni satır oluşturulur; veri innerHTML yerine güvenli yardımcılarla yazılır.
    const row = document.createElement('tr');
    appendCell(row, vehicle.doorNumber, 'fw-semibold');
    appendCell(row, vehicle.plate);
    appendCell(row, `${vehicle.brand} ${vehicle.model}`);
    appendCell(row, String(vehicle.modelYear));
    appendCell(row, vehicle.vehicleType);
    appendCell(row, vehicle.garage);
    appendCell(row, translateDisplayValue(vehicle.status)).appendChild(createBadge(translateDisplayValue(vehicle.status), vehicle.isActive ? 'info' : 'secondary'));
    // Önce eklenen düz metni kaldırıp yalnızca renkli durum rozeti bırakılır.
    row.children[6].firstChild?.remove();
    appendCell(row, numberFormatter.format(vehicle.currentMileage), 'text-end');
    const activeCell = appendCell(row, '');
    activeCell.appendChild(createBadge(vehicle.isActive ? 'Aktif' : 'Pasif', vehicle.isActive ? 'success' : 'secondary'));
    const actionCell = appendCell(row, '', 'text-end');
    const detailButton = document.createElement('button');
    detailButton.type = 'button';
    detailButton.className = 'btn btn-outline-danger btn-sm';
    detailButton.dataset.vehicleId = vehicle.id;
    detailButton.innerHTML = '<i class="bi bi-eye me-1"></i>Detay';
    actionCell.appendChild(detailButton);
    body.appendChild(row);
  });

  state.page = result.page;
  state.totalPages = Math.max(1, result.totalPages);
  document.querySelector('#vehicle-count').textContent = `${numberFormatter.format(result.totalCount)} kayıt`;
  document.querySelector('#page-summary').textContent = `Sayfa ${result.page} / ${state.totalPages} · Sayfada ${result.items.length} araç`;
  document.querySelector('#previous-page').disabled = result.page <= 1;
  document.querySelector('#next-page').disabled = result.page >= state.totalPages;
}

// Form alanlarındaki güncel filtreleri API'nin beklediği sorgu nesnesine çevirir.
function currentFilters() {
  return {
    page: state.page,
    pageSize: state.pageSize,
    search: document.querySelector('#vehicle-search').value.trim(),
    garageId: document.querySelector('#garage-filter').value,
    isActive: document.querySelector('#active-filter').value,
  };
}

// Araç listesini yüklerken spinner, tablo ve hata alanlarının görünürlüğünü yönetir.
async function loadVehicles() {
  const loading = document.querySelector('#vehicles-loading');
  const table = document.querySelector('#vehicles-table-container');
  const errorBox = document.querySelector('#vehicles-error');
  loading.classList.remove('d-none');
  table.classList.add('d-none');
  errorBox.classList.add('d-none');

  try {
    const result = await vehiclesApi.getPage(currentFilters());
    renderVehicles(result);
    table.classList.remove('d-none');
  } catch (error) {
    // Backend, ağ veya yetki hatası kullanıcıya sayfa içinde gösterilir.
    errorBox.textContent = error.message;
    errorBox.classList.remove('d-none');
  } finally {
    // İstek tamamlandığında yükleme göstergesi her koşulda kapatılır.
    loading.classList.add('d-none');
  }
}

// Garaj endpoint'inden gelen değerlerle filtre seçim kutusunu dinamik doldurur.
async function loadGarages() {
  const garages = await vehiclesApi.getGarages();
  const select = document.querySelector('#garage-filter');
  garages.forEach((garage) => {
    const option = document.createElement('option');
    option.value = garage.id;
    option.textContent = `${garage.name} (${garage.code})`;
    select.appendChild(option);
  });
}

// Araç detay DTO'sundaki temel özellikleri duyarlı Bootstrap ızgarasına dönüştürür.
function createInfoGrid(vehicle) {
  const grid = document.createElement('div');
  grid.className = 'row g-3 mb-4';
  const values = [
    ['Kapı No', vehicle.doorNumber], ['Plaka', vehicle.plate],
    ['Marka / Model', `${vehicle.brand} ${vehicle.model}`], ['Model Yılı', vehicle.modelYear],
    ['Araç Tipi', vehicle.vehicleType], ['Yakıt', vehicle.fuelType],
    ['Garaj', vehicle.garage], ['Durum', translateDisplayValue(vehicle.status)],
    ['Kilometre', numberFormatter.format(vehicle.currentMileage)], ['Kapasite', vehicle.capacity ?? '-'],
    ['Görev Tipi', vehicle.dutyType ?? '-'], ['Kayıt', vehicle.isActive ? 'Aktif' : 'Pasif'],
  ];
  values.forEach(([label, value]) => {
    // Her bilgi etiketi ve değeri küçük ekranlardan masaüstüne uyumlu sütunda gösterilir.
    const column = document.createElement('div');
    column.className = 'col-12 col-sm-6 col-lg-3';
    const labelElement = document.createElement('div');
    labelElement.className = 'small text-secondary';
    labelElement.textContent = label;
    const valueElement = document.createElement('div');
    valueElement.className = 'fw-semibold';
    valueElement.textContent = String(value);
    column.append(labelElement, valueElement);
    grid.appendChild(column);
  });
  return grid;
}

// Arıza, garaj ve durum geçmişleri için tekrar kullanılabilir bir tablo üretir.
function createHistoryTable(title, headers, items, valueSelector) {
  const section = document.createElement('section');
  section.className = 'mb-4';
  const heading = document.createElement('h3');
  heading.className = 'h6 border-bottom pb-2';
  heading.textContent = `${title} (${items.length})`;
  section.appendChild(heading);
  if (!items.length) {
    // Geçmiş yoksa boş tablo başlıkları yerine kısa açıklama gösterilir.
    const empty = document.createElement('p');
    empty.className = 'text-secondary small';
    empty.textContent = 'Geçmiş kaydı bulunmuyor.';
    section.appendChild(empty);
    return section;
  }
  const wrapper = document.createElement('div');
  wrapper.className = 'table-responsive';
  const table = document.createElement('table');
  table.className = 'table table-sm table-striped';
  const head = document.createElement('thead');
  const headRow = document.createElement('tr');
  headers.forEach((text) => { const th = document.createElement('th'); th.textContent = text; headRow.appendChild(th); });
  head.appendChild(headRow);
  const body = document.createElement('tbody');
  items.forEach((item) => {
    // valueSelector her geçmiş türünün farklı alanlarını ortak tablo yapısına uyarlar.
    const row = document.createElement('tr');
    valueSelector(item).forEach((value) => appendCell(row, value));
    body.appendChild(row);
  });
  table.append(head, body);
  wrapper.appendChild(table);
  section.appendChild(wrapper);
  return section;
}

// Detay butonuna basılan aracın bilgilerini API'den getirip modal içinde gösterir.
async function showVehicleDetail(id) {
  const content = document.querySelector('#vehicle-detail-content');
  content.replaceChildren();
  const loading = document.createElement('div');
  loading.className = 'text-center py-5';
  loading.innerHTML = '<div class="spinner-border text-danger" role="status"></div>';
  content.appendChild(loading);
  // API cevabı beklenirken modal hemen açılır ve kullanıcı yükleme durumunu görür.
  detailModal.show();

  try {
    const detail = await vehiclesApi.getById(id);
    selectedVehicle = detail.vehicle;
    document.querySelector('#vehicle-detail-title').textContent = `${detail.vehicle.doorNumber} · ${detail.vehicle.plate}`;
    content.replaceChildren(
      createInfoGrid(detail.vehicle),
      createHistoryTable('Arıza Geçmişi', ['Arıza No', 'Kategori', 'Durum', 'Tarih'], detail.faultHistory,
        (item) => [item.faultNumber, item.category, translateDisplayValue(item.status), dateFormatter.format(new Date(item.occurredAt))]),
      createHistoryTable('Garaj Değişiklik Geçmişi', ['Eski Garaj', 'Yeni Garaj', 'Açıklama', 'Tarih', 'İşlemi Yapan'], detail.garageHistory,
        (item) => [item.oldGarage ?? '-', item.newGarage, item.description, dateFormatter.format(new Date(item.changedAt)), item.changedBy]),
      createHistoryTable('Durum Değişiklik Geçmişi', ['Eski Durum', 'Yeni Durum', 'Açıklama', 'Tarih', 'İşlemi Yapan'], detail.statusHistory,
        (item) => [item.oldStatus ?? '-', item.newStatus, item.description, dateFormatter.format(new Date(item.changedAt)), item.changedBy]),
    );
    if (currentUser?.role === 'Admin') {
      const editButton = document.querySelector('#vehicle-edit-button');
      const activeButton = document.querySelector('#vehicle-active-button');
      editButton.classList.remove('d-none');
      activeButton.classList.remove('d-none');
      activeButton.textContent = selectedVehicle.isActive ? 'Pasife Al' : 'Aktifleştir';
      activeButton.className = `btn btn-outline-${selectedVehicle.isActive ? 'danger' : 'success'}`;
    }
  } catch (error) {
    // Detay isteği başarısızsa modal kapanmadan hata mesajı gösterilir.
    const alert = document.createElement('div');
    alert.className = 'alert alert-danger';
    alert.textContent = error.message;
    content.replaceChildren(alert);
  }
}

// Araç sayfasının ana başlangıç akışı: oturum, rol, garaj filtresi ve araç listesidir.
async function initialize() {
  try {
    currentUser = await authService.requireAuthenticatedUser();
    if (!currentUser) return;
    const garage = currentUser.garageName ? ` · ${currentUser.garageName}` : '';
    document.querySelector('#current-user').textContent = `${currentUser.fullName} · ${currentUser.role}${garage}`;
    applyRoleMenu(currentUser.role);
    renderNavigation('vehicles', currentUser.role);
    // Garaj yetkilisinin backend verisi zaten kendi garajıyla sınırlandığı için filtre gizlenir.
    if (currentUser.role === 'Garaj Yetkilisi') document.querySelector('#garage-filter-container').classList.add('d-none');
    else await loadGarages();
    if (currentUser.role === 'Admin') {
      managementOptions = await vehiclesApi.getManagementOptions();
      fillSelect('#edit-vehicle-type', managementOptions.vehicleTypes, (item) => item.name);
      fillSelect('#edit-fuel-type', managementOptions.fuelTypes, (item) => item.name);
      fillSelect('#edit-garage', managementOptions.garages, (item) => `${item.code} · ${item.name}`);
      fillSelect('#edit-status', managementOptions.statuses, (item) => item.name);
    }
    await loadVehicles();
  } catch (error) {
    const errorBox = document.querySelector('#vehicles-error');
    errorBox.textContent = error.message;
    errorBox.classList.remove('d-none');
  }
}

// Filtre gönderildiğinde ilk sayfaya dönülür ve yeni sorgu çalıştırılır.
document.querySelector('#vehicle-filter-form').addEventListener('submit', (event) => { event.preventDefault(); state.page = 1; loadVehicles(); });
// Önceki ve sonraki düğmeleri sınırlar içinde sayfa numarasını değiştirir.
document.querySelector('#previous-page').addEventListener('click', () => { if (state.page > 1) { state.page -= 1; loadVehicles(); } });
document.querySelector('#next-page').addEventListener('click', () => { if (state.page < state.totalPages) { state.page += 1; loadVehicles(); } });
// Event delegation sayesinde sonradan oluşturulan bütün Detay butonları tek dinleyiciyle çalışır.
document.querySelector('#vehicles-body').addEventListener('click', (event) => {
  const button = event.target.closest('[data-vehicle-id]');
  if (button) showVehicleDetail(button.dataset.vehicleId);
});

// Detayda seçili aracın bilgilerini yönetim formuna aktarır.
document.querySelector('#vehicle-edit-button').addEventListener('click', () => {
  if (!selectedVehicle || !managementOptions) return;
  document.querySelector('#edit-vehicle-id').value = selectedVehicle.id;
  document.querySelector('#edit-door-number').value = selectedVehicle.doorNumber;
  document.querySelector('#edit-plate').value = selectedVehicle.plate;
  document.querySelector('#edit-brand').value = selectedVehicle.brand;
  document.querySelector('#edit-model').value = selectedVehicle.model;
  document.querySelector('#edit-model-year').value = selectedVehicle.modelYear;
  document.querySelector('#edit-mileage').value = selectedVehicle.currentMileage;
  document.querySelector('#edit-vehicle-type').value = selectedVehicle.vehicleTypeId;
  document.querySelector('#edit-fuel-type').value = selectedVehicle.fuelTypeId;
  document.querySelector('#edit-garage').value = selectedVehicle.garageId;
  document.querySelector('#edit-status').value = selectedVehicle.vehicleStatusId;
  document.querySelector('#edit-duty-type').value = selectedVehicle.dutyType ?? '';
  document.querySelector('#edit-capacity').value = selectedVehicle.capacity ?? '';
  document.querySelector('#edit-description').value = '';
  detailModal.hide(); editModal.show();
});

document.querySelector('#vehicle-edit-form').addEventListener('submit', async (event) => {
  event.preventDefault();
  const button = event.submitter ?? event.currentTarget.querySelector('[type="submit"]');
  if (button) button.disabled = true;
  const capacity = document.querySelector('#edit-capacity').value;
  const payload = {
    plate: document.querySelector('#edit-plate').value.trim(), brand: document.querySelector('#edit-brand').value.trim(),
    model: document.querySelector('#edit-model').value.trim(), modelYear: Number(document.querySelector('#edit-model-year').value),
    vehicleTypeId: Number(document.querySelector('#edit-vehicle-type').value), fuelTypeId: Number(document.querySelector('#edit-fuel-type').value),
    currentMileage: Number(document.querySelector('#edit-mileage').value), garageId: Number(document.querySelector('#edit-garage').value),
    vehicleStatusId: Number(document.querySelector('#edit-status').value), dutyType: document.querySelector('#edit-duty-type').value.trim() || null,
    capacity: capacity === '' ? null : Number(capacity), changeDescription: document.querySelector('#edit-description').value.trim(),
  };
  try {
    await vehiclesApi.update(Number(document.querySelector('#edit-vehicle-id').value), payload);
    editModal.hide(); await Swal.fire({ icon: 'success', title: 'Araç bilgileri güncellendi' }); await loadVehicles();
  } catch (error) { await Swal.fire({ icon: 'error', title: 'Araç güncellenemedi', text: error.message }); }
  finally { if (button) button.disabled = false; }
});

document.querySelector('#vehicle-active-button').addEventListener('click', async () => {
  if (!selectedVehicle) return;
  const targetActive = !selectedVehicle.isActive;
  const result = await Swal.fire({ title: targetActive ? 'Araç aktifleştirilsin mi?' : 'Araç pasife alınsın mı?',
    input: 'textarea', inputLabel: 'İşlem nedeni', inputPlaceholder: 'Açıklama zorunludur', showCancelButton: true,
    confirmButtonText: targetActive ? 'Aktifleştir' : 'Pasife Al', cancelButtonText: 'Vazgeç',
    inputValidator: (value) => (!value?.trim() ? 'İşlem nedeni zorunludur.' : undefined) });
  if (!result.isConfirmed) return;
  try {
    await vehiclesApi.changeActive(selectedVehicle.id, { isActive: targetActive, reason: result.value.trim() });
    detailModal.hide(); await Swal.fire({ icon: 'success', title: targetActive ? 'Araç aktifleştirildi' : 'Araç pasife alındı' }); await loadVehicles();
  } catch (error) { await Swal.fire({ icon: 'error', title: 'Araç durumu değiştirilemedi', text: error.message }); }
});
// Oturum kapatılmadan önce kullanıcıdan onay alınır.
document.querySelector('#logout-button').addEventListener('click', async () => {
  const result = await Swal.fire({ icon: 'question', title: 'Çıkış yapılsın mı?', showCancelButton: true, confirmButtonText: 'Çıkış Yap', cancelButtonText: 'Vazgeç', confirmButtonColor: '#b00000' });
  if (result.isConfirmed) authService.logout();
});

// DOM hazırlandıktan ve olay dinleyicileri bağlandıktan sonra ilk veri yüklemesi başlatılır.
initialize();
