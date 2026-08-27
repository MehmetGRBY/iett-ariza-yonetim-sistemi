import 'bootstrap/dist/js/bootstrap.bundle.min.js';
import 'bootstrap-icons/font/bootstrap-icons.css';
import 'admin-lte/dist/css/adminlte.min.css';
import 'admin-lte';
import { Modal } from 'bootstrap';
import Swal from 'sweetalert2';
import { authService } from '../auth/auth-service.js';
import { decisionSupportApi, decisionReferenceApi } from '../api/decision-support-api.js';
import { renderNavigation } from '../ui/navigation.js';
import '../styles/app.css';

let solutions = []; let categories = []; let currentUser = null;
const createModal = new Modal('#create-modal');

// Kategori kimliğini kullanıcıya ana kategori / alt kategori biçiminde gösterir.
function categoryName(id) { const item = categories.find((category) => category.id === id); return item ? `${item.parent ? `${item.parent} / ` : ''}${item.name}` : `Kategori #${id}`; }

// Arama ve kategori filtresini kart listesine, sayaçlara ve ortalama süreye birlikte uygular.
function render() {
  const search = document.querySelector('#search').value.toLocaleLowerCase('tr-TR');
  const categoryId = Number(document.querySelector('#category-filter').value || 0);
  const items = solutions.filter((item) => (!categoryId || item.faultCategoryId === categoryId) && `${item.title} ${item.symptoms} ${item.solutionSteps}`.toLocaleLowerCase('tr-TR').includes(search));
  document.querySelector('#total').textContent = items.length;
  document.querySelector('#approved').textContent = items.filter((item) => item.approvalStatus === 'APPROVED').length;
  document.querySelector('#draft').textContent = items.filter((item) => item.approvalStatus !== 'APPROVED').length;
  const durations = items.map((item) => item.estimatedMinutes).filter(Number.isFinite);
  document.querySelector('#average').textContent = durations.length ? `${Math.round(durations.reduce((sum, value) => sum + value, 0) / durations.length)} dk` : '0 dk';
  const list = document.querySelector('#solution-list'); list.replaceChildren();
  items.forEach((item) => {
    const column = document.createElement('div'); column.className = 'col-12 col-xl-6';
    column.innerHTML = `<article class="card shadow-sm h-100"><div class="card-header d-flex justify-content-between gap-3"><div><h2 class="h5 mb-1"></h2><small class="text-secondary"></small></div><span class="badge align-self-start text-bg-${item.approvalStatus === 'APPROVED' ? 'success' : 'warning'}">${item.approvalStatus === 'APPROVED' ? 'Onaylı' : 'Taslak'}</span></div><div class="card-body"><h3 class="h6 text-secondary">Belirtiler</h3><p data-symptoms></p><h3 class="h6 text-secondary">Çözüm adımları</h3><p class="mb-3 text-break" data-steps></p>${item.safetyNotes ? '<div class="alert alert-warning py-2 mb-3" data-safety></div>' : ''}<div class="d-flex justify-content-between align-items-center"><span class="small text-secondary">Tahmini süre: ${item.estimatedMinutes ?? '-'} dk</span>${currentUser.role === 'Admin' && item.approvalStatus !== 'APPROVED' ? `<button class="btn btn-success btn-sm" data-approve="${item.id}">Onayla</button>` : ''}</div></div></article>`;
    column.querySelector('h2').textContent = item.title;
    column.querySelector('.card-header small').textContent = categoryName(item.faultCategoryId);
    column.querySelector('[data-symptoms]').textContent = item.symptoms;
    column.querySelector('[data-steps]').textContent = item.solutionSteps;
    if (item.safetyNotes) column.querySelector('[data-safety]').textContent = `Güvenlik: ${item.safetyNotes}`;
    list.appendChild(column);
  });
  if (!items.length) list.innerHTML = '<div class="col-12"><div class="card"><div class="card-body text-center text-secondary py-5">Çözüm makalesi bulunamadı.</div></div></div>';
}

async function initialize() {
  try {
    currentUser = await authService.requireAuthenticatedUser(); if (!currentUser) return;
    document.querySelector('#current-user').textContent = `${currentUser.fullName} · ${currentUser.role}`;
    renderNavigation('solutions', currentUser.role);
    if (currentUser.role === 'Merkez Yetkilisi') document.querySelector('#create-button').classList.add('d-none');
    [solutions, categories] = await Promise.all([decisionSupportApi.getSolutions(), decisionReferenceApi.getCategories()]);
    const activeCategories = categories.filter((item) => item.parentCategoryId !== null);
    activeCategories.forEach((item) => { const label = `${item.parent ? `${item.parent} / ` : ''}${item.name}`; document.querySelector('#category-filter').appendChild(new Option(label, item.id)); document.querySelector('#solution-category').appendChild(new Option(label, item.id)); });
    const causes = await decisionReferenceApi.getRootCauses(); causes.forEach((item) => document.querySelector('#root-cause').appendChild(new Option(item.name, item.id)));
    render();
  } catch (error) { const box = document.querySelector('#page-error'); box.textContent = error.message; box.classList.remove('d-none'); }
  finally { document.querySelector('#loading').classList.add('d-none'); }
}

document.querySelector('#search').addEventListener('input', render);
document.querySelector('#category-filter').addEventListener('change', render);
document.querySelector('#create-button').addEventListener('click', () => createModal.show());
document.querySelector('#create-form').addEventListener('submit', async (event) => { event.preventDefault(); const form = event.currentTarget; const raw = Object.fromEntries(new FormData(form)); try { await decisionSupportApi.createSolution({ ...raw, faultCategoryId: Number(raw.faultCategoryId), rootCauseId: raw.rootCauseId ? Number(raw.rootCauseId) : null, sourceRepairReportId: null, estimatedMinutes: raw.estimatedMinutes ? Number(raw.estimatedMinutes) : null }); createModal.hide(); form.reset(); solutions = await decisionSupportApi.getSolutions(); render(); await Swal.fire({ icon: 'success', title: currentUser.role === 'Admin' ? 'Çözüm yayımlandı' : 'Taslak kaydedildi' }); } catch (error) { Swal.fire({ icon: 'error', title: 'Çözüm kaydedilemedi', text: error.message }); } });
document.querySelector('#solution-list').addEventListener('click', async (event) => { const button = event.target.closest('[data-approve]'); if (!button) return; try { await decisionSupportApi.approveSolution(button.dataset.approve); solutions = await decisionSupportApi.getSolutions(); render(); await Swal.fire({ icon: 'success', title: 'Çözüm onaylandı' }); } catch (error) { Swal.fire({ icon: 'error', title: 'Onaylanamadı', text: error.message }); } });
document.querySelector('#logout-button').addEventListener('click', () => authService.logout());
initialize();
