import { useEffect, useState } from 'react';
import { useAuth } from '../../auth/AuthContext';
import { trainerIntakesApi } from '../../api/trainerIntakes';

export async function loadTrainerOverview(token, key, signal) {
  const [intakes, catalog] = await Promise.all([
    trainerIntakesApi.list(token, signal),
    loadCatalog(token, key, signal),
  ]);
  return { intakes, ...catalog };
}

async function loadCatalog(token, key, signal) {
  try { return { courses: await trainerIntakesApi.publishedCourses(token, signal), catalogError: null }; }
  catch (catalogError) { return { courses: [], catalogError }; }
}

export async function loadTrainerDetail(token, id, signal) {
  const [intake, catalog] = await Promise.all([trainerIntakesApi.get(token, id, signal), loadCatalog(token, id, signal)]);
  return { intake, ...catalog };
}

// Identity-scoped snapshots prevent a late request from another route/token replacing current data.
export default function useTrainerQuery(loader, queryKey = '') {
  const { token } = useAuth();
  const [revision, setRevision] = useState(0);
  const [snapshot, setSnapshot] = useState(null);
  const current = snapshot?.token === token && snapshot?.loader === loader && snapshot?.queryKey === queryKey && snapshot?.revision === revision;

  useEffect(() => {
    const controller = new AbortController();
    const identity = { token, loader, queryKey, revision };
    loader(token, queryKey, controller.signal).then(
      (data) => { if (!controller.signal.aborted) setSnapshot({ ...identity, data }); },
      (error) => { if (!controller.signal.aborted) setSnapshot({ ...identity, error }); },
    );
    return () => controller.abort();
  }, [token, loader, queryKey, revision]);

  return {
    token, data: current ? snapshot.data : null, error: current ? snapshot.error : null, loading: !current,
    refresh: () => setRevision((value) => value + 1),
    setData: (data) => setSnapshot({ token, loader, queryKey, revision, data }),
  };
}
