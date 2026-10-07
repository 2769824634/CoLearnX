import { useEffect, useState } from 'react';
import { Link, Navigate, useLocation } from 'react-router-dom';
import Logo from '../../components/Logo';
import PasswordInput from '../../components/PasswordInput';
import { consumeSessionReplacedMessage, SESSION_REPLACED_MESSAGE } from '../../api/client';
import useAdminAuth from '../../auth/useAdminAuth';
import { useNoCredentialAutofill } from '../../auth/noCredentialAutofill';
import '../../styles/admin.css';

const CONSOLE_AREAS = [
  ['Approvals', 'Role requests, courses and materials'],
  ['Users', 'Account lookup by name, email or ID'],
  ['Credit Ledger', 'Balance changes and corrections'],
  ['Disputes', 'Cases and refunds'],
  ['Audit Log', 'Recorded decisions and sign-ins'],
];

export default function AdminLoginPage() {
  const { login, isAuthenticated, booting } = useAdminAuth();
  const location = useLocation();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const emailAutofill = useNoCredentialAutofill();
  const passwordAutofill = useNoCredentialAutofill();
  const from = location.state?.from;
  const requestedPath = from?.pathname;
  const destination = requestedPath?.startsWith('/admin/') && requestedPath !== '/admin/login'
    ? `${requestedPath}${from.search || ''}` : '/admin/home';

  useEffect(() => {
    if (booting) return undefined;
    const replaced = consumeSessionReplacedMessage(true);
    if (!replaced) return undefined;
    let active = true;
    queueMicrotask(() => {
      if (active) setError(replaced);
    });
    return () => { active = false; };
  }, [booting]);

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
    <div className="auth-screen admin-auth-screen admin-console-screen">
      <header className="admin-console-bar">
        <Logo tone="dark" />
        <Link className="admin-return-link" to="/login">Return to user sign-in</Link>
      </header>
      <main className="admin-console-layout" aria-labelledby="admin-login-title">
        <section className="admin-console-intro">
          <p className="admin-auth-kicker">
            <span className="admin-restricted-dot" aria-hidden="true" />
            Invited administrator access
          </p>
          <h1 id="admin-login-title">Operations console</h1>
          <p className="admin-auth-copy">
            A separate identity boundary protects reviews, credit operations and the audit trail.
          </p>
          <ol className="admin-console-scope">
            {CONSOLE_AREAS.map(([label, desc], index) => (
              <li key={label}>
                <span aria-hidden="true">{String(index + 1).padStart(2, '0')}</span>
                <strong>{label}</strong>
                <em>{desc}</em>
              </li>
            ))}
          </ol>
        </section>

        <form className="admin-console-form" autoComplete="off" onSubmit={onSubmit}>
          <div>
            <p className="admin-console-eyebrow">Restricted workspace</p>
            <h2>Sign in as administrator</h2>
          </div>
          <div className="admin-boundary-note">
            <span className="admin-boundary-mark" aria-hidden="true" />
            <div>
              <strong>Independent account</strong>
              <span>This sign-in does not use a learner, trainer or creator role.</span>
            </div>
          </div>
          {location.state?.accessNotice ? (
            <div className="callout warn" role="status">{location.state.accessNotice}</div>
          ) : null}
          <div className="form-group">
            <label htmlFor="admin-email">Administrator email</label>
            <input
              id="admin-email"
              type="email"
              value={email}
              onChange={(event) => { setEmail(event.target.value); setError(''); }}
              required
              {...emailAutofill}
            />
          </div>
          <div className="form-group">
            <label htmlFor="admin-password">Password</label>
            <PasswordInput
              id="admin-password"
              label={null}
              value={password}
              onChange={(event) => { setPassword(event.target.value); setError(''); }}
              required
              {...passwordAutofill}
            />
          </div>
          {error ? (
            <div className="callout warn admin-auth-error" role="alert">
              <div className="callout-title">{error === SESSION_REPLACED_MESSAGE ? 'Signed out' : 'Sign-in unsuccessful'}</div>
              {error}
            </div>
          ) : null}
          <button type="submit" className="btn btn-block admin-console-submit" disabled={busy}>
            {busy ? 'Verifying…' : 'Enter operations console'}
          </button>
        </form>
      </main>
    </div>
  );
}
