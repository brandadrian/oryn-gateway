// Nachbereitung eines dockware-Containers fuer den Betrieb hinter Traefik.
const READY_MARKER = 'SHOP URL'; // gibt dockware am Ende des Entrypoints aus
const POLL_MS = 5000;
const TIMEOUT_MS = 20 * 60 * 1000;

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

/** Wartet, bis dockware seit dem letzten Containerstart fertig initialisiert ist. */
export async function waitForDockware(docker, id) {
  const info = await docker.inspectContainer(id);
  const since = Math.floor(new Date(info.State.StartedAt).getTime() / 1000);
  const deadline = Date.now() + TIMEOUT_MS;
  while (Date.now() < deadline) {
    if ((await docker.logs(id, since)).includes(READY_MARKER)) return;
    await sleep(POLL_MS);
  }
  throw new Error('Timeout: dockware ist nicht fertig geworden');
}

/**
 * Bash-Skript (als root im Container):
 * - Apache meldet HTTPS an PHP, wenn Traefik X-Forwarded-Proto=https setzt (sonst Mixed Content)
 * - APP_URL auf https
 * - Admin-Passwort setzen
 * - zusaetzliche Sales-Channel-Domain fuer https://<local> (nicht kritisch)
 */
export function buildSetupScript({ publicHost, localHost, adminPassword }) {
  return `
set -e
echo 'SetEnvIf X-Forwarded-Proto "https" HTTPS=on' > /etc/apache2/conf-enabled/zz-sw-launcher.conf
sed -i 's#^APP_URL=http://#APP_URL=https://#' /var/www/html/.env
chown www-data:www-data /var/www/html/.env
cd /var/www/html
su -s /bin/bash www-data -c "php bin/console user:change-password admin --password='${adminPassword}'"
MYSQL_PWD=root mysql -h 127.0.0.1 -uroot shopware -e "
  SET SESSION sql_mode = '';
  DELETE FROM sales_channel_domain WHERE url = 'https://${localHost}';
  CREATE TEMPORARY TABLE t AS SELECT * FROM sales_channel_domain WHERE url = 'https://${publicHost}' LIMIT 1;
  UPDATE t SET id = UNHEX(REPLACE(UUID(), '-', '')), url = 'https://${localHost}', created_at = NOW(3);
  INSERT INTO sales_channel_domain SELECT * FROM t;" || echo "WARNUNG: lokale Domain konnte nicht angelegt werden"
su -s /bin/bash www-data -c "php bin/console cache:clear"
apache2ctl -k graceful
`;
}

export async function runSetup(docker, id, params) {
  await waitForDockware(docker, id);
  const { exitCode, output } = await docker.exec(id, ['bash', '-c', buildSetupScript(params)]);
  if (exitCode !== 0) throw new Error(`Setup fehlgeschlagen (Exit ${exitCode}): ${output.slice(-500)}`);
}
