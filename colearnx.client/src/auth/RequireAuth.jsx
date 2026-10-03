import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { useAuth } from './AuthContext';

// Route guard by active role.
export function RequireAuth({ role }) {
  const { isAuthenticated, booting, activeRole } = useAuth();
  const location = useLocation();

  if (booting) {
    return (
      <div className="app-wrap">
        <div className="auth-screen">
          <p style={{ color: 'var(--slate)' }}>Loading session…</p>
        </div>
      </div>
    );
  }

  if (!isAuthenticated) {
    return <Navigate to="/login" replace state={{ from: location }} />;
  }

  if (role && activeRole !== role) {
    const roleLabel = role.charAt(0).toUpperCase() + role.slice(1);
    const activeLabel = activeRole ? activeRole.charAt(0).toUpperCase() + activeRole.slice(1) : 'active';
    return (
      <Navigate
        to={`/${activeRole}/home`}
        replace
        state={{ accessNotice: `This page requires the ${roleLabel} workspace. You were returned to your ${activeLabel} workspace.` }}
      />
    );
  }

  return <Outlet />;
}
