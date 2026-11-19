import { qrSvg } from './qr.js';

export function folioPage({ id = '', appointments = [], esc, dateText, badge, icon }) {
  const cita = appointments.find(a => String(a.id) === String(id));

  return `
    <div class="heading">
      <div>
        <div class="eyebrow">PANEL DEL PACIENTE</div>
        <h1>Consultar mi folio y QR</h1>
        <p>Consulta los folios de tus citas y muestra tu código QR.</p>
      </div>
      <a class="btn btn-secondary" href="#/citas">Volver a mis citas</a>
    </div>

    <section class="panel folio-search-panel">
      <form id="folio-search-form" class="folio-form">
        <label for="folio-input">Número de folio</label>
        <div class="folio-form-row">
          <input
            id="folio-input"
            name="folio"
            type="text"
            placeholder="Ej. FC-82D6C0D9"
            value="${cita ? esc(cita.folio) : ''}"
            maxlength="40"
            autocomplete="off"
            required
          />
          <button class="btn btn-primary" type="submit">
            ${icon('search', 17)} Consultar
          </button>
        </div>
        <small>Solo puedes consultar folios de tus propias citas.</small>
      </form>
    </section>

    ${cita ? `
      <section class="panel folio-ticket">
        <div class="eyebrow">FOLIO DE TU CITA</div>
        <strong class="folio-number">${esc(cita.folio)}</strong>
        <div class="folio-qr">${qrSvg(String(cita.folio))}</div>
        <p class="muted">Presenta tu folio o código QR en tu sucursal.</p>
        <div class="folio-details">
          <div><small>Sucursal</small><strong>${esc(cita.branchName)}</strong></div>
          <div><small>Fecha</small><strong>${esc(dateText(cita.date))}</strong></div>
          <div><small>Hora</small><strong>${esc(cita.time)} h</strong></div>
          <div><small>Estado</small>${badge(cita.status)}</div>
        </div>
        ${cita.status === 'Cancelada'
          ? '<p class="folio-warning">Esta cita está cancelada. El QR no representa una cita vigente.</p>'
          : ''}
      </section>
    ` : id ? `
      <section class="panel folio-search-panel">
        <p>No se encontró esa cita en tu cuenta.</p>
      </section>
    ` : ''}

    ${appointments.length ? `
      <section class="panel folio-choices">
        <h3>Seleccionar uno de mis folios</h3>
        <div class="folio-links">
          ${appointments.map(a => `
            <a class="folio-link" href="#/folio/${encodeURIComponent(a.id)}">
              <strong>${esc(a.folio)}</strong>
              <small>${esc(a.branchName)} · ${esc(dateText(a.date))}</small>
            </a>
          `).join('')}
        </div>
      </section>
    ` : `
      <section class="panel folio-search-panel">
        <p>Aún no tienes citas registradas.</p>
        <a class="btn btn-primary" href="#/agendar">Agendar cita</a>
      </section>
    `}
  `;
}
