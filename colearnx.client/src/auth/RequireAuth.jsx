import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { useAuth } from './AuthContext';

// Route guard by active role.
export function RequireAuth({ role }) {
  const { isAuthenticated, booting, activeRole, user } = useAuth();
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

  // An account has one role, so a page of another role simply leads to the account's own home.
  if (role && activeRole !== role) {
    return <Navigate to={`/${activeRole}/home`} replace />;
  }

  // Uncertified Trainer and Creator accounts stay on their certification page. The server enforces it too.
  const certificationPath = `/${activeRole}/certification`;
  if ((activeRole === 'trainer' || activeRole === 'creator') && user?.certifiedAt === null
    && location.pathname !== certificationPath) {
    return <Navigate to={certificationPath} replace />;
  }

  return <Outlet />;
}
