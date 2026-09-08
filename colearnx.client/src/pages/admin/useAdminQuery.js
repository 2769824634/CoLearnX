import { useEffect, useState } from 'react';
import useAdminAuth from '../../auth/useAdminAuth';

// Loaders live outside components; a primitive query key identifies each request.
export default function useAdminQuery(loader, queryKey = '') {
  const { token } = useAdminAuth();
  const [revision, setRevision] = useState(0);
  const [snapshot, setSnapshot] = useState(null);
  const current = snapshot?.token === token && snapshot?.loader === loader
    && snapshot?.queryKey === queryKey && snapshot?.revision === revision;

  useEffect(() => {
    const controller = new AbortController();
    const identity = { token, loader, queryKey, revision };
    loader(token, queryKey, controller.signal).then(
      (data) => {
        if (!controller.signal.aborted) setSnapshot({ ...identity, data, error: null });
      },
      (error) => {
        if (!controller.signal.aborted) setSnapshot({ ...identity, data: null, error });
      },
    );
    return () => controller.abort();
  }, [loader, token, queryKey, revision]);

  return {
    data: current ? snapshot.data : null,
    error: current ? snapshot.error : null,
    loading: !current,
    refresh: () => setRevision((value) => value + 1),
  };
}
