import 'bootstrap/dist/js/bootstrap.bundle.min.js';
import 'bootstrap-icons/font/bootstrap-icons.css';
import 'admin-lte/dist/css/adminlte.min.css';
import 'admin-lte';
import { Modal } from 'bootstrap';
import Swal from 'sweetalert2';
import { authService } from '../auth/auth-service.js';
import { systemApi } from '../api/system-api.js';
import { applicationModules, renderNavigation } from '../ui/navigation.js';
import '../styles/app.css';

let settings = [];
let faultCategories = [];
const categoryModal = new Modal('#category-modal');
const formatter = new Intl.DateTimeFormat('tr-TR', { dateStyle: 'short', timeStyle: 'short' });
const pageAccessSettingKey = 'role_page_access';
// Veri hazırlama damgaları veritabanında korunur ancak kullanıcı ayarı sayılmaz.
const nonUserSettingKeys = new Set([
  'demo_vehicle_data_quality_v1_applied',
  'demo_vehicle_identifiers_v1_applied',
  'service_tow_fixed_model_distribution_v1',
]);

// Ayar anahtarını bulur ve JSON değerini JavaScript türüne dönüştürür.
function settingValue(key, fallback = null) {
  const setting = settings.find((item) => item.settingKey === key);
  if (!setting) return fallback;
  try { return JSON.parse(setting.settingValue); } catch { return fallback; }
}

function settingByKey(key) { return settings.find((item) => item.settingKey === key); }

function fillTypedSettings() {
  document.querySelector('#dispatch-seconds').value = settingValue('presentation_dispatch_seconds', 10);
  document.querySelector('#repair-seconds').value = settingValue('presentation_repair_seconds', 10);
  document.querySelector('#max-inspections').value = settingValue('max_post_repair_inspection_attempts', 3);
  document.querySelector('#alert-hours').value = settingValue('open_fault_alert_hours', 4);
  document.querySelector('#failed-login-limit').value = settingValue('failed_login_limit', 5);
  document.querySelector('#lock-minutes').value = settingValue('account_lock_minutes', 15);
}

function renderOverview() {
  const matrix = settingValue(pageAccessSettingKey, {});
  const last = [...settings].sort((a, b) => new Date(b.updatedAt) - new Date(a.updatedAt))[0];
  document.querySelector('#overview-mode').textContent = 'Yarı otomatik';
  document.querySelector('#overview-access-count').textContent = `${Object.values(matrix).flat().length} izin`;
  document.querySelector('#overview-category-count').textContent = `${faultCategories.filter((item) => item.isActive).length} aktif`;
  document.querySelector('#overview-last-update').textContent = last ? formatter.format(new Date(last.updatedAt)) : '-';
  document.querySelector('#system-setting-count').textContent = settings.filter((item) => !nonUserSettingKeys.has(item.settingKey)).length;
  document.querySelector('#system-last-user').textContent = last?.updatedBy ?? 'Sistem';
  const summaries = [
    ['bi-people', 'Teknik ekip atama', 'Müsait ekip ve FIFO kuyruklu'],
    ['bi-truck', 'Yedek araç atama', 'Arıza kaydında kullanıcı seçer'],
    ['bi-clipboard-check', 'Kontrol denemesi', `En fazla ${settingValue('max_post_repair_inspection_attempts', 3)} başarısız kontrol`],
    ['bi-shield-lock', 'Hesap güvenliği', `${settingValue('failed_login_limit', 5)} hatalı giriş · ${settingValue('account_lock_minutes', 15)} dakika kilit`],
  ];
  const container = document.querySelector('#overview-summary'); container.replaceChildren();
  summaries.forEach(([icon, title, value]) => { const row = document.createElement('div'); row.className = 'd-flex align-items-center gap-3 border rounded p-3'; row.innerHTML = `<span class="settings-icon-sm text-bg-light border"><i class="bi ${icon}"></i></span><div><div class="small text-secondary"></div><strong></strong></div>`; row.querySelector('.small').textContent = title; row.querySelector('strong').textContent = value; container.appendChild(row); });
}

// Sistem artık tek bir yarı otomatik akış kullandığı için eski manuel/sunum
// seçicisini arayüzden kaldırır ve kalan açıklamaları güncel çalışma biçimine uyarlar.
function renderSingleOperationFlow() {
  // Bu davranışlar seçenek değildir: ekip ataması FIFO ile sabittir, yedek
  // araç ise arıza kaydı sırasında kullanıcı tarafından seçilir.
  document.querySelector('#automatic-team')?.closest('.col-md-6')?.remove();
  document.querySelector('#automatic-replacement')?.closest('.col-md-6')?.remove();

  const overviewMode = document.querySelector('#overview-mode');
  const overviewLabel = overviewMode?.previousElementSibling;
  if (overviewLabel) overviewLabel.textContent = 'Operasyon akışı';

  const operationDescription = document.querySelector('#operation-settings-form .card-header .small');
  if (operationDescription) {
    operationDescription.textContent = 'Atama, kaynak hareketi, kontrol ve kapanış davranışları';
  }

  const firstManagementNote = document.querySelector('#overview-pane .col-lg-5 li');
  if (firstManagementNote) {
    firstManagementNote.textContent = 'Kaynak hareketleri yarı otomatik; tamir ve kontrol kararları kullanıcı yönetimindedir.';
  }
}

// JSON olarak saklanan rol-sayfa matrisini anlaşılır onay kutularına dönüştürür.
function renderPageAccess() {
  const setting = settings.find((item) => item.settingKey === pageAccessSettingKey);
  const matrix = setting ? JSON.parse(setting.settingValue) : {};
  const body = document.querySelector('#page-access-body');
  body.replaceChildren();

  let previousGroup = null;
  applicationModules.forEach((module) => {
    if (module.group !== previousGroup) {
      const groupRow = document.createElement('tr'); groupRow.className = 'table-light';
      const groupCell = document.createElement('th'); groupCell.colSpan = 4; groupCell.className = 'small text-uppercase text-secondary py-2'; groupCell.textContent = module.group;
      groupRow.appendChild(groupCell); body.appendChild(groupRow); previousGroup = module.group;
    }
    const row = document.createElement('tr');
    const pageCell = document.createElement('td'); pageCell.innerHTML = `<i class="bi ${module.icon} text-primary me-2"></i>`; pageCell.append(document.createTextNode(module.title));
    row.append(pageCell);

    ['Admin', 'Merkez Yetkilisi', 'Garaj Yetkilisi'].forEach((role) => {
      const cell = document.createElement('td'); cell.className = 'text-center';
      const checkbox = document.createElement('input'); checkbox.type = 'checkbox'; checkbox.className = 'form-check-input';
      checkbox.dataset.role = role; checkbox.dataset.page = module.key;
      checkbox.checked = role === 'Admin' || (matrix[role] ?? []).includes(module.key);
      checkbox.disabled = role === 'Admin' || module.key === 'dashboard';
      checkbox.setAttribute('aria-label', `${role} - ${module.title}`);
      cell.appendChild(checkbox); row.appendChild(cell);
    });
    body.appendChild(row);
  });
}

// Üst ve alt kategorileri tek tabloda, kullanım ve aktiflik bilgileriyle güvenli DOM düğümleri olarak gösterir.
function renderFaultCategories() {
  const body = document.querySelector('#fault-category-body');
  body.replaceChildren();

  faultCategories.forEach((category) => {
    const row = document.createElement('tr');
    const values = category.parentCategoryId == null
      ? [category.name, '-', 'Üst kategori']
      : [category.parentName, category.name, 'Alt kategori'];
    values.forEach((value) => { const cell = document.createElement('td'); cell.textContent = value; row.appendChild(cell); });

    const statusCell = document.createElement('td');
    const status = document.createElement('span');
    status.className = `badge text-bg-${category.isActive ? 'success' : 'secondary'}`;
    status.textContent = category.isActive ? 'Aktif' : 'Pasif';
    statusCell.appendChild(status);

    const usageCell = document.createElement('td');
    usageCell.textContent = category.parentCategoryId == null
      ? `${category.childCount} alt kategori`
      : `${category.faultCount} arıza`;

    const actionCell = document.createElement('td');
    actionCell.className = 'text-end text-nowrap';
    const editButton = document.createElement('button');
    editButton.type = 'button'; editButton.className = 'btn btn-outline-primary btn-sm me-2';
    editButton.dataset.categoryEdit = category.id; editButton.innerHTML = '<i class="bi bi-pencil me-1"></i>Düzenle';
    const toggleButton = document.createElement('button');
    toggleButton.type = 'button';
    toggleButton.className = `btn btn-outline-${category.isActive ? 'danger' : 'success'} btn-sm`;
    toggleButton.dataset.categoryToggle = category.id;
    toggleButton.textContent = category.isActive ? 'Pasife al' : 'Aktifleştir';
    actionCell.append(editButton, toggleButton);
    row.append(statusCell, usageCell, actionCell);
    body.appendChild(row);
  });

  if (!faultCategories.length) {
    body.innerHTML = '<tr><td colspan="6" class="text-center text-secondary py-5">Arıza kategorisi bulunamadı.</td></tr>';
  }
}

// Kategori modalındaki üst kategori seçeneklerini yalnızca aktif kök kategorilerden oluşturur.
function fillParentOptions(selectedId = null) {
  const select = document.querySelector('#category-parent');
  select.replaceChildren(new Option('Üst kategori olarak oluştur', ''));
  faultCategories.filter((item) => item.parentCategoryId == null && item.isActive)
    .forEach((item) => select.appendChild(new Option(item.name, item.id, false, String(item.id) === String(selectedId))));
}

// Kategori listesini backend'den yeniden alarak yapılan değişikliği ekrana yansıtır.
async function reloadFaultCategories() {
  faultCategories = await systemApi.getFaultCategories();
  renderFaultCategories();
}

async function initialize() { try { const user = await authService.requireAuthenticatedUser(); if (!user) return; document.querySelector('#current-user').textContent = `${user.fullName} · ${user.role}`; renderNavigation('system-settings', user.role); const healthPromise = systemApi.getDatabaseHealth().catch(() => ({ database: false })); [settings, faultCategories] = await Promise.all([systemApi.getSettings(), systemApi.getFaultCategories()]); const health = await healthPromise; const statusText = health.database ? 'Bağlı ve çalışıyor' : 'Bağlantı sorunu'; document.querySelector('#database-status').textContent = statusText; document.querySelector('#database-status').className = `fs-4 fw-bold mt-2 text-${health.database ? 'success' : 'danger'}`; renderSingleOperationFlow(); renderPageAccess(); renderFaultCategories(); fillTypedSettings(); renderOverview(); document.querySelector('#settings-content').classList.remove('d-none'); } catch (error) { const box = document.querySelector('#page-error'); box.textContent = error.message; box.classList.remove('d-none'); } finally { document.querySelector('#loading').classList.add('d-none'); } }

async function updateTypedSettings(values) {
  for (const [key, value] of Object.entries(values)) {
    const setting = settingByKey(key);
    if (!setting) throw new Error(`${key} ayarı veritabanında bulunamadı.`);
    await systemApi.updateSetting(setting.id, { settingValue: JSON.stringify(value), description: setting.description, isActive: true });
  }
  settings = await systemApi.getSettings(); fillTypedSettings(); renderOverview();
}

document.querySelector('#operation-settings-form').addEventListener('submit', async (event) => { event.preventDefault(); try { await updateTypedSettings({ presentation_dispatch_seconds: Number(document.querySelector('#dispatch-seconds').value), presentation_repair_seconds: Number(document.querySelector('#repair-seconds').value), max_post_repair_inspection_attempts: Number(document.querySelector('#max-inspections').value), open_fault_alert_hours: Number(document.querySelector('#alert-hours').value) }); await Swal.fire({ icon: 'success', title: 'Operasyon ayarları güncellendi' }); } catch (error) { await Swal.fire({ icon: 'error', title: 'Ayarlar kaydedilemedi', text: error.message }); } });

document.querySelector('#security-settings-form').addEventListener('submit', async (event) => { event.preventDefault(); try { await updateTypedSettings({ failed_login_limit: Number(document.querySelector('#failed-login-limit').value), account_lock_minutes: Number(document.querySelector('#lock-minutes').value) }); await Swal.fire({ icon: 'success', title: 'Güvenlik ayarları güncellendi' }); } catch (error) { await Swal.fire({ icon: 'error', title: 'Ayarlar kaydedilemedi', text: error.message }); } });
document.querySelector('#save-page-access').addEventListener('click', async () => {
  const setting = settings.find((item) => item.settingKey === pageAccessSettingKey);
  if (!setting) return Swal.fire({ icon: 'error', title: 'Yetki ayarı bulunamadı' });
  const matrix = { Admin: applicationModules.map((module) => module.key), 'Merkez Yetkilisi': [], 'Garaj Yetkilisi': [] };
  document.querySelectorAll('#page-access-body input[data-role]:checked').forEach((checkbox) => {
    if (checkbox.dataset.role !== 'Admin') matrix[checkbox.dataset.role].push(checkbox.dataset.page);
  });
  try {
    await systemApi.updateSetting(setting.id, { settingValue: JSON.stringify(matrix), description: setting.description, isActive: true });
    settings = await systemApi.getSettings(); renderPageAccess(); renderOverview();
    await Swal.fire({ icon: 'success', title: 'Sayfa yetkileri güncellendi', text: 'Değişiklikler kullanıcıların sonraki sayfa geçişinde uygulanır.' });
  } catch (error) { await Swal.fire({ icon: 'error', title: 'Yetkiler kaydedilemedi', text: error.message }); }
});

// Yeni kategori düğmesi modalı üst/alt seçimine açık ve varsayılan aktif durumda hazırlar.
document.querySelector('#add-fault-category').addEventListener('click', () => {
  document.querySelector('#category-form').reset();
  document.querySelector('#category-id').value = '';
  document.querySelector('#category-modal-title').textContent = 'Yeni Arıza Kategorisi';
  document.querySelector('#category-active').checked = true;
  document.querySelector('#category-active-row').classList.add('d-none');
  document.querySelector('#category-parent').disabled = false;
  fillParentOptions();
  categoryModal.show();
});

// Düzenleme ve aktiflik düğmeleri tek delegasyon noktasıyla dinamik tablo satırlarında çalışır.
document.querySelector('#fault-category-body').addEventListener('click', async (event) => {
  const editButton = event.target.closest('[data-category-edit]');
  const toggleButton = event.target.closest('[data-category-toggle]');
  const id = editButton?.dataset.categoryEdit ?? toggleButton?.dataset.categoryToggle;
  const category = faultCategories.find((item) => String(item.id) === String(id));
  if (!category) return;

  if (editButton) {
    document.querySelector('#category-id').value = category.id;
    document.querySelector('#category-modal-title').textContent = 'Arıza Kategorisini Düzenle';
    document.querySelector('#category-name').value = category.name;
    document.querySelector('#category-active').checked = category.isActive;
    document.querySelector('#category-active-row').classList.remove('d-none');
    fillParentOptions(category.parentCategoryId);
    // Tarihsel arıza sınıflandırmasının değişmemesi için düzenlemede üst-alt ilişkisi sabit tutulur.
    document.querySelector('#category-parent').disabled = true;
    categoryModal.show();
    return;
  }

  const nextState = !category.isActive;
  const warning = category.parentCategoryId == null && category.isActive
    ? 'Üst kategoriyle birlikte aktif alt kategorileri de pasife alınacaktır.'
    : undefined;
  const confirmation = await Swal.fire({
    icon: 'question',
    title: nextState ? 'Kategori aktifleştirilsin mi?' : 'Kategori pasife alınsın mı?',
    text: warning,
    showCancelButton: true,
    confirmButtonText: nextState ? 'Aktifleştir' : 'Pasife Al',
    cancelButtonText: 'Vazgeç',
  });
  if (!confirmation.isConfirmed) return;

  try {
    await systemApi.updateFaultCategory(category.id, { name: category.name, isActive: nextState });
    await reloadFaultCategories();
    await Swal.fire({ icon: 'success', title: `Kategori ${nextState ? 'aktifleştirildi' : 'pasife alındı'}` });
  } catch (error) {
    await Swal.fire({ icon: 'error', title: 'Kategori durumu değiştirilemedi', text: error.message });
  }
});

// Aynı form, gizli kimlik alanına göre POST ile oluşturma veya PUT ile düzenleme yapar.
document.querySelector('#category-form').addEventListener('submit', async (event) => {
  event.preventDefault();
  const id = document.querySelector('#category-id').value;
  const name = document.querySelector('#category-name').value.trim();

  try {
    if (id) {
      await systemApi.updateFaultCategory(id, {
        name,
        isActive: document.querySelector('#category-active').checked,
      });
    } else {
      const parentValue = document.querySelector('#category-parent').value;
      await systemApi.createFaultCategory({ name, parentCategoryId: parentValue ? Number(parentValue) : null });
    }
    categoryModal.hide();
    await reloadFaultCategories();
    await Swal.fire({ icon: 'success', title: id ? 'Kategori güncellendi' : 'Kategori oluşturuldu' });
  } catch (error) {
    await Swal.fire({ icon: 'error', title: 'Kategori kaydedilemedi', text: error.message });
  }
});

document.querySelector('#logout-button').addEventListener('click', () => authService.logout()); initialize();
