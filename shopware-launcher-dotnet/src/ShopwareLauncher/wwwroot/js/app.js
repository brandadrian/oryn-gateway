// Einstieg Frontend: Formular, Liste mit Polling, Loeschen per Event-Delegation.
import { api } from './api.js';
import { esc, renderList, renderVersionOptions } from './render.js';

const REFRESH_MS = 5000;
const $ = (id) => document.getElementById(id);

async function loadConfig() {
  const [config, versions] = await Promise.all([api.config(), api.versions()]);
  $('max').textContent = config.maxInstances;
  $('ttl').textContent = config.ttlMinutes;
  $('version').innerHTML = renderVersionOptions(versions);
}

async function refresh() {
  try {
    $('list').innerHTML = renderList(await api.instances());
  } catch (err) {
    $('list').innerHTML = `<p class="msg">${esc(err.message)}</p>`;
  }
}

async function onSubmit(event) {
  event.preventDefault();
  $('msg').textContent = '';
  $('submit').disabled = true;
  try {
    await api.createInstance({
      name: $('name').value.trim(),
      version: $('version').value,
      autoDelete: $('autoDelete').checked,
    });
    $('name').value = '';
  } catch (err) {
    $('msg').textContent = err.message;
  } finally {
    $('submit').disabled = false;
    refresh();
  }
}

async function onListClick(event) {
  const name = event.target.closest('[data-delete]')?.dataset.delete;
  if (!name || !confirm(`Instanz sw-${name} wirklich löschen?`)) return;
  try {
    await api.deleteInstance(name);
  } catch (err) {
    alert(err.message);
  }
  refresh();
}

$('form').addEventListener('submit', onSubmit);
$('list').addEventListener('click', onListClick);
loadConfig().catch((err) => { $('msg').textContent = err.message; });
refresh();
setInterval(refresh, REFRESH_MS);
