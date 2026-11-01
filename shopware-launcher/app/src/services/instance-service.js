// Lebenszyklus der Shopware-Instanzen: anlegen, auflisten, loeschen, Auto-Loeschen.
// Der dauerhafte Zustand steckt in Docker-Labels; nur laufende Erstellungen sind im Speicher.
import crypto from 'node:crypto';
import { traefikLabels } from './traefik-labels.js';
import { runSetup } from './shopware-setup.js';

const LABEL = 'sw-launcher';
const L = {
  marker: LABEL,
  name: `${LABEL}.name`,
  version: `${LABEL}.version`,
  expires: `${LABEL}.expires`,
  sshPort: `${LABEL}.ssh-port`,
  sshUser: `${LABEL}.ssh-user`,
  sshPassword: `${LABEL}.ssh-password`,
  adminPassword: `${LABEL}.admin-password`,
};
const NAME_RE = /^[a-z0-9](?:[a-z0-9-]{0,18}[a-z0-9])?$/;
const SSH_USER = 'dev';
const ADMIN_USER = 'admin';
const CLEANUP_INTERVAL_MS = 60_000;

export class ValidationError extends Error {
  constructor(message, status = 400) {
    super(message);
    this.status = status;
  }
}

const randomPassword = () => crypto.randomBytes(12).toString('base64url');

export class InstanceService {
  #docker;
  #config;
  #pending = new Map(); // name -> { step, error, version, expires, sshPort }
  #lock = Promise.resolve();
  #cleanupTimer = null;

  constructor(docker, config) {
    this.#docker = docker;
    this.#config = config;
  }

  // ---------------------------------------------------------------- Hosts
  publicHost(name) { return `sw-${name}.${this.#config.domain}`; }
  localHost(name) { return `sw-${name}.localhost`; }

  // ---------------------------------------------------------------- Abfragen
  #containers() {
    return this.#docker.listContainers(`${L.marker}=1`);
  }

  async list() {
    const containers = await this.#containers();
    const items = containers.map((c) => this.#toDto(c));
    for (const [name, p] of this.#pending) {
      if (!items.some((i) => i.name === name)) {
        items.push({ name, version: p.version, status: p.error ? 'error' : p.step, error: p.error, expires: p.expires });
      }
    }
    return items.sort((a, b) => a.name.localeCompare(b.name));
  }

  #toDto(container) {
    const l = container.Labels;
    const name = l[L.name];
    const pending = this.#pending.get(name);
    const status = pending ? (pending.error ? 'error' : pending.step) : (container.State === 'running' ? 'ready' : container.State);
    return {
      name,
      version: l[L.version],
      state: container.State,
      status,
      error: pending?.error ?? null,
      created: new Date(container.Created * 1000).toISOString(),
      expires: l[L.expires] || null,
      url: `https://${this.publicHost(name)}`,
      localUrl: `https://${this.localHost(name)}`,
      adminUser: ADMIN_USER,
      adminPassword: l[L.adminPassword],
      sshHost: this.publicHost(name),
      sshPort: Number(l[L.sshPort]),
      sshUser: l[L.sshUser],
      sshPassword: l[L.sshPassword],
    };
  }

  // ---------------------------------------------------------------- Anlegen
  /**
   * Prueft Limits, reserviert Name + SSH-Port und startet die Erstellung im Hintergrund.
   * Der Name bekommt eine GUID als Suffix (eindeutig, URL nicht erratbar; max. 60 Zeichen < DNS-Limit 63).
   */
  async create(baseName, version, autoDelete) {
    if (!NAME_RE.test(baseName ?? '')) throw new ValidationError('Name: nur a-z, 0-9, -, max. 20 Zeichen');

    const reservation = await this.#withLock(async () => {
      const count = (await this.list()).length;
      if (count >= this.#config.maxInstances) {
        throw new ValidationError(`Maximal ${this.#config.maxInstances} Instanzen`, 409);
      }
      const name = `${baseName}-${crypto.randomUUID()}`;
      const expires = autoDelete ? new Date(Date.now() + this.#config.ttlMinutes * 60_000).toISOString() : null;
      const entry = { step: 'pulling', error: null, version, expires, sshPort: await this.#freeSshPort() };
      this.#pending.set(name, entry);
      return { name, entry };
    });

    this.#provision(reservation.name, reservation.entry).catch((err) => {
      console.error(`[${reservation.name}]`, err);
      reservation.entry.error = err.message;
    });
    return { name: reservation.name };
  }

  async #provision(name, entry) {
    const { image, network } = this.#config;
    await this.#docker.pullImage(image, entry.version);

    entry.step = 'starting';
    const adminPassword = randomPassword();
    const sshPassword = randomPassword();
    const publicHost = this.publicHost(name);
    const localHost = this.localHost(name);

    const container = await this.#docker.createContainer(`sw-${name}`, {
      Image: `${image}:${entry.version}`,
      Env: [`SHOP_DOMAIN=${publicHost}`, `SSH_USER=${SSH_USER}`, `SSH_PWD=${sshPassword}`],
      Labels: {
        [L.marker]: '1',
        [L.name]: name,
        [L.version]: entry.version,
        [L.expires]: entry.expires ?? '',
        [L.sshPort]: String(entry.sshPort),
        [L.sshUser]: SSH_USER,
        [L.sshPassword]: sshPassword,
        [L.adminPassword]: adminPassword,
        ...traefikLabels({ router: `sw-${name}`, publicHost, localHost }),
      },
      ExposedPorts: { '22/tcp': {} },
      HostConfig: {
        NetworkMode: network,
        PortBindings: { '22/tcp': [{ HostPort: String(entry.sshPort) }] },
        RestartPolicy: { Name: 'unless-stopped' },
      },
    });
    await this.#docker.startContainer(container.Id);

    entry.step = 'setup';
    await runSetup(this.#docker, container.Id, { publicHost, localHost, adminPassword });
    this.#pending.delete(name);
  }

  async #freeSshPort() {
    const used = new Set((await this.#containers()).map((c) => Number(c.Labels[L.sshPort])));
    for (const p of this.#pending.values()) used.add(p.sshPort);
    for (let i = 0; i < this.#config.maxInstances; i++) {
      const port = this.#config.sshPortStart + i;
      if (!used.has(port)) return port;
    }
    throw new ValidationError('Kein freier SSH-Port', 409);
  }

  /** Serialisiert kritische Abschnitte (Limit-Pruefung + Reservierung). */
  #withLock(fn) {
    const run = this.#lock.then(fn, fn);
    this.#lock = run.catch(() => {});
    return run;
  }

  // ---------------------------------------------------------------- Loeschen
  async remove(name) {
    this.#pending.delete(name);
    const container = (await this.#containers()).find((c) => c.Labels[L.name] === name);
    if (container) await this.#docker.removeContainer(container.Id);
  }

  async removeExpired(now = new Date()) {
    for (const c of await this.#containers()) {
      const expires = c.Labels[L.expires];
      if (expires && new Date(expires) < now) {
        console.log(`Auto-Loeschen: ${c.Labels[L.name]}`);
        await this.#docker.removeContainer(c.Id);
      }
    }
  }

  startCleanupTimer() {
    this.#cleanupTimer = setInterval(() => {
      this.removeExpired().catch((err) => console.error('cleanup:', err.message));
    }, CLEANUP_INTERVAL_MS);
  }

  stopCleanupTimer() {
    clearInterval(this.#cleanupTimer);
  }

  // ---------------------------------------------------------------- Neustart
  /** dockware setzt APP_URL bei jedem Containerstart zurueck -> Setup fuer laufende Instanzen erneut anwenden. */
  async resumeRunning() {
    try {
      for (const c of await this.#containers()) {
        if (c.State !== 'running') continue;
        const name = c.Labels[L.name];
        const entry = { step: 'setup', error: null, version: c.Labels[L.version], expires: c.Labels[L.expires] || null, sshPort: Number(c.Labels[L.sshPort]) };
        this.#pending.set(name, entry);
        runSetup(this.#docker, c.Id, {
          publicHost: this.publicHost(name),
          localHost: this.localHost(name),
          adminPassword: c.Labels[L.adminPassword],
        })
          .then(() => this.#pending.delete(name))
          .catch((err) => { entry.error = err.message; });
      }
    } catch (err) {
      console.error('resume:', err.message);
    }
  }
}
