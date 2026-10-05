export function courseListKey(token) {
  return `courses:${token || 'guest'}`;
}

const store = new Map();
const loaderIds = new WeakMap();
let nextLoaderId = 1;

export function queryKeyFor(token, loader, queryKey) {
  let id = loaderIds.get(loader);
  if (!id) {
    id = nextLoaderId;
    nextLoaderId += 1;
    loaderIds.set(loader, id);
  }
  return `query:${token || ''}:${id}:${String(queryKey)}`;
}

function hasData(entry) {
  return Boolean(entry) && Object.prototype.hasOwnProperty.call(entry, 'data');
}

export function peek(key) {
  const entry = store.get(key);
  return hasData(entry) ? entry.data : undefined;
}

export function put(key, data) {
  store.set(key, { data });
}

export function invalidate(key) {
  store.delete(key);
}

export function loadOnce(key, factory) {
  const existing = store.get(key);
  if (hasData(existing)) return Promise.resolve(existing.data);
  if (existing?.promise) return existing.promise;
  const promise = Promise.resolve().then(factory).then(
    (data) => {
      store.set(key, { data });
      return data;
    },
    (error) => {
      if (store.get(key)?.promise === promise) store.delete(key);
      throw error;
    },
  );
  store.set(key, { promise });
  return promise;
}

export function clearReadCache() {
  store.clear();
}
