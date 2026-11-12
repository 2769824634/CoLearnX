import { useEffect, useState } from 'react';
import { Link, Navigate, useLocation, useNavigate } from 'react-router-dom';
import Logo from '../components/Logo';
import PasswordInput from '../components/PasswordInput';
import { authApi } from '../api';
import { consumeSessionReplacedMessage, SESSION_REPLACED_MESSAGE } from '../api/client';
import { useAuth } from '../auth/AuthContext';
import { useNoCredentialAutofill } from '../auth/noCredentialAutofill';

const LEARNING_PATH = [
  ['Choose a course', 'Browse the catalogue and enrol with credits.'],
  ['Learn with your trainer', 'Join a scheduled intake and attend each session.'],
  ['Earn your certificate', 'Request it on completion; it is reviewed before issue.'],
];

function memberReturnPath(from) {
  const path = typeof from === 'string' ? from : `${from?.pathname || ''}${from?.search || ''}`;
  if (!path.startsWith('/') || path.startsWith('//') || path.startsWith('/login') || path.startsWith('/admin')) return null;
  return path;
}

// The page someone was on before signing in is reopened only when it belongs to the role they sign in with.
const WORKSPACES = ['member', 'trainer', 'creator'];
function returnPathFor(path, role) {
  if (!path) return null;
  const area = path.split(/[/?#]/)[1];
  return WORKSPACES.includes(area) && area !== String(role || '').toLowerCase() ? null : path;
}

export default function LoginPage() {
  const { login, isAuthenticated, activeRole, booting } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const returnPath = memberReturnPath(location.state?.from);
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const emailAutofill = useNoCredentialAutofill();
  const passwordAutofill = useNoCredentialAutofill();

  useEffect(() => {
    if (booting) return undefined;
    const replaced = consumeSessionReplacedMessage();
    if (!replaced) return undefined;
    let active = true;
    queueMicrotask(() => {
      if (active) setError(replaced);
    });
    return () => { active = false; };
  }, [booting]);

  if (!booting && isAuthenticated && activeRole) {
    return <Navigate to={returnPathFor(returnPath, activeRole) || `/${activeRole}/home`} replace />;
  }

  async function onSubmit(e) {
    e.preventDefault();
    setBusy(true);
    setError('');
    try {
      const result = await authApi.availableRoles(email, password);
      const roles = result.roles || [];
      if (roles.length === 0) {
        setError('No role is enabled for this account.');
        return;
      }
      const user = await login(email, password, roles[0]);
      navigate(returnPathFor(returnPath, user.activeRole) || `/${user.activeRole.toLowerCase()}/home`);
    } catch (err) {
      setError(err.message || 'Login failed');
    } finally {
      setBusy(false);
    }
  }

  return (
    <>
      <div className="auth-screen login-screen">
        <main className="login-card">
          <section className="login-intro">
            <Logo />
            <p className="login-kicker">Trainer-led learning</p>
            <h1>Your path to what&apos;s neXt.</h1>
            <ol className="login-path">
              {LEARNING_PATH.map(([title, desc]) => (
                <li key={title}>
                  <strong>{title}</strong>
                  <span>{desc}</span>
                </li>
              ))}
            </ol>
            <div className="login-workspaces-block">
              <p>Each account has one role: Member, Trainer or Creator.</p>
            </div>
          </section>
          <form className="login-form" autoComplete="off" onSubmit={onSubmit}>
            <p className="login-eyebrow">Welcome back</p>
            <h2>Login to your account</h2>
            <div className="form-group">
            <label htmlFor="login-email">Email</label>
            <input id="login-email" type="email" spellCheck={false} value={email} onChange={(e) => setEmail(e.target.value)} required {...emailAutofill} />
            </div>
            <div className="form-group">
            <div className="login-label-row">
              <label htmlFor="login-password">Password</label>
              <Link to="/forgot-password">Forgot password?</Link>
            </div>
            <PasswordInput id="login-password" label={null} value={password} onChange={(e) => setPassword(e.target.value)} required {...passwordAutofill} />
            </div>
            {error ? (
              <div className="callout warn" style={{ marginBottom: 12 }}>
                <div className="callout-title">{error === SESSION_REPLACED_MESSAGE ? 'Signed out' : 'Login failed'}</div>
                {error}
                {error.includes('Verify your email') ? (
                  <p className="auth-footer" style={{ marginBottom: 0 }}>
                    <button
                      type="button"
                      className="btn btn-ghost"
                      disabled={busy || !email}
                      onClick={async () => {
                        setBusy(true);
                        try {
                          const result = await authApi.resendVerification(email);
                          setError(result.message || 'If the account is eligible, a verification link will be sent.');
                        } catch (err) {
                          setError(err.message || 'Could not resend the verification email.');
                        } finally {
                          setBusy(false);
                        }
                      }}
                    >
                      Resend verification email
                    </button>
                  </p>
                ) : null}
              </div>
            ) : null}
            <button type="submit" className="btn btn-primary btn-block" disabled={busy}>
              {busy ? 'Signing in…' : 'Sign in'}
            </button>
            <p className="login-register">
              New here? <Link to="/register">Create an account</Link>
            </p>
            <Link className="login-home-link" to="/">Back to home</Link>
          </form>
        </main>
      </div>

    </>
  );
}
