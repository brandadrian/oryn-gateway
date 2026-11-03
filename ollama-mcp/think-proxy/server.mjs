// Mini-Proxy vor Ollama: reicht alles unveraendert durch (Streaming ohne Puffern).
// Nur fuer die Modelle in NO_THINK_MODELS wird Thinking abgeschaltet:
//   /api/chat, /api/generate        -> "think": false
//   /v1/chat/completions, /v1/completions -> "reasoning_effort": "none"  ("think" wird dort ignoriert)
import http from 'node:http';

const UPSTREAM = new URL(process.env.OLLAMA_URL ?? 'http://host.docker.internal:11434');
const NO_THINK_MODELS = new Set((process.env.NO_THINK_MODELS ?? 'qwen3.5-mcp').split(',').map((m) => m.trim()).filter(Boolean));
const PORT = Number(process.env.PORT ?? 8080);
const MAX_RETRIES = Number(process.env.MAX_RETRIES ?? 2);
const RETRY_ERROR = /forcibly closed|chat prompt error/i;

const isNoThink = (model) => typeof model === 'string' && (NO_THINK_MODELS.has(model) || NO_THINK_MODELS.has(model.replace(/:latest$/, '')));

function rewrite(path, raw) {
  if (!/^\/(api\/(chat|generate)|v1\/(chat\/)?completions)$/.test(path)) return null;
  let body;
  try { body = JSON.parse(raw); } catch { return null; }
  if (!isNoThink(body?.model)) return null;
  if (path.startsWith('/api/')) body.think = false;
  else body.reasoning_effort = 'none';
  return JSON.stringify(body);
}

function forward(req, res, body, attempt = 0) {
  const headers = { ...req.headers, host: UPSTREAM.host };
  if (body !== undefined) {
    headers['content-length'] = Buffer.byteLength(body);
    delete headers['transfer-encoding'];
  }
  const up = http.request({ hostname: UPSTREAM.hostname, port: UPSTREAM.port, method: req.method, path: req.url, headers }, (upRes) => {
    // Ollama-Bug: der interne llama-server trennt bei grossen Prompts manchmal die Verbindung ("chat prompt error",
    // HTTP 500 nach ~19 s). Ein zweiter Versuch geht praktisch immer durch -> bei genau diesem Fehler neu senden.
    if (upRes.statusCode === 500 && body !== undefined && attempt < MAX_RETRIES) {
      const errChunks = [];
      upRes.on('data', (c) => errChunks.push(c));
      upRes.on('end', () => {
        const err = Buffer.concat(errChunks);
        if (RETRY_ERROR.test(err.toString('utf8')) && !res.destroyed) {
          console.log(`retry ${attempt + 1}/${MAX_RETRIES} ${req.url}: ${err.toString('utf8').trim()}`);
          return setTimeout(() => { if (!res.destroyed) forward(req, res, body, attempt + 1); }, 1000);
        }
        res.writeHead(upRes.statusCode, upRes.headers);
        res.end(err);
      });
      return;
    }
    res.writeHead(upRes.statusCode, upRes.headers);
    upRes.pipe(res);
  });
  up.on('error', (err) => {
    console.error(`upstream error ${req.method} ${req.url}: ${err.message}`);
    if (!res.headersSent) res.writeHead(502, { 'content-type': 'application/json' });
    res.end(JSON.stringify({ error: `think-proxy: ${err.message}` }));
  });
  // Client bricht ab (z.B. Copilot-Timeout) -> Ollama-Anfrage auch abbrechen
  if (attempt === 0) res.on('close', () => { if (!res.writableFinished) res.currentUpstream?.destroy(); });
  res.currentUpstream = up;
  if (body !== undefined) up.end(body);
  else req.pipe(up);
}

http.createServer((req, res) => {
  const path = req.url.split('?')[0];
  if (req.method !== 'POST' || !/^\/(api|v1)\//.test(path)) return forward(req, res);
  const chunks = [];
  req.on('data', (c) => chunks.push(c));
  req.on('end', () => {
    const raw = Buffer.concat(chunks);
    const changed = rewrite(path, raw.toString('utf8'));
    if (changed) console.log(`no-think ${path} model=${JSON.parse(changed).model}`);
    forward(req, res, changed ?? raw);
  });
}).listen(PORT, process.env.HOST, () => console.log(`think-proxy :${PORT} -> ${UPSTREAM.origin}, no-think models: ${[...NO_THINK_MODELS].join(', ')}`));
