import useAdminAuth from '../../auth/useAdminAuth';
import useCachedQuery from '../../api/useCachedQuery';

// Loaders live outside components; a primitive query key identifies each request.
export default function useAdminQuery(loader, queryKey = '') {
  const { token } = useAdminAuth();
  const { data, error, loading, refresh } = useCachedQuery(token, loader, queryKey);
  return { data, error, loading, refresh };
}
