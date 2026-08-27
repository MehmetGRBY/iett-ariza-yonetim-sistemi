import { tokenStore } from '../auth/token-store.js';
import { notificationsApi } from '../api/notifications-api.js';
import { Dropdown } from 'bootstrap';

// Uygulamadaki bütün sayfa ve modüller tek listede tanımlanır. Böylece sol menü ile
// dashboard modül özeti birbirinden kopmaz ve yeni özellik yalnızca tek yere eklenir.
export const applicationModules = Object.freeze([
  { group: 'GENEL', key: 'dashboard', title: 'Dashboard', icon: 'bi-speedometer2', href: './index.html', roles: ['Admin', 'Merkez Yetkilisi', 'Garaj Yetkilisi'], status: 'ready' },
  { group: 'OPERASYON', key: 'faults', title: 'Arızalar', icon: 'bi-tools', href: './faults.html', roles: ['Admin', 'Merkez Yetkilisi', 'Garaj Yetkilisi'], status: 'ready' },
  { group: 'OPERASYON', key: 'tasks', title: 'Görev ve Hat Planı', icon: 'bi-calendar-check', href: './tasks.html', roles: ['Admin', 'Merkez Yetkilisi', 'Garaj Yetkilisi'], status: 'ready' },
  { group: 'OPERASYON', key: 'personnel-incidents', title: 'Personel Olayları', icon: 'bi-person-exclamation', href: './personnel-incidents.html', roles: ['Admin', 'Merkez Yetkilisi', 'Garaj Yetkilisi'], status: 'ready' },

  { group: 'FİLO VE PERSONEL', key: 'vehicles', title: 'Araçlar', icon: 'bi-bus-front', href: './vehicles.html', roles: ['Admin', 'Merkez Yetkilisi', 'Garaj Yetkilisi'], status: 'ready' },
  { group: 'FİLO VE PERSONEL', key: 'garages', title: 'Garajlar', icon: 'bi-buildings', href: './garages.html', roles: ['Admin', 'Merkez Yetkilisi', 'Garaj Yetkilisi'], status: 'ready' },
  { group: 'FİLO VE PERSONEL', key: 'drivers', title: 'Sürücüler', icon: 'bi-person-vcard', href: './drivers.html', roles: ['Admin', 'Garaj Yetkilisi'], status: 'ready' },
  { group: 'FİLO VE PERSONEL', key: 'technicians', title: 'Teknik Ekipler', icon: 'bi-people', href: './technicians.html', roles: ['Admin', 'Garaj Yetkilisi'], status: 'ready' },

  { group: 'İZLEME', key: 'inspections', title: 'Araç Kontrolleri', icon: 'bi-clipboard2-check', href: './inspections.html', roles: ['Admin', 'Merkez Yetkilisi', 'Garaj Yetkilisi'], status: 'ready' },
  { group: 'İZLEME', key: 'monitoring', title: 'Operasyon İzleme', icon: 'bi-activity', href: './monitoring.html', roles: ['Admin', 'Merkez Yetkilisi', 'Garaj Yetkilisi'], status: 'ready' },
  { group: 'KARAR DESTEK', key: 'solutions', title: 'Çözüm Kütüphanesi', icon: 'bi-journal-medical', href: './solutions.html', roles: ['Admin', 'Merkez Yetkilisi', 'Garaj Yetkilisi'], status: 'ready' },
  { group: 'KARAR DESTEK', key: 'operational-events', title: 'Operasyon Olayları', icon: 'bi-broadcast-pin', href: './operational-events.html', roles: ['Admin', 'Merkez Yetkilisi', 'Garaj Yetkilisi'], status: 'ready' },

  { group: 'YÖNETİM', key: 'users', title: 'Kullanıcı Yönetimi', icon: 'bi-person-gear', href: './users.html', roles: ['Admin'], status: 'ready' },
  { group: 'YÖNETİM', key: 'audit-logs', title: 'İşlem Kayıtları', icon: 'bi-shield-check', href: './audit-logs.html', roles: ['Admin'], status: 'ready' },
  { group: 'YÖNETİM', key: 'system-settings', title: 'Sistem Ayarları', icon: 'bi-sliders', href: './system-settings.html', roles: ['Admin'], status: 'ready' },
]);

// Kullanıcının rolüne göre erişebileceği modüller filtrelenir; gerçek güvenlik kontrolü backend'de kalır.
export function modulesForRole(role, allowedPages = null) {
  return applicationModules.filter((module) => module.roles.includes(role) &&
    (!allowedPages || allowedPages.includes(module.key)));
}

// Ortak sol menüyü aktif sayfa ve rol bilgisine göre güvenli DOM elemanlarıyla üretir.
export function renderNavigation(activePage, role) {
  const menu = document.querySelector('#application-navigation');
  if (!menu) return;
  renderSidebarBrandLogo();
  menu.replaceChildren();

  let currentGroup = null;
  // Güncel erişim listesi oturum doğrulamasında backend'den alınarak kullanıcı nesnesine yazılır.
  const storedUser = tokenStore.getUser();
  const allowedPages = storedUser?.allowedPages ?? null;
  modulesForRole(role, allowedPages).forEach((module) => {
    if (module.group !== currentGroup) {
      currentGroup = module.group;
      const header = document.createElement('li');
      header.className = 'nav-header';
      header.textContent = currentGroup;
      menu.appendChild(header);
    }

    const item = document.createElement('li');
    item.className = 'nav-item';
    const link = document.createElement('a');
    link.href = module.href ?? '#';
    link.className = `nav-link${module.key === activePage ? ' active' : ''}${module.status !== 'ready' ? ' disabled' : ''}`;
    link.setAttribute('aria-disabled', String(module.status !== 'ready'));

    const icon = document.createElement('i');
    icon.className = `nav-icon bi ${module.icon}`;
    const text = document.createElement('p');
    text.textContent = module.title;

    // Henüz sayfası yapılmayan modül görünür kalır ancak kullanıcıyı 404 sayfasına göndermez.
    if (module.status !== 'ready') {
      const badge = document.createElement('span');
      badge.className = 'nav-badge badge text-bg-secondary ms-auto';
      badge.textContent = module.status === 'backend' ? 'API' : 'Sırada';
      text.appendChild(badge);
    }

    link.append(icon, text);
    item.appendChild(link);
    menu.appendChild(item);
  });

  // Bildirimler ayrı bir modül sayfası yerine her ekrandan erişilebilen
  // ortak üst menü zilinde gösterilir.
  renderNotificationBell();
}

// Bütün iç sayfalar aynı menü üreticisini kullandığı için resmî İETT logosu da
// tek noktadan eklenir; böylece her HTML dosyasında ayrı ayrı tekrar edilmez.
function renderSidebarBrandLogo() {
  const brandLink = document.querySelector('.sidebar-brand .brand-link');
  if (!brandLink || brandLink.querySelector('.sidebar-official-logo')) return;

  const logo = document.createElement('span');
  logo.className = 'sidebar-official-logo';
  logo.setAttribute('aria-hidden', 'true');
  logo.innerHTML = '<img src="/assets/brand/iett-logo.png" alt="" />';
  brandLink.prepend(logo);
}

const notificationDateFormatter = new Intl.DateTimeFormat('tr-TR', {
  dateStyle: 'short',
  timeStyle: 'short',
});

// Bildirime tıklandığında ilgili operasyon kaydına gider.
function notificationTarget(item) {
  // Kritik sağlık bildirimi kullanıcıyı doğrudan Araç Sağlığı ekranına götürür.
  if (item.notificationType === 'VEHICLE_HEALTH_CRITICAL') return './monitoring.html';
  if (item.faultId) return `./faults.html?faultId=${item.faultId}`;
  if (item.serviceTaskId) return './tasks.html';
  if (item.notificationType?.startsWith('OPERATIONAL_EVENT_')) return './operational-events.html';
  return null;
}

// Kullanıcı adının yanına okunmamış sayacı olan ortak bildirim menüsü ekler.
async function renderNotificationBell() {
  const currentUser = document.querySelector('#current-user');
  const headerActions = currentUser?.parentElement;
  if (!currentUser || !headerActions || headerActions.querySelector('#header-notification-menu')) return;

  const wrapper = document.createElement('div');
  wrapper.id = 'header-notification-menu';
  wrapper.className = 'dropdown';
  wrapper.innerHTML = `
    <button class="btn header-notification-button position-relative" type="button"
      aria-expanded="false" aria-controls="header-notification-dropdown"
      aria-label="Bildirimleri göster">
      <i class="bi bi-bell fs-5"></i>
      <span id="header-notification-count"
        class="position-absolute top-0 start-100 translate-middle badge rounded-pill text-bg-danger d-none">0</span>
    </button>
    <div id="header-notification-dropdown" class="dropdown-menu dropdown-menu-end notification-dropdown shadow p-0">
      <div class="d-flex align-items-center justify-content-between px-3 py-2 border-bottom">
        <strong>Bildirimler</strong>
        <button id="header-read-all" class="btn btn-link btn-sm text-decoration-none p-0">Tümünü oku</button>
      </div>
      <div id="header-notification-list" class="notification-dropdown-list">
        <div class="text-center text-secondary py-4">Yükleniyor…</div>
      </div>
    </div>`;
  headerActions.insertBefore(wrapper, currentUser);

  const list = wrapper.querySelector('#header-notification-list');
  const countBadge = wrapper.querySelector('#header-notification-count');
  const readAllButton = wrapper.querySelector('#header-read-all');
  const toggleButton = wrapper.querySelector('.header-notification-button');

  // Zil sonradan DOM'a eklendiği için Bootstrap'ın otomatik data-attribute
  // davranışına güvenilmez; dropdown örneği doğrudan oluşturularak her tıklamanın
  // aynı şekilde açılıp kapanması sağlanır.
  const dropdown = Dropdown.getOrCreateInstance(toggleButton, { autoClose: 'outside' });
  toggleButton.addEventListener('click', (event) => {
    event.preventDefault();
    dropdown.toggle();
  });

  const load = async () => {
    try {
      const items = await notificationsApi.getAll(false);
      const unreadCount = items.filter((item) => !item.isRead).length;
      countBadge.textContent = unreadCount > 99 ? '99+' : String(unreadCount);
      countBadge.classList.toggle('d-none', unreadCount === 0);
      readAllButton.disabled = unreadCount === 0;
      list.replaceChildren();

      if (!items.length) {
        list.innerHTML = '<div class="text-center text-secondary py-4">Yeni bildirim bulunmuyor.</div>';
        return;
      }

      items.slice(0, 10).forEach((item) => {
        const button = document.createElement('button');
        button.type = 'button';
        button.className = `notification-dropdown-item${item.isRead ? '' : ' is-unread'}`;
        const title = document.createElement('span');
        title.className = 'd-block fw-semibold';
        title.textContent = item.title;
        const message = document.createElement('span');
        message.className = 'd-block small text-secondary text-truncate';
        message.textContent = item.message;
        const date = document.createElement('small');
        date.className = 'd-block text-secondary mt-1';
        date.textContent = notificationDateFormatter.format(new Date(item.createdAt));
        button.append(title, message, date);
        button.addEventListener('click', async () => {
          if (!item.isRead) await notificationsApi.markAsRead(item.id);
          const target = notificationTarget(item);
          if (target) window.location.href = target;
          else await load();
        });
        list.appendChild(button);
      });
    } catch {
      list.innerHTML = '<div class="text-center text-danger py-4">Bildirimler alınamadı.</div>';
    }
  };

  readAllButton.addEventListener('click', async () => {
    await notificationsApi.markAllAsRead();
    await load();
  });
  await load();
}
