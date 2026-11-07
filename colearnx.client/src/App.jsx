import { useEffect } from 'react';
import { BrowserRouter } from 'react-router-dom';
import { AuthProvider } from './auth/AuthProvider';
import AdminAuthProvider from './auth/AdminAuthProvider';
import StaffAuthProvider from './auth/StaffAuthProvider';
import AppRouter from './routes/AppRouter';
import { SITE_TITLE } from './siteTitle';
import './styles/member.css';
import './styles/base-v10.css';
import './styles/shell-v10.css';
import './styles/auth-v10.css';
import './styles/catalog-v10.css';
import './styles/course-detail-v10.css';

function SiteTitle() {
  useEffect(() => {
    document.title = SITE_TITLE;
  }, []);
  return null;
}

export default function App() {
  return (
    <BrowserRouter>
      <SiteTitle />
      <AuthProvider>
        <AdminAuthProvider>
          <StaffAuthProvider>
            <AppRouter />
          </StaffAuthProvider>
        </AdminAuthProvider>
      </AuthProvider>
    </BrowserRouter>
  );
}
