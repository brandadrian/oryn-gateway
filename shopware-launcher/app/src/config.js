// Zentrale Konfiguration aus Umgebungsvariablen (siehe docker-compose.yml / .env).
const int = (value, fallback) => {
  const n = Number.parseInt(value ?? '', 10);
  return Number.isNaN(n) ? fallback : n;
};

export const config = Object.freeze({
  port: int(process.env.PORT, 3000),
  dockerSocket: process.env.DOCKER_SOCKET || '/var/run/docker.sock',
  domain: process.env.DOMAIN || 'oryn.brandadrian.com',
  image: process.env.IMAGE || 'dockware/dev',
  network: process.env.DOCKER_NETWORK || 'proxy',
  maxInstances: int(process.env.MAX_INSTANCES, 3),
  sshPortStart: int(process.env.SSH_PORT_START, 22001),
  ttlMinutes: int(process.env.AUTO_DELETE_MINUTES, 60),
  auth: {
    user: process.env.LAUNCHER_USER || 'admin',
    password: process.env.LAUNCHER_PASSWORD || '',
  },
});

if (!config.auth.password) {
  console.error('LAUNCHER_PASSWORD fehlt (.env). Abbruch.');
  process.exit(1);
}
