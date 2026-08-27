import 'bootstrap/dist/js/bootstrap.bundle.min.js';
import 'bootstrap-icons/font/bootstrap-icons.css';
import 'admin-lte/dist/css/adminlte.min.css';
import 'admin-lte';
import Swal from 'sweetalert2';
import { authService } from '../auth/auth-service.js';
import { monitoringApi } from '../api/monitoring-api.js';
import { renderNavigation } from '../ui/navigation.js';
import { translateDisplayValue } from '../ui/turkish-display.js';
import '../styles/app.css';

const dateFormatter = new Intl.DateTimeFormat('tr-TR', { dateStyle: 'short', timeStyle: 'short' });

// API tarihlerini kurum içi ekranda tutarlı Türkçe tarih-saat biçiminde gösterir.
function formatDate(value) {
  if (!value) return '-';
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? '-' : dateFormatter.format(date);
}

// Dinamik veriyi innerHTML kullanmadan tablo hücresine yazar.
function appendCell(row, value) {
  const cell = document.createElement('td');
  cell.textContent = value ?? '-';
  row.appendChild(cell);
  return cell;
}

function badge(text, color) {
  const element = document.createElement('span');
  element.className = `badge text-bg-${color}`;
  element.textContent = text ?? '-';
  return element;
}

// Sağlık puanı düştükçe daha dikkat çekici renk kullanır.
function healthColor(score) {
  // 0-29 kırmızı, 30-49 turuncu, 50-70 sarı ve 71-100 yeşil gösterilir.
  if (score < 30) return 'danger';
  if (score < 50) return 'orange';
  if (score <= 70) return 'warning';
  return 'success';
}

function renderRecurring(items) {
  const body = document.querySelector('#recurring-body');
  body.replaceChildren();
  items.forEach((item) => {
    const row = document.createElement('tr');
    appendCell(row, `${item.doorNumber} · ${item.plate}`);
    appendCell(row, item.garage);
    appendCell(row, item.category);
    appendCell(row, item.faultCount);
    appendCell(row, formatDate(item.firstFaultAt));
    appendCell(row, formatDate(item.lastFaultAt));
    body.appendChild(row);
  });
  // Boş API cevabında kullanıcıya ekranın bozuk olmadığını açıkça bildirir.
  if (!items.length) body.innerHTML = '<tr><td colspan="6" class="text-center text-secondary py-5">Son 90 günde aynı kategoride tekrarlayan araç arızası bulunamadı.</td></tr>';
}

function renderHealth(items) {
  const body = document.querySelector('#health-body');
  body.replaceChildren();
  items.forEach((item) => {
    const row = document.createElement('tr');
    // Satır başka bir sayfaya yönlenmez; özet ayrıntılar güvenli bir modal içinde açılır.
    row.className = 'cursor-pointer';
    row.style.cursor = 'pointer';
    row.tabIndex = 0;
    row.title = 'Araç sağlık ayrıntısını göster';
    appendCell(row, `${item.doorNumber} · ${item.plate}`);
    appendCell(row, item.garage);
    appendCell(row, translateDisplayValue(item.status));
    const score = appendCell(row, '');
    score.appendChild(badge(`${item.healthScore}/100`, healthColor(item.healthScore)));
    appendCell(row, item.faults30d);
    appendCell(row, item.faults90d);
    appendCell(row, item.failedInspections90d);
    const showDetails = () => Swal.fire({
      icon: item.healthScore < 70 ? 'warning' : 'info',
      title: `${item.doorNumber} · ${item.plate}`,
      html: `<div class="text-start">
        <p><strong>Garaj:</strong> ${escapeHtml(item.garage)}</p>
        <p><strong>Araç durumu:</strong> ${escapeHtml(translateDisplayValue(item.status))}</p>
        <p><strong>Sağlık puanı:</strong> ${item.healthScore}/100</p>
        <p><strong>Son 30 gün arıza:</strong> ${item.faults30d}</p>
        <p><strong>Son 90 gün arıza:</strong> ${item.faults90d}</p>
        <p class="mb-0"><strong>Sonucu hâlen başarısız kontrol:</strong> ${item.failedInspections90d}</p>
      </div>`,
      confirmButtonText: 'Kapat',
      confirmButtonColor: '#2563eb',
    });
    row.addEventListener('click', showDetails);
    row.addEventListener('keydown', (event) => {
      if (event.key === 'Enter' || event.key === ' ') {
        event.preventDefault();
        showDetails();
      }
    });
    body.appendChild(row);
  });
  if (!items.length) body.innerHTML = '<tr><td colspan="7" class="text-center text-secondary py-5">Araç sağlık kaydı bulunamadı.</td></tr>';
}

// SweetAlert HTML içeriğine API metni eklenirken işaretleme çalıştırılmasını engeller.
function escapeHtml(value) {
  const element = document.createElement('div');
  element.textContent = value ?? '-';
  return element.innerHTML;
}

async function initialize() {
  try {
    const user = await authService.requireAuthenticatedUser();
    if (!user) return;
    renderNavigation('monitoring', user.role);
    document.querySelector('#current-user').textContent = `${user.fullName} · ${user.role}${user.garageName ? ` · ${user.garageName}` : ''}`;

    // Bağımsız rapor endpointleri paralel çağrılarak sayfa açılış süresi azaltılır.
    // Backend; admin/merkez için arıza geçmişi olan araçları, garaj yetkilisi için kendi
    // garajındaki bütün araçları döndürdüğünden istemcide ayrıca rol filtresi uygulanmaz.
    const [recurring, health] = await Promise.all([
      monitoringApi.getRecurringFaults(), monitoringApi.getVehicleHealth(5000),
    ]);
    renderRecurring(recurring);
    renderHealth(health);
    document.querySelector('#recurring-count').textContent = recurring.length;
    document.querySelector('#unhealthy-count').textContent = health.filter((item) => item.healthScore < 70).length;
    document.querySelector('#loading').classList.add('d-none');
    document.querySelector('#monitoring-content').classList.remove('d-none');
  } catch (error) {
    document.querySelector('#loading').classList.add('d-none');
    const box = document.querySelector('#page-error');
    box.textContent = error.message;
    box.classList.remove('d-none');
  }
}

document.querySelector('#logout-button').addEventListener('click', async () => {
  const result = await Swal.fire({ icon: 'question', title: 'Çıkış yapılsın mı?', showCancelButton: true, confirmButtonText: 'Çıkış Yap', cancelButtonText: 'Vazgeç' });
  if (result.isConfirmed) authService.logout();
});

initialize();
