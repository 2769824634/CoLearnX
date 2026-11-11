import { useEffect } from 'react';
import { BrowserRouter } from 'react-router-dom';
import { AuthProvider } from './auth/AuthProvider';
import AdminAuthProvider from './auth/AdminAuthProvider';
import AppRouter from './routes/AppRouter';
import { SITE_TITLE } from './siteTitle';
import './styles/member.css';
import './styles/base-v10.css';
import './styles/shell-v10.css';
import './styles/auth-v10.css';
import './styles/catalog-v10.css';
import './styles/course-detail-v10.css';
import './styles/member-v10.css';
import './styles/wallet-account-v10.css';

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
          <AppRouter />
        </AdminAuthProvider>
      </AuthProvider>
    </BrowserRouter>
  );
}
