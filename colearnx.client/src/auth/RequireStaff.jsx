import { Navigate, Outlet, useLocation } from 'react-router-dom';
import useStaffAuth from './useStaffAuth';

export default function RequireStaff() {
  const { booting, isAuthenticated } = useStaffAuth();
  const location = useLocation();
  if (booting) return <div className="auth-screen" role="status">Checking staff session…</div>;
  if (!isAuthenticated) return <Navigate to="/staff/login" replace state={{ from: location }} />;
  return <Outlet />;
}
