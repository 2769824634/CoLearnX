import { useAuth } from '../../auth/AuthContext';
import useCachedQuery from '../../api/useCachedQuery';
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
  return useCachedQuery(token, loader, queryKey);
}
