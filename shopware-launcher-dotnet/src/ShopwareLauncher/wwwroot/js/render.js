// Reine Render-Funktionen (Daten -> HTML-String), alles escaped.
const STATUS = {
  pulling: ['Image wird geladen (kann einige Minuten dauern)…', 'busy'],
  starting: ['Container startet…', 'busy'],
  setup: ['Shopware wird eingerichtet…', 'busy'],
  ready: ['bereit', 'ready'],
  error: ['Fehler', 'error'],
  exited: ['gestoppt', ''],
};

const ENTITIES = { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' };
export const esc = (value) => String(value ?? '').replace(/[&<>"']/g, (c) => ENTITIES[c]);

export function remaining(expires, now = Date.now()) {
  if (!expires) return 'nie';
  return `in ${Math.max(0, Math.round((new Date(expires) - now) / 60000))} Min.`;
}

const link = (url, active) => (active ? `<a href="${esc(url)}" target="_blank" rel="noopener">${esc(url)}</a>` : esc(url));

function details(i) {
  const ready = i.status === 'ready';
  if (!i.url) return `<dl><dt>Löschen</dt><dd>${remaining(i.expires)}</dd></dl>`;
  return `<dl>
    <dt>Shop</dt><dd>${link(i.url, ready)}</dd>
    <dt>Admin</dt><dd>${link(`${i.url}/admin`, ready)} · <code>${esc(i.adminUser)}</code> / <code>${esc(i.adminPassword)}</code></dd>
    <dt>Lokal</dt><dd>${link(i.localUrl, ready)}</dd>
    <dt>SSH / SFTP</dt><dd><code>ssh -p ${esc(i.sshPort)} ${esc(i.sshUser)}@${esc(i.sshHost)}</code> · Passwort <code>${esc(i.sshPassword)}</code></dd>
    <dt>Löschen</dt><dd>${remaining(i.expires)}</dd>
  </dl>`;
}

export function renderInstance(i) {
  const [label, cls] = STATUS[i.status] ?? [i.status, ''];
  return `<div class="card inst">
    <div class="row">
      <h2>sw-${esc(i.name)} <span class="badge">${esc(i.version)}</span> <span class="badge ${cls}">${esc(label)}</span></h2>
      <button class="danger" data-delete="${esc(i.name)}">Löschen</button>
    </div>
    ${i.error ? `<p class="msg">${esc(i.error)}</p>` : ''}
    ${details(i)}
  </div>`;
}

export function renderList(instances) {
  return instances.length ? instances.map(renderInstance).join('') : '<p class="empty">Keine Instanzen.</p>';
}

export const renderVersionOptions = (versions) => versions.map((v) => `<option>${esc(v)}</option>`).join('');
