import { useAuth } from '../../auth/AuthContext';
import useCachedQuery from '../../api/useCachedQuery';
import { courseTrainersApi } from '../../api/courseTrainers';
import { trainerIntakesApi } from '../../api/trainerIntakes';

export async function loadTrainerOverview(token, key, signal) {
  const [intakes, catalog, invitations] = await Promise.all([
    trainerIntakesApi.list(token, signal),
    loadCatalog(token, key, signal),
    courseTrainersApi.myInvitations(token, signal),
  ]);
  return { intakes, invitations, ...catalog };
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
export default function useTrainerQuery(loader, queryKey = '', tokenOverride = null) {
  const { token } = useAuth();
  return useCachedQuery(tokenOverride || token, loader, queryKey);
}
