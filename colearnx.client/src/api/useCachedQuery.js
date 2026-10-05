import { useEffect, useState } from 'react';
import { invalidate, peek, put, queryKeyFor } from './readCache';

function matches(snapshot, token, loader, queryKey, revision) {
  return snapshot?.token === token
    && snapshot?.loader === loader
    && snapshot?.queryKey === queryKey
    && snapshot?.revision === revision;
}

// Remounting a page reuses the last successful result. refresh() asks for a new one.
export default function useCachedQuery(token, loader, queryKey = '') {
  const cacheKey = queryKeyFor(token, loader, queryKey);
  const cached = peek(cacheKey);
  const [revision, setRevision] = useState(() => cached?.revision || 0);
  const [snapshot, setSnapshot] = useState(() => cached || null);
  const current = matches(snapshot, token, loader, queryKey, revision);

  useEffect(() => {
    const hit = peek(cacheKey);
    if (matches(hit, token, loader, queryKey, revision)) {
      setSnapshot(hit);
      return undefined;
    }
    const controller = new AbortController();
    const identity = { token, loader, queryKey, revision };
    loader(token, queryKey, controller.signal).then(
      (data) => {
        if (controller.signal.aborted) return;
        const next = { ...identity, data, error: null };
        put(cacheKey, next);
        setSnapshot(next);
      },
      (error) => {
        if (!controller.signal.aborted) setSnapshot({ ...identity, data: null, error });
      },
    );
    return () => controller.abort();
  }, [cacheKey, token, loader, queryKey, revision]);

  return {
    token,
    data: current ? snapshot.data : null,
    error: current ? snapshot.error : null,
    loading: !current,
    refresh: () => {
      invalidate(cacheKey);
      setRevision((value) => value + 1);
    },
    setData: (data) => {
      const next = { token, loader, queryKey, revision, data, error: null };
      put(cacheKey, next);
      setSnapshot(next);
    },
  };
}
