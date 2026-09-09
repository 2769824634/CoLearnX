import { Navigate, Outlet, useLocation } from 'react-router-dom';
import useAdminAuth from './useAdminAuth';

export default function RequireAdmin() {
  const { isAuthenticated, booting } = useAdminAuth();
  const location = useLocation();

  if (booting) {
    return (
      <div className="auth-screen admin-auth-screen" role="status">
        <p className="admin-session-status">Checking administrator session…</p>
      </div>
    );
  }

  if (!isAuthenticated) {
    return <Navigate to="/admin/login" replace state={{ from: location }} />;
  }

  return <Outlet />;
}
