import 'bootstrap/dist/js/bootstrap.bundle.min.js';
import 'bootstrap-icons/font/bootstrap-icons.css';
import 'admin-lte/dist/css/adminlte.min.css';
import 'admin-lte';
import { Modal } from 'bootstrap';
import Swal from 'sweetalert2';
import { authService } from '../auth/auth-service.js';
import { usersApi } from '../api/users-api.js';
import { garagesApi } from '../api/garages-api.js';
import { renderNavigation } from '../ui/navigation.js';
import '../styles/app.css';

const dateFormatter = new Intl.DateTimeFormat('tr-TR', { dateStyle: 'short', timeStyle: 'short' });
const userModal = new Modal(document.querySelector('#user-modal'));
let currentUser = null;
let users = [];
let roles = [];
let garages = [];

// Kullanıcı Yönetimi yalnızca uygulamaya giriş yetkisi bulunan hesapları yönetir.
// API eski bir süreçten teknisyen döndürse bile operasyon personeli bu ekranda gösterilmez.
const manageableRoleNames = new Set(['Admin', 'Merkez Yetkilisi', 'Garaj Yetkilisi']);

function formatDate(value) {
  if (!value) return '-';
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? '-' : dateFormatter.format(date);
}

function appendCell(row, value) {
  const cell = document.createElement('td');
  cell.textContent = value ?? '-';
  row.appendChild(cell);
  return cell;
}

function badge(text, color) {
  const element = document.createElement('span');
  element.className = `badge text-bg-${color}`;
  element.textContent = text;
  return element;
}

function isLocked(user) {
  return user.lockedUntil && new Date(user.lockedUntil) > new Date();
}

// Arama ve seçim filtrelerini tarayıcıda uygular; kullanıcı sayısı küçük olduğu için ek API isteği gerekmez.
function filteredUsers() {
  const search = document.querySelector('#user-search').value.trim().toLocaleLowerCase('tr-TR');
  const roleId = document.querySelector('#role-filter').value;
  const active = document.querySelector('#active-filter').value;
  return users.filter((user) => {
    const text = `${user.personnelNumber} ${user.firstName} ${user.lastName} ${user.role.name} ${user.garage ?? ''}`.toLocaleLowerCase('tr-TR');
    if (search && !text.includes(search)) return false;
    if (roleId && String(user.role.id) !== roleId) return false;
    if (active === 'true' && !user.isActive) return false;
    if (active === 'false' && user.isActive) return false;
    if (active === 'locked' && !isLocked(user)) return false;
    return true;
  });
}

function actionButton(icon, title, color, handler, disabled = false) {
  const button = document.createElement('button');
  button.type = 'button';
  button.className = `btn btn-outline-${color} btn-sm`;
  button.title = title;
  button.setAttribute('aria-label', title);
  button.innerHTML = `<i class="bi ${icon}"></i>`;
  button.disabled = disabled;
  button.addEventListener('click', handler);
  return button;
}

function render() {
  const body = document.querySelector('#users-body');
  body.replaceChildren();
  const items = filteredUsers();
  document.querySelector('#user-count').textContent = `${items.length} kayıt`;

  if (!items.length) {
    const row = document.createElement('tr');
    const cell = appendCell(row, 'Filtreye uygun kullanıcı bulunamadı.');
    cell.colSpan = 8;
    cell.className = 'text-center text-secondary py-5';
    body.appendChild(row);
    return;
  }

  items.forEach((user) => {
    const row = document.createElement('tr');
    appendCell(row, user.personnelNumber).className = 'fw-semibold';
    appendCell(row, `${user.firstName} ${user.lastName}`);
    appendCell(row, user.role.name);
    appendCell(row, user.garage ?? 'Merkez');
    const passwordCell = appendCell(row, '');
    passwordCell.appendChild(badge(user.hasPassword ? 'Oluşturuldu' : 'Bekliyor', user.hasPassword ? 'success' : 'secondary'));
    const accountCell = appendCell(row, '');
    accountCell.appendChild(badge(isLocked(user) ? 'Kilitli' : user.isActive ? 'Aktif' : 'Pasif', isLocked(user) ? 'warning' : user.isActive ? 'success' : 'secondary'));
    appendCell(row, formatDate(user.lastLoginAt));
    const actionCell = appendCell(row, '');
    actionCell.className = 'text-end';
    const actions = document.createElement('div');
    actions.className = 'btn-group';
    const protectedAdmin = user.role.name === 'Admin';
    actions.append(
      actionButton('bi-pencil', 'Bilgileri düzenle', 'primary', () => openEdit(user)),
      actionButton(user.isActive ? 'bi-person-dash' : 'bi-person-check', user.isActive ? 'Pasife al' : 'Aktifleştir', user.isActive ? 'danger' : 'success', () => toggleUser(user), protectedAdmin),
      actionButton('bi-unlock', 'Hesap kilidini kaldır', 'warning', () => unlockUser(user), !isLocked(user)),
      actionButton('bi-key', 'Parolayı sıfırla', 'secondary', () => resetPassword(user)),
    );
    actionCell.appendChild(actions);
    body.appendChild(row);
  });
}

function updateGarageVisibility() {
  const role = roles.find((item) => String(item.id) === document.querySelector('#user-role').value);
  const required = role?.name === 'Garaj Yetkilisi';
  document.querySelector('#user-garage-container').classList.toggle('d-none', !required);
  document.querySelector('#user-garage').required = required;
  if (!required) document.querySelector('#user-garage').value = '';
}

function openCreate() {
  document.querySelector('#user-form').reset();
  document.querySelector('#edit-user-id').value = '';
  document.querySelector('#user-modal-title').textContent = 'Yeni Kullanıcı';
  document.querySelector('#first-login-info').classList.remove('d-none');
  document.querySelector('#edit-active-container').classList.add('d-none');
  updateGarageVisibility();
  userModal.show();
}

function openEdit(user) {
  document.querySelector('#user-form').reset();
  document.querySelector('#edit-user-id').value = user.id;
  document.querySelector('#user-modal-title').textContent = `${user.personnelNumber} Kullanıcısını Düzenle`;
  document.querySelector('#first-name').value = user.firstName;
  document.querySelector('#last-name').value = user.lastName;
  document.querySelector('#user-role').value = user.role.id;
  document.querySelector('#gender-code').value = user.genderCode ?? 'U';
  updateGarageVisibility();
  document.querySelector('#user-garage').value = user.garageId ?? '';
  document.querySelector('#edit-is-active').checked = user.isActive;
  document.querySelector('#first-login-info').classList.add('d-none');
  document.querySelector('#edit-active-container').classList.remove('d-none');
  userModal.show();
}

async function reloadUsers() {
  users = (await usersApi.getAll()).filter((user) => manageableRoleNames.has(user.role.name));
  render();
}

async function toggleUser(user) {
  const result = await Swal.fire({ icon: 'question', title: user.isActive ? 'Hesap pasife alınsın mı?' : 'Hesap aktifleştirilsin mi?', text: user.personnelNumber, showCancelButton: true, confirmButtonText: 'Evet', cancelButtonText: 'Vazgeç' });
  if (!result.isConfirmed) return;
  try { await usersApi.toggleActive(user.id); await reloadUsers(); } catch (error) { await Swal.fire({ icon: 'error', title: 'İşlem başarısız', text: error.message }); }
}

async function unlockUser(user) {
  try { await usersApi.unlock(user.id); await reloadUsers(); await Swal.fire({ icon: 'success', title: 'Hesap kilidi kaldırıldı' }); } catch (error) { await Swal.fire({ icon: 'error', title: 'İşlem başarısız', text: error.message }); }
}

async function resetPassword(user) {
  const result = await Swal.fire({ title: `${user.personnelNumber} için yeni parola`, input: 'password', inputAttributes: { minlength: '8', autocomplete: 'new-password' }, inputPlaceholder: 'En az 8 karakter', showCancelButton: true, confirmButtonText: 'Parolayı Sıfırla', cancelButtonText: 'Vazgeç', inputValidator: (value) => (!value || value.length < 8 ? 'Parola en az 8 karakter olmalıdır.' : undefined) });
  if (!result.isConfirmed) return;
  try { await usersApi.resetPassword(user.id, result.value); await Swal.fire({ icon: 'success', title: 'Parola sıfırlandı' }); await reloadUsers(); } catch (error) { await Swal.fire({ icon: 'error', title: 'Parola sıfırlanamadı', text: error.message }); }
}

document.querySelector('#user-form').addEventListener('submit', async (event) => {
  event.preventDefault();
  const id = Number(document.querySelector('#edit-user-id').value) || null;
  const payload = {
    firstName: document.querySelector('#first-name').value.trim(),
    lastName: document.querySelector('#last-name').value.trim(),
    roleId: Number(document.querySelector('#user-role').value),
    garageId: document.querySelector('#user-garage').value ? Number(document.querySelector('#user-garage').value) : null,
    genderCode: document.querySelector('#gender-code').value,
  };
  if (id) payload.isActive = document.querySelector('#edit-is-active').checked;
  const button = event.submitter;
  button.disabled = true;
  try {
    const result = id ? await usersApi.update(id, payload) : await usersApi.create(payload);
    userModal.hide();
    await Swal.fire({ icon: 'success', title: id ? 'Kullanıcı güncellendi' : 'Kullanıcı oluşturuldu', text: result?.personnelNumber ? `Sicil numarası: ${result.personnelNumber}` : undefined });
    await reloadUsers();
  } catch (error) { await Swal.fire({ icon: 'error', title: 'Kullanıcı kaydedilemedi', text: error.message }); } finally { button.disabled = false; }
});

async function initialize() {
  try {
    currentUser = await authService.requireAuthenticatedUser();
    if (!currentUser) return;
    if (currentUser.role !== 'Admin') { window.location.replace('./index.html'); return; }
    document.querySelector('#current-user').textContent = `${currentUser.fullName} · ${currentUser.role}`;
    renderNavigation('users', currentUser.role);
    [users, roles, garages] = await Promise.all([usersApi.getAll(), usersApi.getRoles(), garagesApi.getAll()]);
    // Backend sorgusuna ek olarak görünüm katmanında da yalnızca giriş hesapları korunur.
    users = users.filter((user) => manageableRoleNames.has(user.role.name));
    const roleFilter = document.querySelector('#role-filter');
    const roleSelect = document.querySelector('#user-role');
    roles.forEach((role) => { roleFilter.appendChild(new Option(role.name, role.id)); roleSelect.appendChild(new Option(role.name, role.id)); });
    garages.forEach((garage) => document.querySelector('#user-garage').appendChild(new Option(`${garage.code} · ${garage.name}`, garage.id)));
    render();
    document.querySelector('#table-container').classList.remove('d-none');
  } catch (error) {
    const box = document.querySelector('#page-error'); box.textContent = error.message; box.classList.remove('d-none');
  } finally { document.querySelector('#loading').classList.add('d-none'); }
}

document.querySelector('#create-user-button').addEventListener('click', openCreate);
document.querySelector('#user-role').addEventListener('change', updateGarageVisibility);
document.querySelector('#user-filter-form').addEventListener('submit', (event) => { event.preventDefault(); render(); });
document.querySelector('#logout-button').addEventListener('click', async () => { const result = await Swal.fire({ icon: 'question', title: 'Çıkış yapılsın mı?', showCancelButton: true, confirmButtonText: 'Çıkış Yap', cancelButtonText: 'Vazgeç' }); if (result.isConfirmed) authService.logout(); });

initialize();
