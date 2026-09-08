import { useEffect, useState } from 'react';
import { Link, Navigate, useLocation } from 'react-router-dom';
import Logo from '../../components/Logo';
import useAdminAuth from '../../auth/useAdminAuth';
import '../../styles/admin.css';

export default function AdminLoginPage() {
  const { login, isAuthenticated, booting } = useAdminAuth();
  const location = useLocation();
  const [email, setEmail] = useState('zhu.zirui@colearnx.com');
  const [password, setPassword] = useState('Password123!');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const from = location.state?.from;
  const requestedPath = from?.pathname;
  const destination = requestedPath?.startsWith('/admin/') && requestedPath !== '/admin/login'
    ? `${requestedPath}${from.search || ''}` : '/admin/home';

  useEffect(() => {
    const previousTitle = document.title;
    document.title = 'CoLearnX — Administrator sign-in';
    return () => { document.title = previousTitle; };
  }, []);

  if (!booting && isAuthenticated) {
    return <Navigate to={destination} replace />;
  }

  async function onSubmit(event) {
    event.preventDefault();
    setBusy(true);
    setError('');

    try {
      await login(email, password);
    } catch (err) {
      setError(err.message || 'Administrator sign-in failed.');
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="auth-screen admin-auth-screen">
      <main className="admin-auth-card" aria-labelledby="admin-login-title">
        <section className="admin-auth-intro">
          <Logo />
          <p className="admin-auth-kicker">Invited administrator access</p>
          <h1 id="admin-login-title">Operations console</h1>
          <p className="admin-auth-copy">
            A separate identity boundary protects reviews, credit operations and the audit trail.
          </p>
          <div className="admin-boundary-note">
            <span className="admin-boundary-mark" aria-hidden="true" />
            <div>
              <strong>Independent account</strong>
              <span>This sign-in does not use a learner, trainer or creator role.</span>
            </div>
          </div>
        </section>

        <form className="admin-auth-form" onSubmit={onSubmit}>
          <div>
            <p className="admin-form-eyebrow">Restricted workspace</p>
            <h2>Sign in as administrator</h2>
          </div>
          <div className="form-group">
            <label htmlFor="admin-email">Administrator email</label>
            <input
              id="admin-email"
              type="email"
              autoComplete="username"
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              required
              autoFocus
            />
          </div>
          <div className="form-group">
            <label htmlFor="admin-password">Password</label>
            <input
              id="admin-password"
              type="password"
              autoComplete="current-password"
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              required
            />
          </div>
          {error ? (
            <div className="callout warn admin-auth-error" role="alert">
              <div className="callout-title">Sign-in unsuccessful</div>
              {error}
            </div>
          ) : null}
          <button type="submit" className="btn btn-primary btn-block" disabled={busy}>
            {busy ? 'Verifying…' : 'Enter operations console'}
          </button>
          <p className="admin-demo-credential">
            Demo: zhu.zirui@colearnx.com / Password123!
          </p>
          <Link className="admin-return-link" to="/login">Return to user sign-in</Link>
        </form>
      </main>
    </div>
  );
}
