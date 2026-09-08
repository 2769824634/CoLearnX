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
    return <Navigate to={`/${activeRole}/home`} replace />;
  }

  return <Outlet />;
}
