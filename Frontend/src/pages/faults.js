import 'bootstrap/dist/js/bootstrap.bundle.min.js';
import 'bootstrap-icons/font/bootstrap-icons.css';
import 'admin-lte/dist/css/adminlte.min.css';
import 'admin-lte';
import { Modal } from 'bootstrap';
import Swal from 'sweetalert2';
import { authService } from '../auth/auth-service.js';
import { faultsApi } from '../api/faults-api.js';
import { renderNavigation } from '../ui/navigation.js';
import { translateDisplayText, translateDisplayValue } from '../ui/turkish-display.js';
import '../styles/app.css';

// Sayfa ve toplam sayfa bilgileri server-side sayfalama boyunca korunur.
const state = { page: 1, pageSize: 100, totalPages: 1 };

// Arıza tarihleri kurumun kullandığı Türkçe tarih-saat biçiminde gösterilir.
const dateFormatter = new Intl.DateTimeFormat('tr-TR', { dateStyle: 'short', timeStyle: 'short' });
const numberFormatter = new Intl.NumberFormat('tr-TR');
const detailModal = new Modal(document.querySelector('#fault-detail-modal'));
const createModal = new Modal(document.querySelector('#create-fault-modal'));

// Formun hangi araç ve görev sürücüsüyle çalıştığı API isteğine kadar bellekte tutulur.
let vehicleContext = null;
let resourceCandidates = null;
let createReferencesLoaded = false;
let currentUser = null;
let faultStatuses = [];
let rootCauses = [];

// null veya geçersiz tarih değerlerinin ekranda hata üretmesini engeller.
function formatDate(value) {
  if (!value) return '-';
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? '-' : dateFormatter.format(date);
}

// Dosya boyutunu personelin okuyabileceği KB/MB biçimine dönüştürür.
function formatFileSize(bytes) {
  if (!Number.isFinite(bytes) || bytes < 0) return '-';
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 ** 2) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / 1024 ** 2).toFixed(1)} MB`;
}

// data-roles ile tanımlanan menü izinlerini oturumdaki role göre uygular.
function applyRoleMenu(role) {
  document.querySelectorAll('[data-roles]').forEach((element) => {
    const roles = element.dataset.roles.split(',').map((item) => item.trim());
    element.classList.toggle('d-none', !roles.includes(role));
  });
}

// Dinamik tablolara innerHTML kullanmadan güvenli metin hücresi ekler.
function appendCell(row, value, className = '') {
  const cell = document.createElement('td');
  cell.textContent = value ?? '-';
  if (className) cell.className = className;
  row.appendChild(cell);
  return cell;
}

// Durum metnini semantik Bootstrap rengine çevirir; bilinmeyen durumlar gri gösterilir.
function statusColor(status) {
  const text = (status ?? '').toLocaleLowerCase('tr-TR');
  if (text.includes('kapat') || text.includes('çözül') || text.includes('tamam')) return 'success';
  if (text.includes('bekl') || text.includes('gönder')) return 'warning';
  if (text.includes('iptal') || text.includes('başarısız')) return 'secondary';
  if (text.includes('ekip') || text.includes('işlem') || text.includes('tamir')) return 'primary';
  return 'danger';
}

// Liste ve detay ekranlarında ortak kullanılan renkli durum rozetini oluşturur.
function createBadge(text, color = 'secondary') {
  const badge = document.createElement('span');
  badge.className = `badge text-bg-${color}`;
  badge.textContent = text ?? '-';
  return badge;
}

// Backend'in kaynak kodlarını personelin anlayacağı Türkçe adlara dönüştürür.
function resourceName(type) {
  const names = {
    TOW_TRUCK: 'Çekici',
    SERVICE_VEHICLE: 'Hizmet aracı',
    REPLACEMENT_VEHICLE: 'Yedek araç',
    REPLACEMENT_DRIVER: 'Yedek sürücü',
  };
  return names[type] ?? translateDisplayValue(type);
}

// Sayfalı API cevabını arıza tablosuna ve gezinme kontrollerine aktarır.
function renderFaults(result) {
  const body = document.querySelector('#faults-body');
  body.replaceChildren();

  if (!result.items.length) {
    const row = document.createElement('tr');
    const cell = document.createElement('td');
    cell.colSpan = 9;
    cell.className = 'text-center text-secondary py-5';
    cell.textContent = 'Filtreye uygun arıza kaydı bulunamadı.';
    row.appendChild(cell);
    body.appendChild(row);
  }

  result.items.forEach((fault) => {
    const row = document.createElement('tr');
    appendCell(row, fault.faultNumber, 'fw-semibold text-nowrap');

    // Kapı numarası ve plaka aynı hücrede iki seviyeli bilgi olarak gösterilir.
    const vehicleCell = appendCell(row, '');
    const doorNumber = document.createElement('div');
    doorNumber.className = 'fw-semibold';
    doorNumber.textContent = fault.vehicle;
    const plate = document.createElement('div');
    plate.className = 'small text-secondary';
    plate.textContent = fault.plate;
    vehicleCell.append(doorNumber, plate);

    appendCell(row, fault.garage);
    appendCell(row, fault.driver);
    appendCell(row, fault.category);
    const statusCell = appendCell(row, '');
    statusCell.appendChild(createBadge(translateDisplayValue(fault.status), statusColor(fault.status)));
    appendCell(row, fault.team ?? 'Atama bekliyor');
    appendCell(row, formatDate(fault.occurredAt), 'text-nowrap');

    const actionCell = appendCell(row, '', 'text-end');
    const detailButton = document.createElement('button');
    detailButton.type = 'button';
    // API'nin eski bir sürümü statusCode/closedAt alanlarını göndermese bile ekranda
    // görünen nihai durum adından kaydın salt okunur olduğu anlaşılabilmelidir.
    const normalizedStatusCode = String(fault.statusCode ?? '').trim().toUpperCase();
    const normalizedStatusName = String(fault.status ?? '').trim().toLocaleUpperCase('tr-TR');
    const isClosed = Boolean(fault.closedAt)
      || ['CLOSED', 'CANCELLED'].includes(normalizedStatusCode)
      || ['KAPATILDI', 'İPTAL EDİLDİ', 'IPTAL EDILDI'].includes(normalizedStatusName);
    detailButton.className = `btn btn-${isClosed ? 'outline-secondary' : 'outline-primary'} btn-sm`;
    detailButton.dataset.faultId = fault.id;
    detailButton.innerHTML = isClosed
      ? '<i class="bi bi-eye me-1"></i>Detay'
      : '<i class="bi bi-pencil-square me-1"></i>Güncelle';
    actionCell.appendChild(detailButton);
    body.appendChild(row);
  });

  state.page = result.page;
  state.totalPages = Math.max(1, result.totalPages);
  document.querySelector('#fault-count').textContent = `${numberFormatter.format(result.totalCount)} kayıt`;
  document.querySelector('#fault-page-summary').textContent = `Sayfa ${result.page} / ${state.totalPages} · Sayfada ${result.items.length} arıza`;
  document.querySelector('#fault-previous-page').disabled = result.page <= 1;
  document.querySelector('#fault-next-page').disabled = result.page >= state.totalPages;
}

// Formda seçilen değerleri backend liste endpointinin beklediği nesneye dönüştürür.
function currentFilters() {
  return {
    page: state.page,
    pageSize: state.pageSize,
    search: document.querySelector('#fault-search').value.trim(),
    statusId: document.querySelector('#status-filter').value,
  };
}

// Arıza listesinin yükleniyor, başarılı ve hatalı görünüm durumlarını yönetir.
async function loadFaults() {
  const loading = document.querySelector('#faults-loading');
  const table = document.querySelector('#faults-table-container');
  const errorBox = document.querySelector('#faults-error');
  loading.classList.remove('d-none');
  table.classList.add('d-none');
  errorBox.classList.add('d-none');

  try {
    const result = await faultsApi.getPage(currentFilters());
    renderFaults(result);
    table.classList.remove('d-none');
  } catch (error) {
    errorBox.textContent = error.message;
    errorBox.classList.remove('d-none');
  } finally {
    loading.classList.add('d-none');
  }
}

// Referans tablosundaki aktif arıza durumları filtre seçim kutusuna eklenir.
async function loadStatuses() {
  const statuses = await faultsApi.getStatuses();
  // Aynı durum listesi filtre ve merkez durum güncelleme formunda yeniden kullanılır.
  faultStatuses = statuses;
  const select = document.querySelector('#status-filter');
  statuses.forEach((status) => {
    const option = document.createElement('option');
    option.value = status.id;
    option.textContent = status.name;
    select.appendChild(option);
  });
}

// Garaj teknik raporundaki kök neden listesini gerektiğinde tek sefer yükler.
async function loadRootCauses() {
  if (rootCauses.length) return;
  rootCauses = await faultsApi.getRootCauses();
}

// datetime-local alanına tarayıcının yerel saatini YYYY-MM-DDTHH:mm biçiminde yazar.
function localDateTimeValue(date = new Date()) {
  const local = new Date(date.getTime() - date.getTimezoneOffset() * 60_000);
  return local.toISOString().slice(0, 16);
}

// Ana kategori adlarına göre optgroup oluşturarak yalnızca geçerli alt kategorileri seçilebilir yapar.
async function loadFaultCategories() {
  const categories = await faultsApi.getCategories();
  const select = document.querySelector('#fault-category');
  const groups = new Map();

  categories.filter((category) => category.parentCategoryId != null).forEach((category) => {
    const parent = category.parent ?? 'Diğer';
    if (!groups.has(parent)) groups.set(parent, []);
    groups.get(parent).push(category);
  });

  groups.forEach((items, parent) => {
    const group = document.createElement('optgroup');
    group.label = parent;
    items.forEach((category) => {
      const option = document.createElement('option');
      option.value = category.id;
      option.textContent = category.name;
      group.appendChild(option);
    });
    select.appendChild(group);
  });
}

// O anda devam eden görevlerdeki araçları hızlı seçim kutusuna ekler.
async function loadActiveTaskVehicles() {
  const assignments = await faultsApi.getActiveTaskVehicles();
  const select = document.querySelector('#active-task-vehicle');
  const seenVehicleIds = new Set();

  // Modal her açıldığında eski görev seçenekleri tamamen temizlenir. Böylece
  // görevi yedek araca devredilmiş asıl araç seçim kutusunda kalmaz.
  select.replaceChildren(new Option('Aktif görevdeki aracı seçin', ''));

  assignments.forEach((assignment) => {
    // Aynı aracın aynı zaman aralığında birden fazla görev satırı varsa seçim kutusunda tekrarlanmaz.
    if (seenVehicleIds.has(assignment.id)) return;
    seenVehicleIds.add(assignment.id);
    const option = document.createElement('option');
    option.value = assignment.doorNumber;
    option.textContent = `${assignment.doorNumber} · ${assignment.plate} · ${assignment.driver.fullName} · ${assignment.task.taskNumber}`;
    select.appendChild(option);
  });
}

// Kategoriler sabit referans veridir ve bir kez yüklenebilir. Aktif görev araçları
// ise arıza ve görev devriyle değiştiğinden form her açıldığında yeniden istenir.
async function ensureCreateReferences() {
  if (!createReferencesLoaded) {
    await loadFaultCategories();
    createReferencesLoaded = true;
  }

  await loadActiveTaskVehicles();
}

// Araç bağlamı değiştiğinde sürücü seçimini temizleyip görev durumuna göre yeniden oluşturur.
function renderVehicleContext(context) {
  vehicleContext = context;
  const summary = document.querySelector('#fault-vehicle-summary');
  const driverContainer = document.querySelector('#fault-driver-container');
  const driverSelect = document.querySelector('#fault-driver');
  driverSelect.replaceChildren(new Option('Sürücü seçin', ''));

  const vehicle = context.vehicle;
  document.querySelector('#fault-mileage').min = vehicle.currentMileage;
  document.querySelector('#fault-mileage').value = vehicle.currentMileage;

  if (context.activeAssignment) {
    summary.textContent = `${vehicle.doorNumber} · ${vehicle.plate} · ${vehicle.brand} ${vehicle.model} · ${vehicle.garage} · ${context.activeAssignment.fullName} (${context.activeAssignment.personnelNumber}) · Görev: ${context.activeAssignment.taskNumber}`;
    driverContainer.classList.add('d-none');
  } else {
    summary.textContent = `${vehicle.doorNumber} · ${vehicle.plate} · ${vehicle.brand} ${vehicle.model} · ${vehicle.garage} · ${translateDisplayValue(vehicle.status)} · Aktif görev bulunmuyor`;
    context.availableDrivers.forEach((driver) => {
      driverSelect.appendChild(new Option(`${driver.fullName} (${driver.personnelNumber})`, driver.id));
    });
    // Görev dışında sürücü alanının zorunluluğu olay bağlamına göre ayrıca hesaplanır.
    updateNonTaskDriverRequirement();
  }

  summary.classList.remove('d-none');
  // Araç belli olduktan sonra ön değerlendirme değişikliklerinde kullanılacak
  // kaynak listesi arka planda yüklenir; her tikte yeniden API isteği atılmaz.
  resourceCandidates = null;
  if (context.activeAssignment) {
    // Liste isteği tamamlanmadan da manuel kaynak adımı gösterilir.
    // Böylece bağlantı hatası, kullanıcıya "hiçbir şey açılmadı" gibi görünmez.
    renderResourceDecision();
    loadResourceCandidates().catch(showResourceLoadError);
  }
}

function fillResourceSelect(selector, placeholder, items) {
  const select = document.querySelector(selector);
  select.replaceChildren(new Option(placeholder, ''));
  items.forEach((item) => select.appendChild(new Option(`${item.doorNumber} · ${item.plate}`, item.id)));
}

async function loadResourceCandidates() {
  if (!vehicleContext) return;
  const errorBox = document.querySelector('#fault-resource-error');
  errorBox.classList.add('d-none');
  errorBox.textContent = '';
  resourceCandidates = await faultsApi.getResourceCandidates(vehicleContext.vehicle.id);
  fillResourceSelect('#fault-tow-truck', 'Müsait çekici seçin', resourceCandidates.towTrucks ?? []);
  fillResourceSelect('#fault-service-vehicle', 'Müsait hizmet aracı seçin', resourceCandidates.serviceVehicles ?? []);
  fillResourceSelect('#fault-replacement-vehicle', 'Müsait yedek araç seçin', resourceCandidates.replacementVehicles ?? []);
  const driverSelect = document.querySelector('#fault-replacement-driver');
  driverSelect.replaceChildren(new Option('Müsait yedek sürücü seçin', ''));
  (resourceCandidates.reserveDrivers ?? []).forEach((driver) =>
    driverSelect.appendChild(new Option(`${driver.personnelNumber} · ${driver.fullName}`, driver.id)));
  renderResourceDecision();
}

// Kaynak API'si erişilemezse hata gizlenmez; merkez yetkilisi listeyi yeniden
// yükleyebilir ve eksik kaynak seçimiyle kayıt göndermesi engellenir.
function showResourceLoadError(error) {
  const errorBox = document.querySelector('#fault-resource-error');
  errorBox.textContent = error?.message ?? 'Müsait kaynak listeleri alınamadı. Backend sunucusunu kontrol edip yeniden deneyin.';
  errorBox.classList.remove('d-none');
}

// Kullanıcının cevaplarını tek bir karar tablosuna çevirir ve yalnızca
// gereken kaynak satırını açar.
function renderResourceDecision(revealSelection = false) {
  const activeTask = document.querySelector('#fault-mode-task').checked;
  const section = document.querySelector('#fault-resource-section');
  if (!activeTask || !vehicleContext) { section.classList.add('d-none'); return; }
  const immobile = document.querySelector('#fault-mobility').value === 'IMMOBILE';
  const onsite = document.querySelector('#fault-onsite').value;
  const currentTrip = document.querySelector('#fault-current-trip').checked;
  const remainingTasks = document.querySelector('#fault-remaining-tasks').checked;
  const tow = immobile && onsite === 'NO';
  const service = immobile && onsite === 'YES';
  // Aracın hareket durumu saha müdahalesini (çekici/hizmet aracı) belirler.
  // Seferlerin devri ise bundan bağımsızdır: araç mevcut veya kalan görevleri
  // yapamayacaksa yeni araç ile onu kullanacak yedek sürücü mutlaka seçilir.
  const replacement = !currentTrip || !remainingTasks;
  const rules = [
    tow && 'Araç hareket edemiyor ve yerinde müdahale yapılamıyor: çekici seçilmelidir.',
    service && 'Araç hareket edemiyor ancak yerinde müdahale mümkün: teknik ekip için hizmet aracı seçilmelidir.',
    replacement && 'Araç mevcut veya kalan seferlerini tamamlayamıyor: görev devri için yeni araç ve yedek sürücü seçilmelidir.',
  ].filter(Boolean);
  section.classList.remove('d-none');
  document.querySelector('#fault-resource-decision').textContent = rules.length
    ? `${rules.join(' ')} Aşağıdaki listelerden göndereceğiniz kaynakları seçin.`
    : 'Ek kaynak gerekmiyor; araç seferlerine devam edebilir.';
  [['#fault-tow-row','#fault-tow-truck',tow],['#fault-service-row','#fault-service-vehicle',service],['#fault-replacement-row','#fault-replacement-vehicle',replacement]].forEach(([row,select,needed]) => {
    document.querySelector(row).classList.toggle('d-none', !needed);
    document.querySelector(select).required = needed;
    if (!needed) document.querySelector(select).value = '';
  });
  document.querySelector('#fault-replacement-driver').required = replacement;
  if (!replacement) document.querySelector('#fault-replacement-driver').value = '';

  // Karar değiştirildiğinde kaynak alanı modalın altında kaybolmasın;
  // kullanıcı gereken aracı hemen seçebilsin diye ilgili adıma kaydırılır.
  if (revealSelection && (tow || service || replacement)) {
    section.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
  }
}

// Günün kalan görevleri mevcut görevden sonra yapılabileceği için iki karar
// birbirinden bağımsız bırakılamaz. Mevcut görev kaldırılırsa bağlı seçim temizlenir.
function synchronizeTaskContinuationChoices() {
  const currentTrip = document.querySelector('#fault-current-trip');
  const remainingTasks = document.querySelector('#fault-remaining-tasks');
  const continuationFields = document.querySelector('#active-task-assessment-fields');
  const canMove = document.querySelector('#fault-mobility').value === 'MOVABLE';

  // Hareket edemeyen araç hiçbir sefere devam edemeyeceği için seçenekleri
  // yalnızca pasifleştirmek yerine formdan tamamen kaldırıp eski seçimleri temizleriz.
  continuationFields.classList.toggle('d-none', !canMove);
  currentTrip.disabled = !canMove;
  if (!canMove) currentTrip.checked = false;
  remainingTasks.disabled = !canMove || !currentTrip.checked;
  if (!currentTrip.checked) remainingTasks.checked = false;
  document.querySelector('#fault-remaining-help').textContent = currentTrip.checked
    ? 'Seçilirse sistem bugünkü son görevin bitmesini bekler.'
    : 'Önce mevcut seferi tamamlayabilir seçilmelidir.';
}

// Girilen kapı numarasını backend'de doğrular ve araç/sürücü bağlamını forma getirir.
async function loadVehicleContext() {
  const doorNumber = document.querySelector('#fault-door-number').value.trim();
  if (!doorNumber) throw new Error('Önce kapı numarası girin veya görevdeki araçlardan seçim yapın.');
  const context = await faultsApi.getVehicleContext(doorNumber);
  const mode = document.querySelector('input[name="fault-entry-mode"]:checked').value;
  if (mode === 'ACTIVE_TASK' && !context.activeAssignment)
    throw new Error('Bu araç şu anda aktif görevde değil. Görev dışındaki araç seçeneğini kullanın.');
  if (mode === 'NON_TASK' && context.activeAssignment)
    throw new Error('Bu araç şu anda aktif görevde. Aktif görevdeki araç seçeneğini kullanın.');
  renderVehicleContext(context);
}

// Seçim ile araç bağlamı okunması arasında görev bitebilir veya başka araca
// devredilebilir. Böyle bir durumda eski seçeneği ekranda tutmak yerine aktif
// görev listesini hemen yeniler ve kullanıcıyı güncel seçimlere yönlendirir.
async function recoverActiveTaskListAfterContextError(error, errorBox) {
  if (document.querySelector('#fault-mode-task').checked) {
    try {
      await loadActiveTaskVehicles();
      document.querySelector('#fault-door-number').value = '';
      vehicleContext = null;
    } catch {
      // Asıl hata mesajı korunur; yenileme hatası onu gölgelememelidir.
    }
  }

  errorBox.textContent = error.message;
  errorBox.classList.remove('d-none');
}

// Arıza kayıt isteğinin backend CreateFaultRequest DTO'suyla birebir eşleşen gövdesini oluşturur.
function createFaultPayload() {
  if (!vehicleContext) throw new Error('Araç bilgilerini getirmeden arıza kaydı oluşturamazsınız.');

  const currentDoorNumber = document.querySelector('#fault-door-number').value.trim().toLocaleUpperCase('tr-TR');
  if (currentDoorNumber !== vehicleContext.vehicle.doorNumber.toLocaleUpperCase('tr-TR')) {
    throw new Error('Kapı numarası değişti. Güncel araç bilgilerini yeniden getirin.');
  }

  const isActiveTask = document.querySelector('#fault-mode-task').checked;
  const operationContext = isActiveTask ? 'ACTIVE_TASK' : document.querySelector('#fault-operation-context').value;
  const selectedDriverId = Number(document.querySelector('#fault-driver').value) || null;
  const driverId = vehicleContext.activeAssignment?.id ?? selectedDriverId;
  if (!driverId && ['TEST_DRIVE', 'TRANSFER'].includes(operationContext))
    throw new Error('Test sürüşü ve araç transferinde sürücü seçmelisiniz.');
  return {
    doorNumber: vehicleContext.vehicle.doorNumber,
    driverId,
    faultCategoryId: Number(document.querySelector('#fault-category').value),
    description: document.querySelector('#fault-description').value.trim(),
    mileageAtFailure: Number(document.querySelector('#fault-mileage').value),
    // Görev dışı kayıt garajda tespit edilmiş kabul edilir; kullanıcıdan yapay bir konum alınmaz.
    locationDescription: isActiveTask
      ? document.querySelector('#fault-location').value.trim()
      : 'Garaj kontrolünde tespit edildi.',
    occurredAt: new Date(document.querySelector('#fault-occurred-at').value).toISOString(),
    // Garajdaki araç için çekici, hizmet aracı veya yerinde müdahale kararı oluşturulmaz.
    mobilityStatus: isActiveTask ? document.querySelector('#fault-mobility').value : 'MOVABLE',
    onSiteRepairDecision: isActiveTask ? document.querySelector('#fault-onsite').value : 'NO',
    canCompleteCurrentTrip: isActiveTask && document.querySelector('#fault-current-trip').checked,
    canContinueRemainingTasks: isActiveTask && document.querySelector('#fault-remaining-tasks').checked,
    // Yedek araç her zaman kendi yedek sürücüsüyle gönderilir.
    driverCanContinue: false,
    assessmentNote: isActiveTask
      ? document.querySelector('#fault-assessment-note').value.trim()
      : 'Garaj kontrolünde görev dışı araç arızası kaydedildi.',
    operationContext,
    technicianTeamId: Number(document.querySelector('#fault-team').value) || null,
    towTruckId: Number(document.querySelector('#fault-tow-truck').value) || null,
    serviceVehicleId: Number(document.querySelector('#fault-service-vehicle').value) || null,
    replacementVehicleId: Number(document.querySelector('#fault-replacement-vehicle').value) || null,
    replacementDriverId: Number(document.querySelector('#fault-replacement-driver').value) || null,
  };
}

// Backend'in geçmiş performans sıralamasını okunabilir seçeneklere çevirir;
// ilk kayıt önerilir ancak merkez yetkilisi başka müsait ekibi seçebilir.
async function loadTeamRecommendations() {
  if (!vehicleContext) throw new Error('Ekip önerisi için önce aracı getirin.');
  const categoryId = Number(document.querySelector('#fault-category').value);
  if (!categoryId) throw new Error('Ekip önerisi için arıza alt kategorisini seçin.');
  const teams = await faultsApi.getTeamRecommendations(vehicleContext.vehicle.garageId, categoryId);
  const select = document.querySelector('#fault-team');
  select.replaceChildren(new Option('Ekip seçilmezse bekleme sırasına alınır', ''));
  teams.forEach((team) => {
    const average = team.averageMinutes == null ? 'geçmiş kayıt yok' : `ort. ${Math.round(team.averageMinutes)} dk`;
    const option = new Option(`${team.isRecommended ? 'ÖNERİ · ' : ''}${team.name} · ${team.successfulCount}/${team.completedCount} başarı · ${average}`, team.id);
    select.appendChild(option);
    if (team.isRecommended) select.value = String(team.id);
  });
  document.querySelector('#fault-team-help').textContent = teams.length
    ? 'Ekipler geçmiş başarı oranı, ortalama süre ve son atanma zamanına göre sıralandı; atamayı siz seçebilirsiniz.'
    : 'Bu garajda şu anda müsait teknik ekip bulunamadı.';
}

// Test sürüşü/transfer gerçek bir kullanım olduğu için sürücü zorunludur;
// garaj ve servis öncesi kontrolünde arıza anahtarsız kontrolde de bulunabilir.
function updateNonTaskDriverRequirement() {
  const context = document.querySelector('#fault-operation-context').value;
  const required = ['TEST_DRIVE', 'TRANSFER'].includes(context);
  const driver = document.querySelector('#fault-driver');
  if (!document.querySelector('#fault-mode-task').checked && vehicleContext)
    document.querySelector('#fault-driver-container').classList.remove('d-none');
  driver.required = required;
  document.querySelector('#fault-driver-required-label').classList.toggle('d-none', !required);
  document.querySelector('#fault-driver-help').textContent = required
    ? 'Bu durumda aracı fiilen kullanan müsait sürücü zorunludur.'
    : 'Garaj ve servis öncesi kontrolde sürücü isteğe bağlıdır.';
}

// Aktif görev ve görev dışı girişleri aynı formda, birbirine karışmadan yönetilir.
function updateFaultEntryMode() {
  const taskMode = document.querySelector('#fault-mode-task').checked;
  document.querySelector('#active-task-vehicle-container').classList.toggle('d-none', !taskMode);
  // Garajdaki görev dışı araçta operasyon kararı ve konum kullanıcıdan istenmez.
  document.querySelector('#fault-assessment-section').classList.toggle('d-none', !taskMode);
  document.querySelector('#fault-location-container').classList.toggle('d-none', !taskMode);
  document.querySelector('#fault-location').required = taskMode;
  document.querySelector('#fault-assessment-note').required = taskMode;
  document.querySelector('#active-task-vehicle').value = '';
  document.querySelector('#fault-door-number').value = '';
  document.querySelector('#fault-vehicle-summary').classList.add('d-none');
  document.querySelector('#fault-driver-container').classList.add('d-none');
  document.querySelector('#non-task-context-container').classList.toggle('d-none', taskMode);
  // Görev dışı kayıtta sefer/görev kararları yoktur; gizlenen eski seçimler de temizlenir.
  if (!taskMode) {
    document.querySelector('#fault-current-trip').checked = false;
    document.querySelector('#fault-remaining-tasks').checked = false;
  }
  document.querySelector('#fault-entry-help').textContent = taskMode
    ? 'Aktif görev seçildiğinde araç ve sürücü bilgisi otomatik getirilir.'
    : 'Araç garajda görev dışındayken tespit edilen arıza kaydedilir. Konum, sefer ve müdahale kararı istenmez.';
  vehicleContext = null;
  resourceCandidates = null;
  document.querySelector('#fault-resource-section').classList.add('d-none');
}

// Yeni kayıt sonrasında formu ilk açılış durumuna getirir ve eski araç bağlamını temizler.
function resetCreateForm() {
  document.querySelector('#create-fault-form').reset();
  document.querySelector('#fault-occurred-at').value = localDateTimeValue();
  document.querySelector('#fault-mode-task').checked = true;
  updateFaultEntryMode();
  document.querySelector('#fault-vehicle-summary').classList.add('d-none');
  document.querySelector('#fault-driver-container').classList.add('d-none');
  document.querySelector('#create-fault-error').classList.add('d-none');
  vehicleContext = null;
  resourceCandidates = null;
}

// Etiket-değer çiftlerini duyarlı Bootstrap ızgarası biçiminde oluşturur.
function createInfoGrid(values) {
  const grid = document.createElement('div');
  grid.className = 'row g-3 mb-4';

  values.forEach(([label, value]) => {
    const column = document.createElement('div');
    column.className = 'col-12 col-sm-6 col-lg-3';
    const labelElement = document.createElement('div');
    labelElement.className = 'small text-secondary';
    labelElement.textContent = label;
    const valueElement = document.createElement('div');
    valueElement.className = 'fw-semibold';
    valueElement.textContent = value ?? '-';
    column.append(labelElement, valueElement);
    grid.appendChild(column);
  });

  return grid;
}

// Müdahale planındaki boolean kararları Evet/Hayır rozeti olarak sunar.
function decisionItem(label, value) {
  const item = document.createElement('div');
  item.className = 'd-flex justify-content-between align-items-center gap-3 border-bottom py-2';
  const text = document.createElement('span');
  text.textContent = label;
  item.append(text, createBadge(value ? 'Evet' : 'Hayır', value ? 'success' : 'secondary'));
  return item;
}

// Otomatik kaynak kararlarını ve tahmini tamir süresini ayrı bir kartta gösterir.
function createResponsePlan(plan) {
  const section = document.createElement('section');
  section.className = 'card mb-4';
  const header = document.createElement('div');
  header.className = 'card-header fw-semibold';
  header.textContent = 'Müdahale Planı';
  const body = document.createElement('div');
  body.className = 'card-body';

  if (!plan) {
    body.className += ' text-secondary';
    body.textContent = 'Aktif müdahale planı bulunmuyor.';
  } else {
    body.append(
      createInfoGrid([
        ['Hareket durumu', translateDisplayValue(plan.mobilityStatus)],
        ['İşlem durumu', translateDisplayValue(plan.automationStatus)],
        ['Tahmini tamir', plan.plannedRepairMinutes == null ? '-' : `${plan.plannedRepairMinutes} dakika`],
        ['Sonraki işlem', formatDate(plan.nextAutomationAt)],
      ]),
      decisionItem('Mevcut seferi tamamlayabilir', plan.canCompleteCurrentTrip),
      decisionItem('Kalan görevlere devam edebilir', plan.canContinueRemainingTasks),
      decisionItem('Yerinde tamir mümkün', plan.onSiteRepairPossible),
      decisionItem('Çekici gerekli', plan.towRequired),
      decisionItem('Hizmet aracı gerekli', plan.serviceVehicleRequired),
      decisionItem('Yedek araç gerekli', plan.replacementVehicleRequired),
      decisionItem('Sürücü devam edebilir', plan.driverCanContinue),
    );
    if (plan.assessmentNote) {
      const note = document.createElement('div');
      note.className = 'alert alert-light border mt-3 mb-0';
      note.textContent = `Değerlendirme: ${translateDisplayText(plan.assessmentNote)}`;
      body.appendChild(note);
    }
  }

  section.append(header, body);
  return section;
}

// Farklı geçmiş listelerini ortak tablo bileşeniyle ekrana dönüştürür.
function createTableSection(title, headers, items, valueSelector) {
  const section = document.createElement('section');
  section.className = 'card mb-4';
  const header = document.createElement('div');
  header.className = 'card-header fw-semibold';
  header.textContent = `${title} (${items.length})`;
  const body = document.createElement('div');
  body.className = 'card-body p-0';

  if (!items.length) {
    body.className = 'card-body text-secondary';
    body.textContent = 'Kayıt bulunmuyor.';
  } else {
    const wrapper = document.createElement('div');
    wrapper.className = 'table-responsive';
    const table = document.createElement('table');
    table.className = 'table table-sm table-striped align-middle mb-0';
    const head = document.createElement('thead');
    const headRow = document.createElement('tr');
    headers.forEach((text) => {
      const cell = document.createElement('th');
      cell.textContent = text;
      headRow.appendChild(cell);
    });
    head.appendChild(headRow);
    const tableBody = document.createElement('tbody');
    items.forEach((item) => {
      const row = document.createElement('tr');
      valueSelector(item).forEach((value) => appendCell(row, value));
      tableBody.appendChild(row);
    });
    table.append(head, tableBody);
    wrapper.appendChild(table);
    body.appendChild(wrapper);
  }

  section.append(header, body);
  return section;
}

// Arıza fotoğraf ve belgelerini yetkili API indirmesiyle sunan detay kartını oluşturur.
function createAttachmentsSection(attachments) {
  const section = document.createElement('section');
  section.className = 'card mb-4';
  const header = document.createElement('div');
  header.className = 'card-header fw-semibold';
  header.textContent = `Arıza Ekleri (${attachments.length})`;
  const body = document.createElement('div');
  body.className = 'list-group list-group-flush';

  if (!attachments.length) {
    body.className = 'card-body text-secondary';
    body.textContent = 'Fotoğraf veya belge bulunmuyor.';
  } else {
    attachments.forEach((attachment) => {
      const row = document.createElement('div');
      row.className = 'list-group-item d-flex flex-wrap align-items-center justify-content-between gap-3';
      const info = document.createElement('div');
      const name = document.createElement('div');
      name.className = 'fw-semibold';
      name.textContent = attachment.originalFileName;
      const meta = document.createElement('div');
      meta.className = 'small text-secondary';
      meta.textContent = `${attachment.contentType} · ${formatFileSize(attachment.fileSize)} · ${formatDate(attachment.uploadedAt)}`;
      info.append(name, meta);

      const button = document.createElement('button');
      button.type = 'button';
      button.className = 'btn btn-outline-primary btn-sm';
      button.innerHTML = '<i class="bi bi-download me-1"></i>İndir';
      button.addEventListener('click', async () => {
        button.disabled = true;
        try {
          const blob = await faultsApi.downloadAttachment(attachment.id);
          const url = URL.createObjectURL(blob);
          const link = document.createElement('a');
          link.href = url;
          link.download = attachment.originalFileName;
          link.click();
          // Tarayıcı indirmeyi başlattıktan sonra geçici Blob adresi serbest bırakılır.
          window.setTimeout(() => URL.revokeObjectURL(url), 1000);
        } catch (error) {
          await Swal.fire({ icon: 'error', title: 'Dosya indirilemedi', text: error.message });
        } finally {
          button.disabled = false;
        }
      });
      row.append(info, button);
      body.appendChild(row);
    });
  }

  section.append(header, body);
  return section;
}

// Admin ve merkez yetkilisine açıklaması zorunlu arıza durum güncelleme kartı oluşturur.
function createStatusAction(detail) {
  const section = document.createElement('section');
  section.className = 'card border-primary mb-4';
  section.innerHTML = `
    <div class="card-header fw-semibold text-primary">Merkez İşlemi: Durum Güncelle</div>
    <div class="card-body">
      <form class="row g-3 align-items-end" data-status-form>
        <div class="col-12 col-lg-4"><label class="form-label">Yeni durum</label><select class="form-select" data-status-select required><option value="">Durum seçin</option></select></div>
        <div class="col-12 col-lg-6"><label class="form-label">Değişiklik açıklaması</label><input class="form-control" data-status-description maxlength="1000" required placeholder="Durumun neden değiştirildiğini yazın" /></div>
        <div class="col-12 col-lg-2 d-grid"><button class="btn btn-primary" type="submit">Güncelle</button></div>
      </form>
    </div>`;

  const select = section.querySelector('[data-status-select]');
  const allowedCodes = new Set(detail.allowedStatusCodes ?? []);
  const latestReport = detail.reports?.[0];

  // Çözüldü kullanıcı tarafından seçilen bir durum değildir. Bu durum yalnızca
  // kontrol ekranındaki başarılı/koşullu başarılı sonuçtan sonra sistemce atanır.
  // Eski bir backend sürümü RESOLVED kodunu gönderse bile arayüzde gösterilmez.
  allowedCodes.delete('RESOLVED');

  // Teknik rapor çözülemedi diyorsa araç başarılı tamir kontrolüne gönderilemez;
  // merkez yalnızca Çözülemedi akışını seçebilir.
  if (latestReport?.result === 'UNRESOLVED') {
    allowedCodes.delete('WAITING_INSPECTION');
  }

  // Son teknik rapor tamir edildi veya geçici çözüm sonucundaysa merkez
  // aynı kaydı "Çözülemedi" yapamaz. Araç önce kontrol kuyruğuna gönderilir.
  if (latestReport && ['REPAIRED', 'TEMPORARY_REPAIR'].includes(latestReport.result)) {
    allowedCodes.delete('UNRESOLVED');
  }

  // Gerçek bir teknik rapor yoksa rapora bağlı sonuç adımları arayüzden açılmaz.
  if (!latestReport) {
    allowedCodes.delete('WAITING_INSPECTION');
    allowedCodes.delete('UNRESOLVED');
  }

  faultStatuses.filter((status) => allowedCodes.has(status.code)).forEach((status) => {
    // Teknik rapordaki "Tamir Edildi" sonucunun ardından uygulanacak iş adımını
    // kullanıcıya yalnızca durum koduyla değil, sürecin anlamıyla birlikte gösterir.
    const optionLabel = status.code === 'WAITING_INSPECTION' && latestReport?.result === 'REPAIRED'
      ? 'Tamir Edildi – Kontrole Gönder'
      : status.code === 'WAITING_INSPECTION' && latestReport?.result === 'TEMPORARY_REPAIR'
        ? 'Geçici Tamir – Kontrole Gönder'
        : status.name;
    const option = new Option(optionLabel, status.id);
    // Mevcut duruma tekrar geçiş yapılmasını önlemek için aynı seçenek kapatılır.
    option.disabled = status.name === detail.status;
    select.appendChild(option);
  });

  // Geçerli bir sonraki durum yoksa kullanıcıya boş bir form göstermek yerine sürecin
  // tamamlandığı veya başka rolün işlem yapması gerektiği açıklanır.
  if (select.options.length === 1) {
    section.querySelector('[data-status-form]').innerHTML = '<div class="col-12 text-secondary">Bu aşamada merkez tarafından uygulanabilecek yeni bir durum bulunmuyor.</div>';
    return section;
  }

  section.querySelector('[data-status-form]').addEventListener('submit', async (event) => {
    event.preventDefault();
    const button = event.submitter;
    button.disabled = true;
    try {
      await faultsApi.updateStatus(detail.id, {
        statusId: Number(select.value),
        description: section.querySelector('[data-status-description]').value.trim(),
      });
      await Swal.fire({ icon: 'success', title: 'Arıza durumu güncellendi', confirmButtonColor: '#2563eb' });
      await loadFaults();
      await showFaultDetail(detail.id);
    } catch (error) {
      await Swal.fire({ icon: 'error', title: 'Durum güncellenemedi', text: error.message });
    } finally {
      button.disabled = false;
    }
  });

  return section;
}

// Durum geçmişini tek bakışta okunabilen altı aşamalı bir iş akışına dönüştürür.
// Geçmiş kodları kullanıldığı için yeniden açılan arızalarda daha önce tamamlanan aşamalar da korunur.
function createLifecycleTimeline(detail, history) {
  const section = document.createElement('section');
  section.className = 'card mb-4';
  const codes = new Set(history.map((item) => item.statusCode).filter(Boolean));
  codes.add(detail.statusCode);

  const hasFieldResources = detail.responsePlan?.towRequired || detail.responsePlan?.serviceVehicleRequired || detail.responsePlan?.replacementVehicleRequired;
  const stages = [
    { key: 'created', label: 'Arıza oluşturuldu', icon: 'bi-exclamation-diamond', codes: ['OPEN', 'SENT_TO_GARAGE'] },
    ...(hasFieldResources ? [
      { key: 'departing', label: 'Kaynaklar yolda', icon: 'bi-truck', codes: ['RESOURCES_DEPARTING', 'RESOURCES_EN_ROUTE'] },
      { key: 'arrived', label: 'Kaynaklar ulaştı', icon: 'bi-geo-alt', codes: ['RESOURCES_ARRIVED', 'VEHICLE_DELIVERED'] },
    ] : []),
    { key: 'team', label: 'Ekibe atandı', icon: 'bi-people', codes: ['WAITING_TEAM', 'ASSIGNED_TO_TEAM', 'WAITING_REPAIR'] },
    { key: 'repair', label: 'Tamir süreci', icon: 'bi-tools', codes: ['REPAIR_IN_PROGRESS'] },
    { key: 'report', label: 'Teknik rapor', icon: 'bi-file-earmark-text', codes: ['REPORT_SUBMITTED'] },
    { key: 'inspection', label: 'Araç kontrolü', icon: 'bi-clipboard2-check', codes: ['WAITING_INSPECTION'], completed: () => (detail.inspections ?? []).some((item) => ['PASSED', 'CONDITIONAL'].includes(item.result)) },
    { key: 'closed', label: 'Sistem kapattı', icon: 'bi-check2-circle', codes: ['CLOSED'] },
  ];

  // Mevcut aşama, doğrudan durum kodundan veya rapor/kontrol verisinden belirlenir.
  let activeIndex = stages.findIndex((stage) => stage.codes?.includes(detail.statusCode));
  const reportIndex = stages.findIndex((stage) => stage.key === 'report');
  const inspectionIndex = stages.findIndex((stage) => stage.key === 'inspection');
  const closedIndex = stages.findIndex((stage) => stage.key === 'closed');
  if (detail.statusCode === 'RESOLVED' || detail.statusCode === 'UNRESOLVED') activeIndex = closedIndex;
  if ((detail.reports ?? []).length && activeIndex < reportIndex) activeIndex = reportIndex;
  if ((detail.inspections ?? []).length && activeIndex < inspectionIndex) activeIndex = inspectionIndex;

  const body = document.createElement('div');
  body.className = 'card-body';
  const timeline = document.createElement('ol');
  timeline.className = 'fault-lifecycle';

  stages.forEach((stage, index) => {
    const explicitlyCompleted = stage.completed?.() ?? stage.codes?.some((code) => codes.has(code));
    const isCompleted = detail.statusCode === 'CLOSED' || index < activeIndex || explicitlyCompleted;
    const isActive = detail.statusCode !== 'CLOSED' && index === activeIndex;
    const item = document.createElement('li');
    item.className = `fault-lifecycle-step${isCompleted ? ' is-completed' : ''}${isActive ? ' is-active' : ''}`;
    item.innerHTML = `<span class="fault-lifecycle-icon"><i class="bi ${stage.icon}"></i></span><span class="fault-lifecycle-label">${stage.label}</span>`;
    timeline.appendChild(item);
  });

  const header = document.createElement('div');
  header.className = 'card-header fw-semibold';
  header.textContent = 'Arıza Yaşam Döngüsü';
  body.appendChild(timeline);
  section.append(header, body);
  return section;
}

// Oturumdaki role ve mevcut aşamaya göre beklenen işi açıkça belirtir.
function createRoleGuidance(detail, reports, inspections) {
  const section = document.createElement('section');
  section.className = 'alert alert-light border d-flex gap-3 align-items-start mb-4';
  const successfulInspection = inspections.some((item) => ['PASSED', 'CONDITIONAL'].includes(item.result));
  let title = 'Süreç bilgisi';
  let message = 'Bu arıza için şu anda ek bir işlem gerekmiyor.';

  if (detail.statusCode === 'CLOSED') {
    title = 'Süreç tamamlandı';
    message = 'Arıza merkez tarafından kapatılmış ve ayrılan kaynaklar serbest bırakılmıştır.';
  } else if (currentUser?.role === 'Garaj Yetkilisi') {
    title = 'Garaj yetkilisinin sıradaki işlemi';
    if (['RESOURCES_DEPARTING', 'RESOURCES_EN_ROUTE', 'RESOURCES_ARRIVED', 'VEHICLE_DELIVERED', 'ASSIGNED_TO_TEAM', 'WAITING_TEAM'].includes(detail.statusCode)) message = 'Kaynak ve ekip hareketleri devam ediyor. Tamir başlayınca teknik rapor alanı açılacaktır.';
    else if (detail.statusCode === 'REPAIR_IN_PROGRESS') message = 'Teknik ekip çalışmasını doğrulayın ve tamir bittiğinde teknik raporu gönderin.';
    else if (!reports.length) message = 'Teknik ekibin tamire başlaması bekleniyor.';
    else if (!successfulInspection && reports[0]?.result !== 'UNRESOLVED') message = 'Tamir edilen aracı kontrol edin ve kontrol sonucunu kaydedin.';
    else message = 'Garaj işlemleri tamamlandı. Merkez yetkilisinin sonuçlandırması bekleniyor.';
  } else if (currentUser?.role === 'Merkez Yetkilisi') {
    title = 'Merkez yetkilisinin sıradaki işlemi';
    if (!reports.length) message = 'Garajdan teknik rapor gönderilmesi bekleniyor.';
    else if (!successfulInspection && reports[0]?.result !== 'UNRESOLVED') message = 'Garajın başarılı araç kontrolünü kaydetmesi bekleniyor.';
    else message = 'Rapor ve kontrol hazır. Arızayı sonuçlandırıp kapatabilirsiniz.';
  } else if (currentUser?.role === 'Admin') {
    title = 'Yönetici görünümü';
    message = 'Tüm aşamaları izleyebilir ve gerekli olduğunda ilgili rolün işlemini uygulayabilirsiniz.';
  }

  section.innerHTML = `<i class="bi bi-signpost-split fs-4 text-primary"></i><div><div class="fw-semibold">${title}</div><div class="text-secondary">${message}</div></div>`;
  return section;
}

// Garaj veya admin kullanıcısının tamir sonrası araç kontrolünü,
// araç ve arıza numarasını tekrar yazmadan kaydetmesini sağlar.
function createInspectionAction(detail) {
  const section = document.createElement('section');
  section.className = 'card border-success mb-4';
  section.innerHTML = `
    <div class="card-header fw-semibold text-success">Tamir Sonrası Araç Kontrolü</div>
    <div class="card-body">
      <form class="row g-3" data-inspection-form>
        <div class="col-12 col-md-4"><label class="form-label">Kontrol türü</label><select class="form-select" data-inspection-type><option value="POST_REPAIR">Tamir sonrası</option><option value="TEST_DRIVE">Test sürüşü</option><option value="RETURN_TO_SERVICE">Servise dönüş</option></select></div>
        <div class="col-12 col-md-4"><label class="form-label">Sonuç</label><select class="form-select" data-inspection-result><option value="PASSED">Başarılı</option><option value="FAILED">Başarısız</option><option value="CONDITIONAL">Koşullu geçti</option></select></div>
        <div class="col-12 col-md-4"><label class="form-label">Kilometre</label><input class="form-control" data-inspection-odometer type="number" min="${detail.mileageAtFailure}" value="${detail.mileageAtFailure}" required /></div>
        <div class="col-12 col-lg-6"><label class="form-label">Kontrol notları</label><textarea class="form-control" data-inspection-notes rows="2" maxlength="2000" required></textarea></div>
        <div class="col-12 col-lg-4"><label class="form-label">Sonraki işlem</label><textarea class="form-control" data-inspection-next rows="2" maxlength="1000"></textarea></div>
        <div class="col-12 col-lg-2 d-grid align-self-end"><button class="btn btn-success" type="submit">Kontrolü Kaydet</button></div>
      </form>
    </div>`;

  section.querySelector('[data-inspection-form]').addEventListener('submit', async (event) => {
    event.preventDefault();
    const button = event.submitter;
    button.disabled = true;
    try {
      await faultsApi.createInspection({
        vehicleId: detail.vehicle.id,
        faultId: detail.id,
        inspectionType: section.querySelector('[data-inspection-type]').value,
        result: section.querySelector('[data-inspection-result]').value,
        odometer: Number(section.querySelector('[data-inspection-odometer]').value),
        notes: section.querySelector('[data-inspection-notes]').value.trim(),
        nextAction: section.querySelector('[data-inspection-next]').value.trim() || null,
      });
      await Swal.fire({ icon: 'success', title: 'Araç kontrolü kaydedildi', confirmButtonColor: '#198754' });
      await showFaultDetail(detail.id);
    } catch (error) {
      await Swal.fire({ icon: 'error', title: 'Kontrol kaydedilemedi', text: error.message });
    } finally {
      button.disabled = false;
    }
  });
  return section;
}

// Garaj yetkilisinin teknisyenlerden aldığı bilgiyi standart teknik rapora dönüştüren kartı oluşturur.
function createRepairReportAction(detail) {
  const section = document.createElement('section');
  section.className = 'card border-warning mb-4';
  section.innerHTML = `
    <div class="card-header fw-semibold text-warning-emphasis">Garaj İşlemi: Teknik Rapor Gönder</div>
    <div class="card-body">
      <form class="row g-3" data-report-form>
        <div class="col-12 col-md-6 col-lg-3"><label class="form-label">Sonuç</label><select class="form-select" data-report-result required><option value="REPAIRED">Tamir edildi</option><option value="UNRESOLVED">Çözülemedi</option><option value="TEMPORARY_REPAIR">Geçici çözüm uygulandı</option></select></div>
        <div class="col-12 col-md-6 col-lg-3"><label class="form-label">Kök neden</label><select class="form-select" data-root-cause><option value="">Belirlenmedi</option></select></div>
        <div class="col-12 col-md-6 col-lg-3"><label class="form-label">Başlangıç</label><input class="form-control" data-report-start type="datetime-local" required /></div>
        <div class="col-12 col-md-6 col-lg-3"><label class="form-label">Bitiş</label><input class="form-control" data-report-end type="datetime-local" required /></div>
        <div class="col-12"><label class="form-label">Yapılan işlemler ve teknik açıklama</label><textarea class="form-control" data-report-description rows="3" maxlength="4000" required></textarea></div>
        <div class="col-12 col-lg-6"><label class="form-label">Çözüm özeti</label><textarea class="form-control" data-solution-summary rows="2"></textarea></div>
        <div class="col-12 col-lg-6"><label class="form-label">Tekrarı önleme önerisi</label><textarea class="form-control" data-recurrence-prevention rows="2"></textarea></div>
        <div class="col-12 col-lg-8"><div class="form-check"><input class="form-check-input" data-requires-follow-up id="report-follow-up-${detail.id}" type="checkbox"><label class="form-check-label" for="report-follow-up-${detail.id}">Araç için takip kontrolü gerekli</label></div></div>
        <div class="col-12 col-lg-4 d-grid"><button class="btn btn-warning" type="submit">Teknik Raporu Gönder</button></div>
      </form>
    </div>`;

  const rootCauseSelect = section.querySelector('[data-root-cause]');
  rootCauses.forEach((cause) => rootCauseSelect.appendChild(new Option(cause.name, cause.id)));
  section.querySelector('[data-report-start]').value = localDateTimeValue(new Date(Date.now() - 30 * 60_000));
  section.querySelector('[data-report-end]').value = localDateTimeValue();

  section.querySelector('[data-report-form]').addEventListener('submit', async (event) => {
    event.preventDefault();
    const button = event.submitter;
    button.disabled = true;
    try {
      const rootCauseValue = rootCauseSelect.value;
      await faultsApi.createRepairReport(detail.id, {
        result: section.querySelector('[data-report-result]').value,
        description: section.querySelector('[data-report-description]').value.trim(),
        startedAt: new Date(section.querySelector('[data-report-start]').value).toISOString(),
        completedAt: new Date(section.querySelector('[data-report-end]').value).toISOString(),
        rootCauseId: rootCauseValue ? Number(rootCauseValue) : null,
        solutionSummary: section.querySelector('[data-solution-summary]').value.trim() || null,
        recurrencePrevention: section.querySelector('[data-recurrence-prevention]').value.trim() || null,
        requiresFollowUp: section.querySelector('[data-requires-follow-up]').checked,
      });
      await Swal.fire({ icon: 'success', title: 'Teknik rapor merkeze gönderildi', confirmButtonColor: '#2563eb' });
      await showFaultDetail(detail.id);
    } catch (error) {
      await Swal.fire({ icon: 'error', title: 'Rapor gönderilemedi', text: error.message });
    } finally {
      button.disabled = false;
    }
  });

  return section;
}

// Yerinde müdahale başarısız olduğunda süreç kullanıcıdan çekici seçimi bekler.
// Seçim yapılmadan arıza yeniden tamir veya kontrol aşamasına geçirilemez.
function createTowDispatchAction(detail) {
  const section = document.createElement('section');
  section.className = 'card border-danger mb-4';
  section.innerHTML = `<div class="card-header fw-semibold text-danger">Yerinde Müdahale Başarısız: Çekici Seçin</div>
    <div class="card-body"><p class="text-secondary">Araç hareket edemiyor. Garaja alınması için aynı garajdaki müsait çekicilerden birini seçin.</p>
    <form class="row g-3 align-items-end" data-tow-form><div class="col-12 col-lg-9"><label class="form-label">Çekici</label><select class="form-select" data-tow-select required><option value="">Müsait çekiciler yükleniyor...</option></select></div>
    <div class="col-12 col-lg-3 d-grid"><button class="btn btn-danger" type="submit">Çekiciyi Gönder</button></div></form></div>`;
  const select = section.querySelector('[data-tow-select]');
  faultsApi.getResourceCandidates(detail.vehicle.id).then((data) => {
    select.replaceChildren(new Option('Çekici seçin', ''));
    (data.towTrucks ?? []).forEach((item) => select.add(new Option(`${item.doorNumber} · ${item.plate}`, item.id)));
    if (select.options.length === 1) select.add(new Option('Müsait çekici bulunamadı', '', true, true));
  }).catch((error) => { select.replaceChildren(new Option(error.message, '', true, true)); });
  section.querySelector('[data-tow-form]').addEventListener('submit', async (event) => {
    event.preventDefault(); const button = event.submitter; button.disabled = true;
    try { await faultsApi.dispatchTow(detail.id, Number(select.value)); await Swal.fire('Çekici gönderildi', 'Araç garaja alınacak.', 'success'); await openDetail(detail.id); }
    catch (error) { await Swal.fire('Çekici gönderilemedi', error.message, 'error'); button.disabled = false; }
  });
  return section;
}

// Arıza detayını API'den getirerek modal içinde iş akışına göre bölümlere ayırır.
async function showFaultDetail(id) {
  const content = document.querySelector('#fault-detail-content');
  content.innerHTML = '<div class="text-center py-5"><div class="spinner-border text-primary" role="status"></div></div>';
  detailModal.show();

  try {
    // Arıza ayrıntıları tek kaynak üzerinden alınır; detay ekranı harici servise bağımlı değildir.
    const detail = await faultsApi.getById(id);
    document.querySelector('#fault-detail-title').textContent = detail.faultNumber;
    const resources = [...(detail.resources ?? [])];
    const history = [...(detail.history ?? [])];
    const reports = [...(detail.reports ?? [])];
    const attachments = [...(detail.attachments ?? [])];
    const inspections = [...(detail.inspections ?? [])];

    const sections = [
      createLifecycleTimeline(detail, history),
      createRoleGuidance(detail, reports, inspections),
      createInfoGrid([
        ['Araç', `${detail.vehicle.doorNumber} · ${detail.vehicle.plate}`],
        ['Marka / Model', `${detail.vehicle.brand} ${detail.vehicle.model}`],
        ['Garaj', detail.garage],
        ['Sürücü', `${detail.driver.fullName} · ${detail.driver.personnelNumber}`],
        ['Kategori', detail.category],
        ['Durum', translateDisplayValue(detail.status)],
        ['Teknik ekip', detail.team ?? 'Atama bekliyor'],
        ['Arıza zamanı', formatDate(detail.occurredAt)],
        ['Kilometre', numberFormatter.format(detail.mileageAtFailure)],
        ['Konum', detail.locationDescription],
        ['Müdahale son zamanı', formatDate(detail.responseDueAt)],
        ['Çözüm son zamanı', formatDate(detail.resolutionDueAt)],
      ]),
      createTableSection('Arıza Açıklaması', ['Açıklama'], [{ description: detail.description }], (item) => [item.description]),
    ];

    // İşlem kartları yalnızca backend'de aynı yetkiye sahip kullanıcıların ekranına eklenir.
    if (currentUser?.role === 'Admin' || currentUser?.role === 'Merkez Yetkilisi') sections.push(createStatusAction(detail));
    if ((currentUser?.role === 'Admin' || currentUser?.role === 'Garaj Yetkilisi') && detail.statusCode === 'REPAIR_IN_PROGRESS') {
      sections.push(createRepairReportAction(detail));
    }
    if ((currentUser?.role === 'Admin' || currentUser?.role === 'Merkez Yetkilisi') && detail.statusCode === 'TOW_SELECTION_REQUIRED') {
      sections.push(createTowDispatchAction(detail));
    }
    const hasSuccessfulInspection = inspections.some((item) => ['PASSED', 'CONDITIONAL'].includes(item.result));
    const needsInspection = reports.length > 0 && reports[0].result !== 'UNRESOLVED' && !hasSuccessfulInspection && detail.statusCode === 'WAITING_INSPECTION';
    if ((currentUser?.role === 'Admin' || currentUser?.role === 'Garaj Yetkilisi') && needsInspection) {
      sections.push(createInspectionAction(detail));
    }

    sections.push(
      createResponsePlan(detail.responsePlan),
      createAttachmentsSection(attachments),
      createTableSection('Atanan Kaynaklar', ['Kaynak', 'Durum', 'Atanma', 'Tamamlanma'], resources,
        (item) => [resourceName(item.resourceType), translateDisplayValue(item.status), formatDate(item.assignedAt), formatDate(item.completedAt)]),
      createTableSection('Durum Geçmişi', ['Durum', 'Açıklama', 'Tarih', 'İşlemi yapan'], history,
        (item) => [translateDisplayValue(item.status), translateDisplayText(item.description), formatDate(item.changedAt), item.user]),
      createTableSection('Teknik Raporlar', ['Sonuç', 'Açıklama', 'Başlangıç', 'Bitiş', 'Takip'], reports,
        (item) => [translateDisplayValue(item.result), translateDisplayText(item.description), formatDate(item.startedAt), formatDate(item.completedAt), item.requiresFollowUp ? 'Gerekli' : 'Gerekli değil']),
      createTableSection('Araç Kontrolleri', ['Tür', 'Sonuç', 'KM', 'Not', 'Sonraki işlem', 'Tarih'], inspections,
        (item) => [translateDisplayValue(item.inspectionType), translateDisplayValue(item.result), item.odometer ?? '-', translateDisplayText(item.notes), translateDisplayText(item.nextAction), formatDate(item.inspectedAt)]),
    );

    content.replaceChildren(...sections);
  } catch (error) {
    const alert = document.createElement('div');
    alert.className = 'alert alert-danger';
    alert.textContent = error.message;
    content.replaceChildren(alert);
  }
}

// Sayfanın başlangıcında oturum doğrulanır, filtreler ve ilk arıza sayfası yüklenir.
async function initialize() {
  try {
    const user = await authService.requireAuthenticatedUser();
    if (!user) return;
    currentUser = user;
    const garage = user.garageName ? ` · ${user.garageName}` : '';
    document.querySelector('#current-user').textContent = `${user.fullName} · ${user.role}${garage}`;
    applyRoleMenu(user.role);
    renderNavigation('faults', user.role);
    await loadStatuses();
    // Kök neden listesi teknik rapor yazabilen admin ve garaj yetkilisi için yüklenir.
    if (user.role === 'Admin' || user.role === 'Garaj Yetkilisi') await loadRootCauses();
    await loadFaults();

    // Bildirim ekranından faultId ile gelindiyse ilgili arıza detayı doğrudan açılır.
    const requestedFaultId = Number(new URLSearchParams(window.location.search).get('faultId'));
    if (Number.isInteger(requestedFaultId) && requestedFaultId > 0) await showFaultDetail(requestedFaultId);
  } catch (error) {
    const errorBox = document.querySelector('#faults-error');
    errorBox.textContent = error.message;
    errorBox.classList.remove('d-none');
    document.querySelector('#faults-loading').classList.add('d-none');
  }
}

// Filtre değişikliğinde kullanıcı ilk sonuç sayfasına döndürülür.
document.querySelector('#fault-filter-form').addEventListener('submit', (event) => {
  event.preventDefault();
  state.page = 1;
  loadFaults();
});

// Sayfalama düğmeleri geçerli sınırlar içinde yeni API sorgusu çalıştırır.
document.querySelector('#fault-previous-page').addEventListener('click', () => {
  if (state.page > 1) { state.page -= 1; loadFaults(); }
});
document.querySelector('#fault-next-page').addEventListener('click', () => {
  if (state.page < state.totalPages) { state.page += 1; loadFaults(); }
});

// Event delegation, dinamik oluşturulan bütün Detay düğmelerini tek dinleyiciyle yönetir.
document.querySelector('#faults-body').addEventListener('click', (event) => {
  const button = event.target.closest('[data-fault-id]');
  if (button) showFaultDetail(button.dataset.faultId);
});

// Yeni Arıza düğmesi referans verilerini hazırlar ve boş formu modalda açar.
document.querySelector('#open-create-fault').addEventListener('click', async () => {
  const errorBox = document.querySelector('#create-fault-error');
  try {
    await ensureCreateReferences();
    resetCreateForm();
    createModal.show();
  } catch (error) {
    errorBox.textContent = error.message;
    errorBox.classList.remove('d-none');
    createModal.show();
  }
});

// Görevdeki araç seçildiğinde kapı numarası yazılır ve araç bağlamı otomatik yüklenir.
document.querySelector('#active-task-vehicle').addEventListener('change', async (event) => {
  if (!event.target.value) return;
  document.querySelector('#fault-door-number').value = event.target.value;
  try { await loadVehicleContext(); } catch (error) {
    const errorBox = document.querySelector('#create-fault-error');
    await recoverActiveTaskListAfterContextError(error, errorBox);
  }
});

// Kayıt türü değişince önceki araç bağlamı temizlenir; yanlış görev/sürücü eşleşmesi engellenir.
document.querySelectorAll('input[name="fault-entry-mode"]').forEach((input) => input.addEventListener('change', updateFaultEntryMode));
document.querySelector('#fault-operation-context').addEventListener('change', updateNonTaskDriverRequirement);

// Kaynaklar başka bir arızaya atanırsa merkez yetkilisi formu kapatmadan
// güncel müsait araç listesini yeniden isteyebilir.
document.querySelector('#refresh-fault-resources').addEventListener('click', async () => {
  try { await loadResourceCandidates(); }
  catch (error) { showResourceLoadError(error); }
});

// Ön değerlendirmedeki her değişiklik kaynak adımını anında yeniden hesaplar.
['#fault-mobility', '#fault-onsite', '#fault-current-trip', '#fault-remaining-tasks'].forEach((selector) => {
  document.querySelector(selector).addEventListener('change', () => {
    synchronizeTaskContinuationChoices();
    renderResourceDecision(true);
  });
});

// Manuel kapı numarası girişinde kullanıcı Aracı Getir düğmesiyle backend doğrulaması yapar.
document.querySelector('#load-fault-vehicle').addEventListener('click', async () => {
  const errorBox = document.querySelector('#create-fault-error');
  errorBox.classList.add('d-none');
  try { await loadVehicleContext(); } catch (error) {
    await recoverActiveTaskListAfterContextError(error, errorBox);
  }
});

document.querySelector('#load-team-recommendations').addEventListener('click', async () => {
  const errorBox = document.querySelector('#create-fault-error');
  errorBox.classList.add('d-none');
  try { await loadTeamRecommendations(); } catch (error) {
    errorBox.textContent = error.message;
    errorBox.classList.remove('d-none');
  }
});

// Form gönderiminde arıza kaydı oluşturulur, modal kapanır ve liste ilk sayfadan yenilenir.
document.querySelector('#create-fault-form').addEventListener('submit', async (event) => {
  event.preventDefault();
  const button = document.querySelector('#create-fault-button');
  const spinner = document.querySelector('#create-fault-spinner');
  const errorBox = document.querySelector('#create-fault-error');
  errorBox.classList.add('d-none');
  button.disabled = true;
  spinner.classList.remove('d-none');

  try {
    const payload = createFaultPayload();
    // Aktif servis görevi ve görev dışı olaylar farklı backend iş kurallarına gider.
    const result = document.querySelector('#fault-mode-task').checked
      ? await faultsApi.create(payload)
      : await faultsApi.createNonTask(payload);
    // Dosya yükleme ayrı bir HTTP işlemidir. Yükleme başarısız olsa bile oluşan arıza tekrar gönderilmez.
    const attachment = document.querySelector('#fault-attachment').files[0];
    let attachmentWarning = null;
    if (attachment) {
      try {
        await faultsApi.uploadAttachment(result.id, attachment);
      } catch (error) {
        attachmentWarning = error.message;
      }
    }
    createModal.hide();
    state.page = 1;
    await loadFaults();
    await Swal.fire({
      icon: attachmentWarning ? 'warning' : 'success',
      title: attachmentWarning ? 'Arıza oluşturuldu, dosya yüklenemedi' : 'Arıza kaydı oluşturuldu',
      text: attachmentWarning ? `${result.faultNumber} · ${attachmentWarning}` : result.faultNumber,
      confirmButtonColor: '#2563eb',
    });
  } catch (error) {
    errorBox.textContent = error.message;
    errorBox.classList.remove('d-none');
  } finally {
    button.disabled = false;
    spinner.classList.add('d-none');
  }
});

// Çıkıştan önce SweetAlert2 ile yanlış tıklamaya karşı kullanıcı onayı alınır.
document.querySelector('#logout-button').addEventListener('click', async () => {
  const result = await Swal.fire({
    icon: 'question',
    title: 'Çıkış yapılsın mı?',
    showCancelButton: true,
    confirmButtonText: 'Çıkış Yap',
    cancelButtonText: 'Vazgeç',
    confirmButtonColor: '#2563eb',
  });
  if (result.isConfirmed) authService.logout();
});

// DOM olayları bağlandıktan sonra sayfanın ilk veri akışı başlatılır.
initialize();

// Yarı otomatik operasyon adımları kısa zaman aralıklarıyla ilerlediği için liste
// beş saniyede bir sessizce yenilenir; kullanıcı sayfayı elle tazelemeden
// "Kaynaklar yolda", "Tamir devam ediyor" gibi durumları izleyebilir.
window.setInterval(() => {
  if (!document.hidden && !document.querySelector('#create-fault-modal.show')) {
    loadFaults().catch(() => {});
  }
}, 5000);
