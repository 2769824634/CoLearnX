export function resolveAuthSession(auth = {}) {
  if (auth.booting || (auth.token && !auth.user && !auth.isAuthenticated)) return 'booting';
  const role = String(auth.activeRole || auth.user?.activeRole || '').toLowerCase();
  if (auth.isAuthenticated && role === 'member') return 'member';
  if (auth.isAuthenticated) return 'signed-in';
  return 'guest';
}
