import 'bootstrap/dist/js/bootstrap.bundle.min.js';
import 'bootstrap-icons/font/bootstrap-icons.css';
import 'admin-lte/dist/css/adminlte.min.css';
import 'admin-lte';
import { Modal } from 'bootstrap';
import Swal from 'sweetalert2';
import { authService } from '../auth/auth-service.js';
import { auditLogsApi } from '../api/audit-logs-api.js';
import { renderNavigation } from '../ui/navigation.js';
import { translateDisplayText, translateDisplayValue } from '../ui/turkish-display.js';
import '../styles/app.css';

const dateFormatter = new Intl.DateTimeFormat('tr-TR', { dateStyle: 'short', timeStyle: 'medium' });
const detailModal = new Modal(document.querySelector('#audit-detail-modal'));
const state = { page: 1, pageSize: 50, totalPages: 1, totalCount: 0, items: [] };

// Teknik tablo adları yerine kurumun iş alanlarını gösteren sade filtre seçenekleri.
const recordTypes = {
  VEHICLE: 'Araç', FAULT: 'Arıza', USER: 'Kullanıcı', DRIVER: 'Sürücü', GARAGE: 'Garaj',
  TECHNICAL_TEAM: 'Teknik ekip', TASK: 'Görev', PERSONNEL_INCIDENT: 'Personel olayı',
  OPERATIONAL_EVENT: 'Operasyon olayı', FAULT_CATEGORY: 'Arıza kategorisi',
  SOLUTION: 'Çözüm kütüphanesi', SYSTEM: 'Sistem',
};

const actionNames = {
  AI_SUGGESTION_REVIEWED: 'Çözüm önerisi incelendi', ARV_HALF_DEACTIVATION_COMPLETED: 'Arnavutköy yedek filosunun yarısı pasife alındı',
  BULK_DATA_ENRICHMENT: 'Toplu veri zenginleştirmesi tamamlandı', BULK_IDENTIFIER_NORMALIZATION: 'Toplu araç kodu düzenlemesi tamamlandı',
  DATABASE_BACKEND_FINALIZATION: 'Veritabanı ve backend sonlandırma işlemi tamamlandı', DEACTIVATE: 'Kayıt pasife alındı',
  DRIVER_ACTIVATED: 'Sürücü aktifleştirildi', DRIVER_DEACTIVATED: 'Sürücü pasife alındı', DRIVER_UPDATED: 'Sürücü güncellendi',
  DUPLICATE_GARAGE_MANAGER_DEACTIVATED: 'Mükerrer garaj yetkilisi pasife alındı', PASSWORD_CHANGED: 'Parola değiştirildi',
  USER_CREATED: 'Kullanıcı oluşturuldu', USER_UPDATED: 'Kullanıcı güncellendi', USER_ACTIVATED: 'Kullanıcı aktifleştirildi',
  USER_DEACTIVATED: 'Kullanıcı pasife alındı', USER_UNLOCKED: 'Hesap kilidi kaldırıldı', USER_PASSWORD_RESET: 'Parola sıfırlandı',
  USER_ACCOUNT_UNLOCKED: 'Kullanıcı hesap kilidi kaldırıldı', USER_REGISTERED: 'Kullanıcı kaydı tamamlandı',
  FAULT_STATUS_CHANGED: 'Arıza durumu değiştirildi', REPAIR_REPORT_SUBMITTED: 'Teknik rapor gönderildi',
  FAULT_CREATED: 'Arıza kaydı oluşturuldu', FAULT_AUTOMATION_COMPLETED: 'Yarı otomatik arıza akışı tamamlandı',
  FAULT_AUTOMATION_REPAIR_STARTED: 'Arıza tamir süreci başlatıldı', FAULT_BULK_CLOSED: 'Arıza kayıtları toplu kapatıldı',
  FAULT_REPAIRED_INSPECTED_CLOSED: 'Arıza tamir ve kontrol sonrası kapatıldı',
  VEHICLE_INSPECTION_CREATED: 'Araç kontrolü oluşturuldu', PERSONNEL_INCIDENT_CREATED: 'Personel olayı oluşturuldu',
  PERSONNEL_REPORT_SUBMITTED: 'Personel raporu gönderildi', PERSONNEL_INCIDENT_DISPATCHED: 'Personel olayına kaynak gönderildi',
  PERSONNEL_INCIDENT_RESOLVED: 'Personel olayı sonuçlandırıldı', DRIVER_ABSENCE_ENDED: 'Sürücü izin/rapor süresi tamamlandı',
  DRIVER_STALE_LEAVE_RECONCILED: 'Sürücü uygunluk durumu düzeltildi', FAULT_TEAM_QUEUE_ASSIGNED: 'Arıza sıradaki ekibe atandı',
  FAULT_AUTOMATION_DISPATCHED: 'Arıza kaynakları yola çıkarıldı', FAULT_AUTOMATICALLY_CLOSED: 'Arıza otomatik kapatıldı',
  FAULT_AUTOMATION_WAITING_INSPECTION: 'Arıza kontrol sırasına alındı', FAULT_AUTOMATION_UNRESOLVED: 'Arıza çözülemedi',
  FAULT_CLOSED_AFTER_INSPECTION_FAILURE: 'Başarısız kontrollerden sonra arıza kapatıldı',
  FAULT_CATEGORY_CREATED: 'Arıza kategorisi oluşturuldu', FAULT_CATEGORY_UPDATED: 'Arıza kategorisi güncellendi',
  SERVICE_TOW_MODEL_FINALIZATION: 'Hizmet aracı ve çekici modelleri düzenlendi', SOLUTION_LIBRARY_SEEDED: 'Çözüm kütüphanesi oluşturuldu',
  TECHNICIAN_CREATED: 'Teknisyen oluşturuldu', TECHNICIAN_UPDATED: 'Teknisyen güncellendi',
  OPERATIONAL_EVENT_UPDATED: 'Operasyon olayı güncellendi', OPERATIONAL_EVENT_AUTO_CLOSED: 'Operasyon olayı otomatik kapatıldı',
  SYSTEM_SETTING_UPDATED: 'Sistem ayarı güncellendi',
  UPDATE: 'Kayıt güncellendi', DELETE: 'Kayıt silindi',
};

// Veritabanı tablo adları arayüzde kurum personelinin anlayacağı kayıt türleriyle gösterilir.
const entityNames = {
  app_users: 'Kullanıcı', faults: 'Arıza', vehicles: 'Araç', drivers: 'Sürücü',
  Fault: 'Arıza', Vehicle: 'Araç', Garage: 'Garaj',
  fault_categories: 'Arıza kategorisi', vehicle_inspections: 'Araç kontrolü',
  personnel_incidents: 'Personel olayı', system_settings: 'Sistem ayarı',
  operational_events: 'Operasyon olayı',
  fault_assignments: 'Arıza ekip ataması', fault_resource_assignments: 'Arıza kaynak ataması',
  repair_reports: 'Teknik rapor', technician_teams: 'Teknik ekip', garages: 'Garaj',
  team_members: 'Teknik ekip üyesi', task_assignments: 'Görev ataması', solution_articles: 'Çözüm kaydı',
  ai_suggestions: 'Çözüm önerisi', database_schema: 'Veritabanı şeması',
};

// Eski/yeni değer karşılaştırmasındaki C# özellik adlarını Türkçeleştirir.
const fieldNames = {
  PersonnelNumber: 'Sicil numarası', FirstName: 'Ad', LastName: 'Soyad', RoleId: 'Rol numarası',
  GarageId: 'Garaj numarası', IsActive: 'Aktif mi?', LockedUntil: 'Kilit bitiş zamanı', PasswordReset: 'Parola sıfırlandı mı?',
  FaultStatusId: 'Arıza durumu numarası', StatusId: 'Durum numarası', Status: 'Durum', Result: 'Sonuç',
  TeamId: 'Ekip numarası', VehicleId: 'Araç numarası', DriverId: 'Sürücü numarası', ReplacementDriverId: 'Yedek sürücü numarası',
  EventNumber: 'Olay numarası', FaultNumber: 'Arıza numarası', TransferredTaskCount: 'Devredilen görev sayısı',
  SettingKey: 'Ayar kodu', SettingValue: 'Ayar değeri', Description: 'Açıklama', Name: 'Ad', ParentCategoryId: 'Üst kategori numarası',
  ResponseSlaMinutes: 'Müdahale hedefi (dakika)', ResolutionSlaMinutes: 'Çözüm hedefi (dakika)',
  EstimatedRepairMinutes: 'Tahmini tamir süresi (dakika)', OnsiteRepairMinutes: 'Yerinde tamir süresi (dakika)',
  AutoRepairResult: 'Varsayılan tamir sonucu', Code: 'Kod', IsSystemAction: 'Sistem işlemi mi?',
};

// Teknik enum ve boolean değerleri yalnızca ekranda Türkçe karşılığa dönüştürülür.
const valueNames = {
  true: 'Evet', false: 'Hayır', null: 'Boş', ACTIVE: 'Aktif', INACTIVE: 'Pasif',
  AVAILABLE: 'Müsait', ON_DUTY: 'Görevde', ON_LEAVE: 'İzinli / raporlu',
  REPAIRED: 'Tamir edildi', UNRESOLVED: 'Çözülemedi', PASSED: 'Başarılı', FAILED: 'Başarısız',
  PENDING: 'Bekliyor', DISPATCHED: 'Gönderildi', COMPLETED: 'Tamamlandı', CANCELLED: 'İptal edildi',
  WAITING_REPLACEMENT: 'Yedek kaynak bekliyor', MANUAL: 'Yarı otomatik',
  ORIGINAL: 'İlk atama', REPLACEMENT: 'Yedek atama', RETURN: 'İlk atama',
};

function formatDate(value) { const date = new Date(value); return Number.isNaN(date.getTime()) ? '-' : dateFormatter.format(date); }
function appendCell(row, value) { const cell = document.createElement('td'); cell.textContent = value ?? '-'; row.appendChild(cell); return cell; }
// Sözlüğe henüz eklenmemiş yeni backend kodlarının İngilizce olarak arayüze sızmasını önler.
function actionName(code) { return actionNames[code] ?? 'Diğer sistem işlemi'; }
function entityName(code) { return entityNames[code] ?? 'Sistem kaydı'; }

// Eski kayıtların bir kısmı teknik bakım sırasında database_schema adıyla yazılmıştır.
// Action kodunu da değerlendirerek bunları doğru iş alanında (ör. Araç) gösterir.
function recordTypeName(item) {
  const action = item.action ?? '';
  const entity = item.entityType ?? '';
  if (['BULK_DATA_ENRICHMENT', 'BULK_IDENTIFIER_NORMALIZATION', 'ARV_HALF_DEACTIVATION_COMPLETED',
    'SERVICE_TOW_MODEL_FINALIZATION', 'VEHICLE_INSPECTION_CREATED'].includes(action) ||
    ['vehicles', 'Vehicle', 'vehicle_inspections'].includes(entity)) return 'Araç';
  if ((action.startsWith('FAULT_') && !action.startsWith('FAULT_CATEGORY_')) || action === 'REPAIR_REPORT_SUBMITTED' ||
    ['faults', 'Fault', 'fault_assignments', 'fault_resource_assignments', 'repair_reports'].includes(entity)) return 'Arıza';
  if (action.startsWith('USER_') || ['PASSWORD_CHANGED', 'DUPLICATE_GARAGE_MANAGER_DEACTIVATED'].includes(action) || entity === 'app_users') return 'Kullanıcı';
  if (action.startsWith('DRIVER_') || entity === 'drivers') return 'Sürücü';
  if (action.startsWith('TECHNICIAN_') || ['technician_teams', 'team_members'].includes(entity)) return 'Teknik ekip';
  if (action.startsWith('PERSONNEL_') || entity === 'personnel_incidents') return 'Personel olayı';
  if (action.startsWith('OPERATIONAL_EVENT_') || entity === 'operational_events') return 'Operasyon olayı';
  if (action.startsWith('FAULT_CATEGORY_') || entity === 'fault_categories') return 'Arıza kategorisi';
  if (['SOLUTION_LIBRARY_SEEDED', 'AI_SUGGESTION_REVIEWED'].includes(action) || ['solution_articles', 'ai_suggestions'].includes(entity)) return 'Çözüm kütüphanesi';
  if (['task_assignments', 'service_tasks'].includes(entity)) return 'Görev';
  if (['garages', 'Garage'].includes(entity)) return 'Garaj';
  return entityName(entity);
}
function translateDescription(value) {
  if (!value) return '-';
  const replacements = {
    Vehicle: 'Araç', Driver: 'Sürücü', Fault: 'Arıza', Garage: 'Garaj', AppUser: 'Kullanıcı',
    TechnicianTeam: 'Teknik ekip', TeamMember: 'Ekip üyesi', TaskAssignment: 'Görev ataması',
    ServiceTask: 'Sefer görevi',
    RepairReport: 'Teknik rapor', FaultAssignment: 'Arıza ekip ataması',
    FaultResourceAssignment: 'Arıza kaynak ataması', PersonnelIncident: 'Personel olayı',
    SystemSetting: 'Sistem ayarı',
  };
  const translated = Object.entries(replacements).reduce(
    (text, [english, turkish]) => text.replace(new RegExp(`\\b${english}\\b`, 'gi'), turkish), String(value));
  const phraseReplacements = {
    'record updated': 'kaydı güncellendi', 'record created': 'kaydı oluşturuldu',
    'record deactivated': 'kaydı pasife alındı', 'record activated': 'kaydı aktifleştirildi',
    'successfully completed': 'başarıyla tamamlandı',
  };
  const description = Object.entries(phraseReplacements).reduce(
    (text, [english, turkish]) => text.replace(new RegExp(english, 'gi'), turkish), translated);
  return translateDisplayText(description);
}
function fieldName(key) {
  return key.split('.').map((part) => fieldNames[part] ?? 'Diğer alan').join(' / ');
}
function displayValue(value) {
  if (value === undefined) return '-';
  if (value === null) return 'Boş';
  const mapped = valueNames[String(value)];
  if (mapped) return mapped;
  if (typeof value === 'string' && /^\d{4}-\d{2}-\d{2}T/.test(value)) return formatDate(value);
  return translateDisplayValue(value);
}

function parseJson(value) {
  if (!value) return {};
  try { return typeof value === 'string' ? JSON.parse(value) : value; } catch { return { value }; }
}

// İç içe JSON alanlarını noktalı anahtarlarla düzleştirerek eski/yeni değer karşılaştırmasını kolaylaştırır.
function flatten(value, prefix = '', target = {}) {
  if (value && typeof value === 'object' && !Array.isArray(value)) {
    Object.entries(value).forEach(([key, child]) => flatten(child, prefix ? `${prefix}.${key}` : key, target));
  } else target[prefix || 'değer'] = Array.isArray(value) ? JSON.stringify(value) : value;
  return target;
}

function recordLink(item) {
  if (!item.entityId) return null;
  if (item.entityType === 'faults') return `./faults.html?faultId=${item.entityId}`;
  if (item.entityType === 'app_users') return './users.html';
  if (item.entityType === 'vehicles') return `./vehicles.html?vehicleId=${item.entityId}`;
  return null;
}

function showDetail(item) {
  document.querySelector('#audit-detail-title').textContent = `#${item.id} · ${actionName(item.action)}`;
  const content = document.querySelector('#audit-detail-content');
  content.replaceChildren();
  const summary = document.createElement('dl');
  summary.className = 'row mb-4';
  [['Tarih', formatDate(item.createdAt)], ['Kullanıcı', item.user ? `${item.user.fullName} · ${item.user.personnelNumber}` : 'Sistem'], ['Rol', item.role ?? '-'], ['Kayıt türü', `${recordTypeName(item)}${item.entityId ? ` #${item.entityId}` : ''}`], ['Açıklama', translateDescription(item.description)], ['IP adresi', item.ipAddress ?? '-']].forEach(([label, value]) => { const dt = document.createElement('dt'); dt.className = 'col-sm-3'; dt.textContent = label; const dd = document.createElement('dd'); dd.className = 'col-sm-9'; dd.textContent = value; summary.append(dt, dd); });
  content.appendChild(summary);

  const oldValues = flatten(parseJson(item.oldValues));
  const newValues = flatten(parseJson(item.newValues));
  const keys = [...new Set([...Object.keys(oldValues), ...Object.keys(newValues)])].sort();
  const heading = document.createElement('h3'); heading.className = 'h6'; heading.textContent = 'Değişen Değerler'; content.appendChild(heading);
  if (!keys.length) { const empty = document.createElement('div'); empty.className = 'alert alert-light border'; empty.textContent = 'Bu işlem için eski veya yeni alan değeri kaydedilmemiş.'; content.appendChild(empty); }
  else {
    const wrapper = document.createElement('div'); wrapper.className = 'table-responsive'; const table = document.createElement('table'); table.className = 'table table-bordered table-sm'; table.innerHTML = '<thead><tr><th>Alan</th><th>Eski değer</th><th>Yeni değer</th></tr></thead>'; const body = document.createElement('tbody');
    keys.forEach((key) => { const row = document.createElement('tr'); appendCell(row, fieldName(key)).className = 'fw-semibold'; appendCell(row, displayValue(oldValues[key])); appendCell(row, displayValue(newValues[key])); body.appendChild(row); });
    table.appendChild(body); wrapper.appendChild(table); content.appendChild(wrapper);
  }
  const href = recordLink(item); if (href) { const link = document.createElement('a'); link.className = 'btn btn-outline-primary mt-2'; link.href = href; link.innerHTML = '<i class="bi bi-box-arrow-up-right me-1"></i>İlgili Kayda Git'; content.appendChild(link); }
  detailModal.show();
}

function readFilters() { return { page: state.page, pageSize: state.pageSize, search: document.querySelector('#audit-search').value.trim(), recordType: document.querySelector('#entity-filter').value, userId: document.querySelector('#user-filter').value, startDate: document.querySelector('#start-date').value, endDate: document.querySelector('#end-date').value }; }

function render() {
  const body = document.querySelector('#audit-body'); body.replaceChildren();
  document.querySelector('#audit-count').textContent = `${state.totalCount.toLocaleString('tr-TR')} kayıt`;
  document.querySelector('#page-summary').textContent = `Sayfa ${state.page} / ${Math.max(1, state.totalPages)}`;
  document.querySelector('#previous-page').disabled = state.page <= 1;
  document.querySelector('#next-page').disabled = state.page >= state.totalPages;
  if (!state.items.length) { const row = document.createElement('tr'); const cell = appendCell(row, 'Filtreye uygun işlem kaydı bulunamadı.'); cell.colSpan = 8; cell.className = 'text-center text-secondary py-5'; body.appendChild(row); return; }
  state.items.forEach((item) => { const row = document.createElement('tr'); appendCell(row, formatDate(item.createdAt)); appendCell(row, item.user ? `${item.user.fullName}\n${item.user.personnelNumber}` : 'Sistem').className = 'text-nowrap'; appendCell(row, item.role ?? '-'); appendCell(row, actionName(item.action)); appendCell(row, `${recordTypeName(item)}${item.entityId ? ` #${item.entityId}` : ''}`); appendCell(row, translateDescription(item.description)); appendCell(row, item.ipAddress ?? '-'); const buttonCell = appendCell(row, ''); const button = document.createElement('button'); button.type = 'button'; button.className = 'btn btn-outline-primary btn-sm'; button.innerHTML = '<i class="bi bi-eye me-1"></i>Detay'; button.addEventListener('click', () => showDetail(item)); buttonCell.appendChild(button); body.appendChild(row); });
}

async function load() {
  document.querySelector('#loading').classList.remove('d-none'); document.querySelector('#table-container').classList.add('d-none');
  try { const result = await auditLogsApi.getAll(readFilters()); Object.assign(state, result); render(); document.querySelector('#table-container').classList.remove('d-none'); }
  catch (error) { const box = document.querySelector('#page-error'); box.textContent = error.message; box.classList.remove('d-none'); }
  finally { document.querySelector('#loading').classList.add('d-none'); }
}

async function initialize() {
  const user = await authService.requireAuthenticatedUser(); if (!user) return;
  if (user.role !== 'Admin') { window.location.replace('./index.html'); return; }
  document.querySelector('#current-user').textContent = `${user.fullName} · ${user.role}`; renderNavigation('audit-logs', user.role);
  const filters = await auditLogsApi.getFilters(); Object.entries(recordTypes).forEach(([code, name]) => document.querySelector('#entity-filter').appendChild(new Option(name, code))); filters.users.forEach((value) => document.querySelector('#user-filter').appendChild(new Option(`${value.personnelNumber} · ${value.fullName}`, value.id)));
  await load();
}

document.querySelector('#audit-filter-form').addEventListener('submit', (event) => { event.preventDefault(); state.page = 1; load(); });
document.querySelector('#previous-page').addEventListener('click', () => { if (state.page > 1) { state.page--; load(); } });
document.querySelector('#next-page').addEventListener('click', () => { if (state.page < state.totalPages) { state.page++; load(); } });
document.querySelector('#logout-button').addEventListener('click', async () => { const result = await Swal.fire({ icon: 'question', title: 'Çıkış yapılsın mı?', showCancelButton: true, confirmButtonText: 'Çıkış Yap', cancelButtonText: 'Vazgeç' }); if (result.isConfirmed) authService.logout(); });

initialize();
