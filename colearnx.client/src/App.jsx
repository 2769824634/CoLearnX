import { useEffect } from 'react';
import { BrowserRouter } from 'react-router-dom';
import { AuthProvider } from './auth/AuthProvider';
import AdminAuthProvider from './auth/AdminAuthProvider';
import AppRouter from './routes/AppRouter';
import { SITE_TITLE } from './siteTitle';
import './styles/member.css';

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
