import { Link } from 'react-router-dom';
import Logo from '../components/Logo';
import { useAuth } from '../auth/AuthContext';
import { SITE_TITLE } from '../siteTitle';

export default function PublicHomePage() {
  const { booting, isAuthenticated, activeRole } = useAuth();
  const workspace = activeRole ? `/${activeRole}/home` : '/login';

  return (
    <div className="public-home">
      <main className="public-home-card">
        <Logo />
        <h1>{SITE_TITLE}</h1>
        <p>Your Path to What's neXt.</p>
        {booting ? null : isAuthenticated ? (
          <div className="public-home-actions">
            <Link className="btn btn-primary" to={workspace}>Continue to workspace</Link>
          </div>
        ) : (
          <div className="public-home-actions">
            <Link className="btn btn-primary" to="/login">Sign in</Link>
            <Link className="btn btn-ghost" to="/register">Create an account</Link>
          </div>
        )}
      </main>
    </div>
  );
}
