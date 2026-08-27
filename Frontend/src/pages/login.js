import 'bootstrap/dist/js/bootstrap.bundle.min.js';
import 'bootstrap-icons/font/bootstrap-icons.css';
import 'admin-lte/dist/css/adminlte.min.css';
import Swal from 'sweetalert2';
import { authService } from '../auth/auth-service.js';
import '../styles/app.css';

document.documentElement.lang = 'tr';

// Geçerli oturum varken kimlik ekranının tekrar açılması engellenir.
if (authService.isAuthenticated()) window.location.replace('/');

const form = document.querySelector('#login-form');
const personnelNumberInput = document.querySelector('#personnel-number');
const currentPasswordInput = document.querySelector('#password');
const newPasswordInput = document.querySelector('#new-password');
const confirmPasswordInput = document.querySelector('#confirm-password');
const currentPasswordGroup = document.querySelector('#current-password-group');
const currentPasswordLabel = document.querySelector('#current-password-label');
const newPasswordFields = document.querySelector('#new-password-fields');
const title = document.querySelector('#login-title');
const description = document.querySelector('#login-description');
const button = document.querySelector('#login-button');
const buttonText = document.querySelector('#login-button-text');
const spinner = document.querySelector('#login-spinner');
const alert = document.querySelector('#login-alert');
const modeButtons = [...document.querySelectorAll('[data-auth-mode]')];

let activeMode = 'login';

const modeContent = {
  login: { title: 'Tekrar hoş geldiniz', description: 'Devam etmek için kurum sicil numaranızı ve parolanızı girin.', button: 'Sisteme Giriş Yap', loading: 'Giriş yapılıyor…' },
  activate: { title: 'İlk giriş parolanızı oluşturun', description: 'Admin tarafından oluşturulan sicil numaranızla kendi parolanızı belirleyin.', button: 'Parolamı Oluştur', loading: 'Parola oluşturuluyor…' },
  change: { title: 'Parolanızı değiştirin', description: 'Sicil numaranızı, mevcut parolanızı ve kullanmak istediğiniz yeni parolayı girin.', button: 'Parolamı Değiştir', loading: 'Parola değiştiriliyor…' },
};

// Seçilen işleme göre aynı form içindeki gerekli parola alanları değiştirilir.
function selectMode(mode) {
  activeMode = mode;
  const content = modeContent[mode];
  const isLogin = mode === 'login';
  const isActivate = mode === 'activate';
  title.textContent = content.title;
  description.textContent = content.description;
  buttonText.textContent = content.button;
  currentPasswordGroup.classList.toggle('d-none', isActivate);
  newPasswordFields.classList.toggle('d-none', isLogin);
  currentPasswordInput.required = !isActivate;
  newPasswordInput.required = !isLogin;
  confirmPasswordInput.required = !isLogin;
  currentPasswordLabel.textContent = mode === 'change' ? 'Mevcut parola' : 'Parola';
  currentPasswordInput.placeholder = mode === 'change' ? 'Mevcut parolanızı girin' : 'Parolanızı girin';
  currentPasswordInput.value = '';
  newPasswordInput.value = '';
  confirmPasswordInput.value = '';
  alert.classList.add('d-none');
  modeButtons.forEach((modeButton) => {
    const selected = modeButton.dataset.authMode === mode;
    modeButton.classList.toggle('active', selected);
    modeButton.setAttribute('aria-selected', String(selected));
  });
}

function setLoading(isLoading) {
  button.disabled = isLoading;
  modeButtons.forEach((modeButton) => { modeButton.disabled = isLoading; });
  spinner.classList.toggle('d-none', !isLoading);
  buttonText.textContent = isLoading ? modeContent[activeMode].loading : modeContent[activeMode].button;
}

function showError(message) {
  alert.textContent = message;
  alert.classList.remove('d-none');
}

function validatePasswordFields() {
  if (newPasswordInput.value.length < 8) return 'Yeni parola en az 8 karakter olmalıdır.';
  if (newPasswordInput.value !== confirmPasswordInput.value) return 'Yeni parola ile parola tekrarı eşleşmiyor.';
  if (activeMode === 'change' && currentPasswordInput.value === newPasswordInput.value) return 'Yeni parola mevcut paroladan farklı olmalıdır.';
  return null;
}

modeButtons.forEach((modeButton) => modeButton.addEventListener('click', () => selectMode(modeButton.dataset.authMode)));

form.addEventListener('submit', async (event) => {
  event.preventDefault();
  alert.classList.add('d-none');
  const personnelNumber = personnelNumberInput.value.trim();
  if (!personnelNumber) return showError('Sicil numarası zorunludur.');
  if (activeMode !== 'activate' && !currentPasswordInput.value) return showError('Parola zorunludur.');
  if (activeMode !== 'login') {
    const validationMessage = validatePasswordFields();
    if (validationMessage) return showError(validationMessage);
  }

  setLoading(true);
  try {
    if (activeMode === 'login') {
      const user = await authService.login(personnelNumber, currentPasswordInput.value);
      await Swal.fire({ icon: 'success', title: 'Giriş başarılı', text: `Hoş geldiniz, ${user.fullName}.`, timer: 1000, showConfirmButton: false });
      window.location.replace('/');
      return;
    }
    const response = activeMode === 'activate'
      ? await authService.activateAccount(personnelNumber, newPasswordInput.value, confirmPasswordInput.value)
      : await authService.changePasswordFromLogin(personnelNumber, currentPasswordInput.value, newPasswordInput.value, confirmPasswordInput.value);
    await Swal.fire({ icon: 'success', title: activeMode === 'activate' ? 'Parola oluşturuldu' : 'Parola değiştirildi', text: response.message });
    selectMode('login');
    personnelNumberInput.value = personnelNumber;
    currentPasswordInput.focus();
  } catch (error) {
    showError(error.message);
  } finally {
    setLoading(false);
  }
});
