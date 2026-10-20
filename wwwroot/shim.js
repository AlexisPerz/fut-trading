/* Local stand-ins for the services the interface used on claude.ai:
   - db: a small document store (same calls as before) saved to data/base.json by the app;
   - mcp: the app's built-in browser window (preview_start, navigate, get_page_text, find, computer);
   - user: the single local user, who can always write.
   Claude sampling (Coach, item detail) does not exist locally: claude.use("sample") resolves null. */
(() => {
  const bridge = window.chrome?.webview;
  const pending = new Map();
  let seq = 0;
  const host = (op, payload = {}) => new Promise((resolve, reject) => {
    if (!bridge) return reject({ code: 'server_not_connected', message: 'Hors de l’application FUT Trading' });
    const id = ++seq;
    pending.set(id, { resolve, reject });
    bridge.postMessage({ id, op, ...payload });
  });
  bridge?.addEventListener('message', e => {
    const m = e.data, p = pending.get(m.id);
    if (!p) return;
    pending.delete(m.id);
    if (m.error != null) p.reject({ code: 'tool_error', message: m.error }); else p.resolve(m.result);
  });
  window.futHost = host;

  /* ---------- document store: flat map "collection/id" → data ---------- */
  let docs = {};
  const listeners = new Set();
  let saveT = null;
  const clone = v => v === undefined ? undefined : JSON.parse(JSON.stringify(v));
  const parentOf = p => p.split('/').slice(0, -1).join('/');
  function persist() {
    clearTimeout(saveT);
    saveT = setTimeout(() => host('save', { data: JSON.stringify({ version: 1, docs }) }).catch(() => {}), 400);
  }
  function notify() { queueMicrotask(() => { for (const l of listeners) l(); }); }
  function changed() { notify(); persist(); }
  const docSnap = (path) => {
    const d = docs[path];
    return { id: path.split('/').pop(), exists: d !== undefined, data: () => clone(d), metadata: { fromCache: false, hasPendingWrites: false } };
  };
  const mergeDeep = (a, b) => {
    const out = { ...a };
    for (const [k, v] of Object.entries(b)) out[k] = v && typeof v === 'object' && !Array.isArray(v) && a?.[k] && typeof a[k] === 'object' && !Array.isArray(a[k]) ? mergeDeep(a[k], v) : v;
    return out;
  };
  const newId = () => Date.now().toString(36) + Math.random().toString(36).slice(2, 8);

  function docRef(path) {
    return {
      id: path.split('/').pop(), path,
      async get() { return docSnap(path); },
      async set(data) { docs[path] = clone(data); changed(); },
      async update(data) { if (docs[path] === undefined) throw { code: 'invalid_argument', message: 'Document absent' }; docs[path] = mergeDeep(docs[path], clone(data)); changed(); },
      async delete() { delete docs[path]; changed(); },
      onSnapshot(next) { const l = () => next(docSnap(path)); listeners.add(l); queueMicrotask(l); return () => listeners.delete(l); },
      collection(sub) { return collRef(path + '/' + sub); },
    };
  }
  function query(path, order = null, lim = null) {
    const run = () => {
      let ids = Object.keys(docs).filter(p => parentOf(p) === path);
      if (order) {
        const [f, dir] = order;
        ids.sort((a, b) => { const x = docs[a][f], y = docs[b][f]; if (x == null) return 1; if (y == null) return -1; return (x < y ? -1 : x > y ? 1 : 0) * (dir === 'desc' ? -1 : 1); });
      } else ids.sort();
      if (lim) ids = ids.slice(0, lim);
      const list = ids.map(docSnap);
      return { docs: list, size: list.length, empty: !list.length, docChanges: () => [], metadata: { fromCache: false, hasPendingWrites: false } };
    };
    return {
      path,
      orderBy(f, dir = 'asc') { return query(path, [f, dir], lim); },
      limit(n) { return query(path, order, n); },
      where() { return query(path, order, lim); },
      async get() { return run(); },
      onSnapshot(next) { const l = () => next(run()); listeners.add(l); queueMicrotask(l); return () => listeners.delete(l); },
    };
  }
  function collRef(path) {
    return Object.assign(query(path), {
      doc(id) { return docRef(path + '/' + (id || newId())); },
      async add(data) { const r = docRef(path + '/' + newId()); await r.set(data); return r; },
    });
  }
  const db = { doc: docRef, collection: collRef };
  const ready = host('load').then(raw => { if (raw) { try { docs = JSON.parse(raw).docs || {}; } catch { docs = {}; } } }).catch(() => {});

  /* ---------- browser bridge ---------- */
  const mcp = {
    async callTool(server, tool, input) {
      const text = await host('browser', { tool, input: input || {} });
      return { content: [{ type: 'text', text }], payload: text };
    },
    async listTools() { return { servers: [{ server: 'host:claude_browser', authStatus: 'connected', tools: [] }] }; },
  };
  const user = { async can() { return true; }, async canEdit() { return true; }, isOwner() { return true; }, async id() { return 'local'; } };

  window.claude = {
    async use(name) {
      if (!bridge) return null;
      if (name === 'db') { await ready; return db; }
      if (name === 'mcp') return mcp;
      if (name === 'user') return user;
      return null;
    },
  };
})();
