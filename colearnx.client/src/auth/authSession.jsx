export function resolveAuthSession(auth = {}) {
  if (auth.booting || (auth.token && !auth.user && !auth.isAuthenticated)) return 'booting';
  const role = String(auth.activeRole || auth.user?.activeRole || '').toLowerCase();
  if (auth.isAuthenticated && role === 'member') return 'member';
  if (auth.isAuthenticated) return 'signed-in';
  return 'guest';
}

export function SessionLoading() {
  return (
    <div className="app-wrap">
      <div className="auth-screen">
        <p role="status">Loading session…</p>
      </div>
    </div>
  );
}
