// Einstiegspunkt: Konfiguration laden, Hintergrundjobs starten, HTTP-Server hochfahren.
import http from 'node:http';
import { config } from './src/config.js';
import { createRouter } from './src/http/router.js';
import { InstanceService } from './src/services/instance-service.js';
import { VersionService } from './src/services/version-service.js';
import { DockerClient } from './src/lib/docker-client.js';

const docker = new DockerClient(config.dockerSocket);
const versions = new VersionService(config);
const instances = new InstanceService(docker, config);

const server = http.createServer(createRouter({ config, versions, instances }));

server.listen(config.port, () => {
  console.log(`Shopware-Launcher laeuft auf :${config.port}`);
  instances.resumeRunning();
  instances.startCleanupTimer();
});

// Sauber beenden (docker compose stop/restart)
for (const signal of ['SIGTERM', 'SIGINT']) {
  process.on(signal, () => {
    instances.stopCleanupTimer();
    server.close(() => process.exit(0));
  });
}
