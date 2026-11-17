const $ = sel => document.querySelector(sel);
const escapeHTML = value => String(value ?? '').replace(/[&<>"']/g, ch => ({
  '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
}[ch]));

const ico = {
  medical: '<path d="M10 3h4v7h7v4h-7v7h-4v-7H3v-4h7z"/>',
  users: '<path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><path d="M23 21v-2a4 4 0 0 0-3-3.87"/><path d="M16 3.13a4 4 0 0 1 0 7.75"/>',
  qr: '<rect x="3" y="3" width="7" height="7"/><rect x="14" y="3" width="7" height="7"/><rect x="3" y="14" width="7" height="7"/><path d="M14 14h3v3h-3zm4 4h3v3h-3zm-4 3h3"/>',
  refresh: '<path d="M20 7v5h-5M4 17v-5h5"/><path d="M6 9a7 7 0 0 1 12-2l2 5M4 12l2 5a7 7 0 0 0 12-2"/>',
  check: '<path d="m5 12 4 4L19 6"/>',
  info: '<circle cx="12" cy="12" r="10"/><path d="M12 11v6m0-10v.01"/>',
  lock: '<rect x="4" y="11" width="16" height="11" rx="2"/><path d="M8 11V7a4 4 0 0 1 8 0v4"/>',
  logout: '<path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4"/><polyline points="16 17 21 12 16 7"/><line x1="21" y1="12" x2="9" y2="12"/>'
};
const icon = (name, size = 18) =>
  `<svg width="${size}" height="${size}" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">${ico[name] || ico.info}</svg>`;

const state = {
  online: navigator.onLine,
  tab: 'fila',
  adminKey: sessionStorage.getItem('fc_doctor_key') || '',
  defaultRoom: sessionStorage.getItem('fc_doctor_room') || 'Consultorio 1',
  turns: [],
  verifiedTurn: null,
  busy: false,
  lastSync: null
};

function toast(message, type = 'success') {
  const area = $('#toast-region');
  const box = document.createElement('div');
  box.className = 'toast ' + type;
  box.innerHTML = icon(type === 'danger' ? 'info' : 'check', 18) + '<span></span>';
  box.querySelector('span').textContent = message;
  area.appendChild(box);
  setTimeout(() => box.remove(), 4500);
}

async function api(url, options = {}) {
  const res = await fetch(url, {
    ...options,
    headers: {
      ...(options.body ? { 'Content-Type': 'application/json' } : {}),
      'X-Admin-Key': state.adminKey,
      ...options.headers
    }
  });
  let body;
  try { body = await res.json(); } catch { body = {}; }
  if (!res.ok) {
    if (res.status === 401) {
      state.adminKey = '';
      sessionStorage.removeItem('fc_doctor_key');
    }
    throw new Error(body.error || `Error HTTP ${res.status}`);
  }
  return body;
}

function loginView() {
  return `
    <div class="content">
      <div class="eyebrow">FILACERO · PWA OPERATIVA</div>
      <h1 class="page-title">Acceso Médico y Recepción</h1>
      <p class="page-subtitle">Ingresa tu clave de acceso y selecciona el consultorio asignado para gestionar la fila de pacientes en tiempo real.</p>
      <div class="form-layout">
        <div class="form-panel">
          <h2 class="panel-title">Iniciar turno de atención</h2>
          <p class="panel-subtitle">Clave predeterminada en entorno de demostración: <code>demo-fila-2026</code></p>
          <form id="doctor-login">
            <div class="field-grid">
              <div class="field">
                <label for="adminKey">Clave de acceso médico *</label>
                <input id="adminKey" name="key" type="password" placeholder="demo-fila-2026" required autocomplete="off" />
              </div>
              <div class="field">
                <label for="defaultRoom">Consultorio asignado *</label>
                <select id="defaultRoom" name="room">
                  <option value="Consultorio 1">Consultorio 1</option>
                  <option value="Consultorio 2">Consultorio 2</option>
                  <option value="Consultorio 3">Consultorio 3</option>
                  <option value="Consultorio 4">Consultorio 4</option>
                  <option value="Consultorio 5">Consultorio 5</option>
                </select>
              </div>
            </div>
            <div class="form-footer">
              <button type="submit" class="btn btn-primary" ${!state.online ? 'disabled' : ''}>Entrar al panel ${icon('medical')}</button>
            </div>
          </form>
        </div>
        <aside class="context-side">
          <div class="side-card">
            <h3>Funciones del Panel Médico</h3>
            <div class="bullet"><span class="bullet-icon">1</span><p>Visualizar pacientes en espera y su prioridad de triaje.</p></div>
            <div class="bullet"><span class="bullet-icon">2</span><p>Llamar pacientes a consultorio notificando vía WebSocket.</p></div>
            <div class="bullet"><span class="bullet-icon">3</span><p>Validar la firma criptográfica de los pases QR en recepción.</p></div>
          </div>
        </aside>
      </div>
    </div>
  `;
}

function queueView() {
  const waiting = state.turns.filter(t => t.status === 'En espera');
  const active = state.turns.filter(t => t.status === 'Llamado a consultorio' || t.status === 'En atención');
  const done = state.turns.filter(t => t.status === 'Atendido');

  const rows = state.turns.map(t => {
    const roomValue = t.room || state.defaultRoom;
    const statusOptions = ['En espera', 'Llamado a consultorio', 'En atención', 'Atendido']
      .map(s => `<option value="${s}" ${s === t.status ? 'selected' : ''}>${s}</option>`)
      .join('');
    const roomOptions = ['Consultorio 1', 'Consultorio 2', 'Consultorio 3', 'Consultorio 4', 'Consultorio 5']
      .map(r => `<option value="${r}" ${r === roomValue ? 'selected' : ''}>${r}</option>`)
      .join('');

    return `
      <tr>
        <td><strong>${escapeHTML(t.code)}</strong></td>
        <td>${escapeHTML(t.fullName)}</td>
        <td>${escapeHTML(t.clinic)}</td>
        <td><span class="pill ${escapeHTML(t.priorityColor || '')}">${escapeHTML(t.priority || 'Estándar')}</span></td>
        <td><span class="pill ${t.status === 'Atendido' ? 'muted' : t.status === 'Llamado a consultorio' ? 'ambar' : ''}">${escapeHTML(t.status)}</span></td>
        <td>
          <form class="admin-update" data-action="update-turn" data-id="${escapeHTML(t.id)}">
            <select class="admin-control" name="status" aria-label="Estado">${statusOptions}</select>
            <select class="admin-control" name="room" aria-label="Consultorio">${roomOptions}</select>
            <button type="submit" class="btn btn-primary" ${!state.online ? 'disabled' : ''}>Actualizar</button>
          </form>
        </td>
      </tr>
    `;
  }).join('');

  return `
    <div class="eyebrow">GESTIÓN DE CONSULTA · ${escapeHTML(state.defaultRoom.toUpperCase())}</div>
    <h1 class="page-title">Sala de Espera en Tiempo Real</h1>
    <p class="page-subtitle">Administra el flujo de pacientes. Al cambiar a "Llamado a consultorio", el paciente recibe una alerta instantánea en su dispositivo.</p>

    <div class="steps" style="margin-top:24px">
      <div class="step-card">
        <div class="step-top"><div class="step-icon">${icon('users', 20)}</div><span class="step-number">EN ESPERA</span></div>
        <div><h3>${waiting.length} pacientes</h3><p>Turnos pendientes por pasar a valoración médica.</p></div>
      </div>
      <div class="step-card">
        <div class="step-top"><div class="step-icon">${icon('medical', 20)}</div><span class="step-number">EN CONSULTORIO</span></div>
        <div><h3>${active.length} activos</h3><p>Pacientes llamados o actualmente en consulta.</p></div>
      </div>
      <div class="step-card">
        <div class="step-top"><div class="step-icon">${icon('check', 20)}</div><span class="step-number">ATENDIDOS</span></div>
        <div><h3>${done.length} finalizados</h3><p>Consultas concluidas en la jornada actual.</p></div>
      </div>
    </div>

    <div class="admin-toolbar">
      <span class="pill muted">${state.lastSync ? `Última sincronización: ${state.lastSync}` : 'Sincronizado'}</span>
      <button type="button" class="btn btn-outline" data-action="refresh-turns" ${!state.online ? 'disabled' : ''}>${icon('refresh')} Actualizar lista</button>
    </div>

    ${state.turns.length ? `
      <div class="admin-table card" style="padding:0">
        <table>
          <thead>
            <tr>
              <th>Folio</th>
              <th>Paciente</th>
              <th>Unidad</th>
              <th>Prioridad Triaje</th>
              <th>Estado</th>
              <th>Acción / Consultorio</th>
            </tr>
          </thead>
          <tbody>${rows}</tbody>
        </table>
      </div>
    ` : `
      <div class="empty-state">
        <div class="empty-icon">${icon('users', 28)}</div>
        <h3>No hay pacientes en fila</h3>
        <p>Cuando un paciente complete su pre-registro y genere su turno en la PWA de pacientes, aparecerá aquí automáticamente.</p>
      </div>
    `}
  `;
}

function qrVerifyView() {
  const v = state.verifiedTurn;
  return `
    <div class="eyebrow">RECEPCIÓN Y CHECK-IN</div>
    <h1 class="page-title">Verificación de Pase QR</h1>
    <p class="page-subtitle">Valida la autenticidad del código QR presentado por el paciente antes de ingresarlo a consultorio.</p>

    <div class="form-layout">
      <div class="form-panel">
        <h2 class="panel-title">Validar firma del ticket</h2>
        <p class="panel-subtitle">Pega o escanea la cadena del código QR (formato <code>FC1|FOLIO|...</code>).</p>
        <form id="verify-qr-form">
          <div class="field">
            <label for="qrPayload">Cadena del código QR *</label>
            <input id="qrPayload" name="qrPayload" placeholder="FC1|A-001|..." required autocomplete="off" />
          </div>
          <div class="form-footer">
            <button type="submit" class="btn btn-primary" ${!state.online ? 'disabled' : ''}>Verificar autenticidad ${icon('qr')}</button>
          </div>
        </form>

        ${v ? `
          <div class="divider"></div>
          <div class="result-box">
            <span class="priority">✓ PASE QR AUTÉNTICO</span>
            <strong>Turno ${escapeHTML(v.code)} — ${escapeHTML(v.fullName)}</strong>
            <p>Unidad: <strong>${escapeHTML(v.clinic)}</strong> · Estado actual: <strong>${escapeHTML(v.status)}</strong> · Prioridad: <strong>${escapeHTML(v.priority)}</strong></p>
          </div>
        ` : ''}
      </div>
      <aside class="context-side">
        <div class="side-footnote">
          <h3>Seguridad HMAC</h3>
          <p>El servidor comprueba la firma criptográfica del QR para evitar pases falsificados o duplicados en recepción.</p>
        </div>
      </aside>
    </div>
  `;
}

function render() {
  if (!state.adminKey) {
    $('#app').innerHTML = loginView();
    return;
  }

  const mainContent = state.tab === 'qr' ? qrVerifyView() : queueView();
  $('#app').innerHTML = `
    <div class="shell">
      <aside class="sidebar">
        <a class="logo" href="#fila"><span class="logo-icon">F<small>+</small></span><span>Fila<strong>Cero</strong></span></a>
        <div class="logo-subtitle">PANEL MÉDICO · PWA 2</div>
        <div class="nav-caption">OPERACIÓN CLÍNICA</div>
        <nav class="nav" aria-label="Menú médico">
          <a class="nav-link ${state.tab === 'fila' ? 'active' : ''}" href="#fila" data-tab="fila">
            <span class="ico">${icon('users')}</span>Sala de espera
          </a>
          <a class="nav-link ${state.tab === 'qr' ? 'active' : ''}" href="#qr" data-tab="qr">
            <span class="ico">${icon('qr')}</span>Verificar QR
          </a>
        </nav>
        <div class="sidebar-bottom">
          <div class="help-mini">
            <span class="mini-icon">${icon('medical')}</span>
            <strong>${escapeHTML(state.defaultRoom)}</strong>
            <p>Sesión médica activa en modo operativo.</p>
            <button type="button" class="text-link" data-action="logout" style="background:none;border:0;padding:0">Cerrar sesión →</button>
          </div>
          <div class="side-foot">FilaCero Médico · v0.5.0<br/>Entorno académico</div>
        </div>
      </aside>

      <div class="main-area">
        <header class="topbar">
          <div class="crumb">Consola Médica <span>/</span> <strong>${state.tab === 'qr' ? 'Verificación QR' : 'Gestión de Turnos'}</strong></div>
          <div class="top-right">
            <span class="mode-indicator"><i class="dot ${state.online ? '' : 'off'}"></i>${state.online ? 'Conectado al servidor' : 'Sin conexión'}</span>
            <span class="avatar">DR</span>
          </div>
        </header>
        <header class="mobile-top">
          <span class="logo"><span class="logo-icon">F<small>+</small></span><span>Fila<strong>Cero</strong> Med</span></span>
          <span class="mode-indicator"><i class="dot ${state.online ? '' : 'off'}"></i>${state.online ? 'En línea' : 'Offline'}</span>
        </header>
        ${state.online ? '' : '<div class="offline-banner">Sin conexión a internet · Reconectando con el servidor de turnos…</div>'}
        <main class="content">${mainContent}</main>
      </div>

      <nav class="mobile-nav" aria-label="Navegación móvil médico">
        <a class="${state.tab === 'fila' ? 'active' : ''}" href="#fila" data-tab="fila">${icon('users')}<span>Turnos</span></a>
        <a class="${state.tab === 'qr' ? 'active' : ''}" href="#qr" data-tab="qr">${icon('qr')}<span>Validar QR</span></a>
        <a href="#salir" data-action="logout">${icon('logout')}<span>Salir</span></a>
      </nav>
    </div>
  `;
}

async function loadTurns(silent = false) {
  if (!state.adminKey || !state.online) return;
  try {
    const data = await api('/api/admin/turns');
    state.turns = data.turns || [];
    state.lastSync = new Date().toLocaleTimeString('es-MX', { hour: '2-digit', minute: '2-digit', second: '2-digit' });
    render();
  } catch (e) {
    if (!silent) toast(e.message, 'danger');
    render();
  }
}

document.addEventListener('click', async event => {
  const tabLink = event.target.closest('[data-tab]');
  if (tabLink) {
    event.preventDefault();
    state.tab = tabLink.dataset.tab;
    render();
    return;
  }

  const actionBtn = event.target.closest('[data-action]');
  if (!actionBtn) return;

  if (actionBtn.dataset.action === 'logout') {
    event.preventDefault();
    state.adminKey = '';
    state.turns = [];
    sessionStorage.removeItem('fc_doctor_key');
    render();
    toast('Sesión médica cerrada.');
  }

  if (actionBtn.dataset.action === 'refresh-turns') {
    await loadTurns();
    toast('Lista de turnos actualizada.');
  }
});

document.addEventListener('submit', async event => {
  const form = event.target;
  event.preventDefault();
  if (state.busy) return;
  state.busy = true;

  try {
    if (form.id === 'doctor-login') {
      const fd = new FormData(form);
      state.adminKey = String(fd.get('key') || '').trim();
      state.defaultRoom = String(fd.get('room') || 'Consultorio 1');
      sessionStorage.setItem('fc_doctor_key', state.adminKey);
      sessionStorage.setItem('fc_doctor_room', state.defaultRoom);
      await loadTurns();
      if (state.adminKey) toast(`Bienvenido a ${state.defaultRoom}`);
    }

    if (form.dataset.action === 'update-turn') {
      const fd = new FormData(form);
      const status = fd.get('status');
      const room = fd.get('room');
      const data = await api(`/api/admin/turns/${form.dataset.id}`, {
        method: 'PATCH',
        body: JSON.stringify({ status, room })
      });
      toast(`Turno ${data.turn.code} actualizado a "${data.turn.status}" (${room})`);
      await loadTurns(true);
    }

    if (form.id === 'verify-qr-form') {
      const qrPayload = String(new FormData(form).get('qrPayload') || '').trim();
      const data = await api('/api/admin/verify-qr', {
        method: 'POST',
        body: JSON.stringify({ qrPayload })
      });
      state.verifiedTurn = data.turn;
      render();
      toast(`QR válido: Turno ${data.turn.code}`);
    }
  } catch (e) {
    toast(e.message, 'danger');
  } finally {
    state.busy = false;
  }
});

window.addEventListener('online', () => { state.online = true; loadTurns(true); render(); });
window.addEventListener('offline', () => { state.online = false; render(); });

// Actualización automática cada 8 segundos para reflejar nuevos turnos de pacientes
setInterval(() => {
  if (state.adminKey && state.online && state.tab === 'fila') {
    loadTurns(true);
  }
}, 8000);

(async function init() {
  render();
  if ('serviceWorker' in navigator) {
    try {
      await navigator.serviceWorker.register('/doctor-sw.js', { scope: '/' });
    } catch (e) {
      console.warn('Error registrando doctor-sw.js:', e);
    }
  }
  if (state.adminKey) await loadTurns(true);
})();