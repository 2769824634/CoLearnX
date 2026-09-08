import { BrowserRouter } from 'react-router-dom';
import { AuthProvider } from './auth/AuthProvider';
import AdminAuthProvider from './auth/AdminAuthProvider';
import AppRouter from './routes/AppRouter';
import './styles/member.css';

export default function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <AdminAuthProvider>
          <AppRouter />
        </AdminAuthProvider>
      </AuthProvider>
    </BrowserRouter>
  );
}
