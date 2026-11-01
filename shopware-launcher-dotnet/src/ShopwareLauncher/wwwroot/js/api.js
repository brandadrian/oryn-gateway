// REST-Client fuer das Launcher-Backend.
async function request(path, options = {}) {
  const res = await fetch(path, options);
  const data = await res.json().catch(() => ({}));
  if (!res.ok) throw new Error(data.error || res.statusText);
  return data;
}

export const api = {
  config: () => request('/api/config'),
  versions: () => request('/api/versions'),
  instances: () => request('/api/instances'),
  createInstance: (payload) => request('/api/instances', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(payload),
  }),
  deleteInstance: (name) => request(`/api/instances/${encodeURIComponent(name)}`, { method: 'DELETE' }),
};
