// HTTP-Routing: Auth, REST-API unter /api, sonst statische Dateien aus /public.
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { isAuthorized, sendUnauthorized } from './auth.js';
import { HttpError, readJson, sendJson, serveStatic } from '../lib/http.js';
import { ValidationError } from '../services/instance-service.js';

const PUBLIC_DIR = path.join(path.dirname(fileURLToPath(import.meta.url)), '../../public');

export function createRouter({ config, versions, instances }) {
  const routes = [
    ['GET', /^\/api\/config$/, () => ({
      domain: config.domain, maxInstances: config.maxInstances, ttlMinutes: config.ttlMinutes,
    })],
    ['GET', /^\/api\/versions$/, () => versions.list()],
    ['GET', /^\/api\/instances$/, () => instances.list()],
    ['POST', /^\/api\/instances$/, async (req) => {
      const { name, version, autoDelete } = await readJson(req);
      if (!(await versions.list()).includes(version)) throw new ValidationError('Unbekannte Version');
      return [202, await instances.create(name, version, Boolean(autoDelete))];
    }],
    ['DELETE', /^\/api\/instances\/([a-z0-9-]+)$/, async (_req, [name]) => {
      await instances.remove(name);
      return { ok: true };
    }],
  ];

  return async (req, res) => {
    if (!isAuthorized(req, config.auth)) return sendUnauthorized(res);

    const { pathname } = new URL(req.url, 'http://localhost');
    try {
      for (const [method, pattern, handler] of routes) {
        const match = pathname.match(pattern);
        if (req.method !== method || !match) continue;
        const result = await handler(req, match.slice(1));
        const [status, body] = Array.isArray(result) && typeof result[0] === 'number' ? result : [200, result];
        return sendJson(res, status, body);
      }
      if (req.method === 'GET' && !pathname.startsWith('/api/') && await serveStatic(res, PUBLIC_DIR, pathname)) return;
      sendJson(res, 404, { error: 'Nicht gefunden' });
    } catch (err) {
      if (err instanceof HttpError) return sendJson(res, err.status, { error: err.message });
      if (err instanceof ValidationError) return sendJson(res, err.status, { error: err.message });
      console.error(err);
      sendJson(res, 500, { error: err.message });
    }
  };
}
