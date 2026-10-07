import { useEffect, useState } from 'react';
import { Link, Navigate, useNavigate } from 'react-router-dom';
import Logo from '../components/Logo';
import Modal from '../components/Modal';
import PasswordInput from '../components/PasswordInput';
import { authApi } from '../api';
import { consumeSessionReplacedMessage, SESSION_REPLACED_MESSAGE } from '../api/client';
import { useAuth } from '../auth/AuthContext';

const ROLE_META = {
  Member: { label: 'Member', desc: 'Learn & enrol with credits' },
  Trainer: { label: 'Trainer', desc: 'Run courses & issue certs' },
  Creator: { label: 'Creator', desc: 'Upload materials & royalties' },
};

const LEARNING_PATH = [
  ['Choose a course', 'Browse the catalogue and enrol with credits.'],
  ['Learn with your trainer', 'Join a scheduled intake and attend each session.'],
  ['Earn your certificate', 'Request it on completion; it is reviewed before issue.'],
];

function roleMeta(id) {
  return ROLE_META[id] || { label: id, desc: '' };
}

export default function LoginPage() {
  const { login, isAuthenticated, activeRole, booting } = useAuth();
  const navigate = useNavigate();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [roleOpen, setRoleOpen] = useState(false);
  const [availableRoles, setAvailableRoles] = useState([]);
  const [pickedRole, setPickedRole] = useState('');

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
    return <Navigate to={`/${activeRole}/home`} replace />;
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
      if (roles.length === 1) {
        const user = await login(email, password, roles[0]);
        navigate(`/${user.activeRole.toLowerCase()}/home`);
        return;
      }
      setAvailableRoles(roles);
      setPickedRole(roles[0]);
      setRoleOpen(true);
    } catch (err) {
      setError(err.message || 'Login failed');
    } finally {
      setBusy(false);
    }
  }

  async function onContinue() {
    if (!pickedRole) return;
    setBusy(true);
    setError('');
    try {
      const user = await login(email, password, pickedRole);
      setRoleOpen(false);
      navigate(`/${user.activeRole.toLowerCase()}/home`);
    } catch (err) {
      setError(err.message || 'Login failed');
      setRoleOpen(false);
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
              <p>One account for every workspace</p>
              <ul className="login-workspaces">
                {['Member', 'Trainer', 'Creator'].map((id) => (
                  <li key={id} data-role={id.toLowerCase()}>
                    <span className="login-workspace-dot" aria-hidden="true" />
                    {ROLE_META[id].label}
                  </li>
                ))}
              </ul>
            </div>
          </section>
          <form className="login-form" onSubmit={onSubmit}>
            <p className="login-eyebrow">Welcome back</p>
            <h2>Login to your account</h2>
            <div className="form-group">
            <label htmlFor="login-email">Email</label>
            <input id="login-email" type="email" autoComplete="username" spellCheck={false} value={email} onChange={(e) => setEmail(e.target.value)} required />
            </div>
            <div className="form-group">
            <div className="login-label-row">
              <label htmlFor="login-password">Password</label>
              <Link to="/forgot-password">Forgot password?</Link>
            </div>
            <PasswordInput id="login-password" value={password} onChange={(e) => setPassword(e.target.value)} />
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
              {busy && !roleOpen ? 'Signing in…' : 'Sign in'}
            </button>
            <p className="login-register">
              New here? <Link to="/register">Create an account</Link>
            </p>
            <Link className="login-home-link" to="/">Back to home</Link>
          </form>
        </main>
      </div>

      <Modal
        open={roleOpen}
        title="Continue as"
        onClose={() => {
          if (!busy) setRoleOpen(false);
        }}
      >
        <div className="role-pick" style={{ margin: 0 }}>
          {availableRoles.map((id) => {
            const meta = roleMeta(id);
            return (
              <button
                key={id}
                type="button"
                data-role={id.toLowerCase()}
                className={`role-option${pickedRole === id ? ' selected' : ''}`}
                onClick={() => setPickedRole(id)}
              >
                <div className="radio" />
                <div className="meta">
                  <strong>{meta.label}</strong>
                  <span>{meta.desc}</span>
                </div>
                {pickedRole === id ? <span className="pill">Selected</span> : null}
              </button>
            );
          })}
        </div>
        <button
          type="button"
          className="btn btn-primary btn-block"
          style={{ marginTop: 16 }}
          disabled={busy || !pickedRole}
          onClick={onContinue}
        >
          {busy ? 'Signing in…' : `Continue as ${pickedRole}`}
        </button>
      </Modal>
    </>
  );
}
