// Minimaler Client fuer die Docker Engine API ueber den Unix-Socket.
import http from 'node:http';

export class DockerError extends Error {
  constructor(message, statusCode) {
    super(message);
    this.name = 'DockerError';
    this.statusCode = statusCode;
  }
}

export class DockerClient {
  #socketPath;

  constructor(socketPath) {
    this.#socketPath = socketPath;
  }

  /** Rohe Anfrage, liefert den Body als Buffer. */
  request(method, path, body) {
    return new Promise((resolve, reject) => {
      const data = body ? JSON.stringify(body) : null;
      const req = http.request(
        {
          socketPath: this.#socketPath,
          path,
          method,
          headers: data ? { 'Content-Type': 'application/json', 'Content-Length': Buffer.byteLength(data) } : {},
        },
        (res) => {
          const chunks = [];
          res.on('data', (c) => chunks.push(c));
          res.on('end', () => {
            const buf = Buffer.concat(chunks);
            if (res.statusCode >= 400) {
              let msg = buf.toString();
              try { msg = JSON.parse(msg).message || msg; } catch { /* kein JSON */ }
              return reject(new DockerError(`Docker ${method} ${path}: ${res.statusCode} ${msg}`, res.statusCode));
            }
            resolve(buf);
          });
        },
      );
      req.on('error', reject);
      if (data) req.write(data);
      req.end();
    });
  }

  async json(method, path, body) {
    const buf = await this.request(method, path, body);
    return buf.length ? JSON.parse(buf.toString()) : null;
  }

  listContainers(labelFilter) {
    const filters = encodeURIComponent(JSON.stringify({ label: [labelFilter] }));
    return this.json('GET', `/containers/json?all=1&filters=${filters}`);
  }

  inspectContainer(id) {
    return this.json('GET', `/containers/${id}/json`);
  }

  createContainer(name, spec) {
    return this.json('POST', `/containers/create?name=${encodeURIComponent(name)}`, spec);
  }

  startContainer(id) {
    return this.request('POST', `/containers/${id}/start`);
  }

  removeContainer(id) {
    return this.request('DELETE', `/containers/${id}?force=1&v=1`);
  }

  async logs(id, sinceUnix = 0) {
    return (await this.request('GET', `/containers/${id}/logs?stdout=1&stderr=1&since=${sinceUnix}`)).toString();
  }

  /** Image laden; die Antwort ist ein JSON-Stream, Fehler stehen als {"error": ...} darin. */
  async pullImage(image, tag) {
    const buf = await this.request('POST', `/images/create?fromImage=${encodeURIComponent(image)}&tag=${encodeURIComponent(tag)}`);
    const error = buf.toString().split('\n').filter(Boolean)
      .map((line) => { try { return JSON.parse(line); } catch { return {}; } })
      .find((msg) => msg.error);
    if (error) throw new DockerError(error.error);
  }

  /** Befehl im Container ausfuehren und auf das Ende warten. */
  async exec(id, cmd, user = 'root') {
    const ex = await this.json('POST', `/containers/${id}/exec`, {
      Cmd: cmd, User: user, AttachStdout: true, AttachStderr: true,
    });
    const out = await this.request('POST', `/exec/${ex.Id}/start`, { Detach: false, Tty: false });
    const info = await this.json('GET', `/exec/${ex.Id}/json`);
    // Stream-Header (8 Byte pro Frame) grob entfernen
    return { exitCode: info.ExitCode, output: out.toString('utf8').replace(/[\x00-\x08]/g, '') };
  }
}
