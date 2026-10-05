import { createContext, useCallback, useContext, useEffect } from 'react';

export const MemberDataContext = createContext(null);

export function useMemberData() {
  const context = useContext(MemberDataContext);
  if (!context) throw new Error('useMemberData must be used within MemberDataProvider');
  return context;
}

// Pages ask for the slices they render. Injected test contexts without ensure keep their own state.
export function useMemberSlices(...names) {
  const data = useMemberData();
  const key = names.join('|');
  useEffect(() => {
    if (typeof data.ensure === 'function') data.ensure(key.split('|'));
  }, [data.ensure, key]);

  const reload = useCallback(() => {
    if (typeof data.reload !== 'function') return undefined;
    if (typeof data.ensure !== 'function') return data.reload();
    return data.reload(key.split('|'));
  }, [data.reload, data.ensure, key]);

  if (typeof data.ensure !== 'function') return { ...data, reload };
  const slices = data.state.slices || {};
  const loading = names.some((name) => slices[name] !== 'ready' && slices[name] !== 'error');
  const loadError = names.map((name) => data.state.sliceErrors?.[name]).find(Boolean) || '';
  return { ...data, reload, state: { ...data.state, loading, loadError } };
}
