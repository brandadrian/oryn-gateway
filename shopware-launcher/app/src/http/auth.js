// HTTP Basic Auth mit zeitkonstantem Vergleich.
import crypto from 'node:crypto';

const safeEqual = (a, b) => crypto.timingSafeEqual(
  crypto.createHash('sha256').update(a).digest(),
  crypto.createHash('sha256').update(b).digest(),
);

export function isAuthorized(req, { user, password }) {
  const header = req.headers.authorization || '';
  if (!header.startsWith('Basic ')) return false;
  const decoded = Buffer.from(header.slice(6), 'base64').toString();
  const sep = decoded.indexOf(':');
  if (sep < 0) return false;
  // beide Vergleiche immer ausfuehren (kein Short-Circuit)
  const userOk = safeEqual(decoded.slice(0, sep), user);
  const passOk = safeEqual(decoded.slice(sep + 1), password);
  return userOk && passOk;
}

export function sendUnauthorized(res) {
  res.writeHead(401, { 'WWW-Authenticate': 'Basic realm="Shopware-Launcher"' });
  res.end('Login erforderlich');
}
