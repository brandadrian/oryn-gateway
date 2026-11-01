// Verfuegbare Shopware-Versionen aus den Docker-Hub-Tags des Images (gecacht).
const VERSION_RE = /^6\.\d+\.\d+\.\d+$/;
const CACHE_MS = 60 * 60 * 1000;
const MAX_PAGES = 5;

/** Sortiert absteigend nach Versionsnummer (6.7.2.2 vor 6.7.2.1). */
export const compareVersionsDesc = (a, b) => {
  const pa = a.split('.').map(Number);
  const pb = b.split('.').map(Number);
  for (let i = 0; i < Math.max(pa.length, pb.length); i++) {
    if ((pa[i] ?? 0) !== (pb[i] ?? 0)) return (pb[i] ?? 0) - (pa[i] ?? 0);
  }
  return 0;
};

export class VersionService {
  #image;
  #cache = { at: 0, list: [] };

  constructor({ image }) {
    this.#image = image;
  }

  async list() {
    if (this.#cache.list.length && Date.now() - this.#cache.at < CACHE_MS) return this.#cache.list;
    const tags = new Set();
    let url = `https://hub.docker.com/v2/repositories/${this.#image}/tags?page_size=100`;
    for (let page = 0; url && page < MAX_PAGES; page++) {
      const res = await fetch(url);
      if (!res.ok) throw new Error(`Docker Hub: ${res.status}`);
      const json = await res.json();
      for (const tag of json.results) if (VERSION_RE.test(tag.name)) tags.add(tag.name);
      url = json.next;
    }
    this.#cache = { at: Date.now(), list: [...tags].sort(compareVersionsDesc) };
    return this.#cache.list;
  }
}
