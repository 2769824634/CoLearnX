import { useState } from 'react';
import { Navigate, useNavigate } from 'react-router-dom';
import Logo from '../components/Logo';
import Modal from '../components/Modal';
import { useAuth } from '../auth/AuthContext';

const ROLES = [
  { id: 'Member', label: 'Member', desc: 'Learn & enrol with credits' },
  { id: 'Trainer', label: 'Trainer', desc: 'Run courses & issue certs' },
  { id: 'Creator', label: 'Creator', desc: 'Upload materials & royalties' },
  { id: 'Admin', label: 'Admin', desc: 'Approvals, ledger & audit' },
];

// Login + Continue as role. Shared: LoginPage
export default function LoginPage() {
  const { login, isAuthenticated, activeRole, booting } = useAuth();
  const navigate = useNavigate();
  const [role, setRole] = useState('Member');
  const [email, setEmail] = useState('huang.yousheng@colearnx.com');
  const [password, setPassword] = useState('Password123!');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [forgotOpen, setForgotOpen] = useState(false);

  if (!booting && isAuthenticated && activeRole) {
    return <Navigate to={`/${activeRole}/home`} replace />;
  }

  async function onSubmit(e) {
    e.preventDefault();
    setBusy(true);
    setError('');
    try {
      const user = await login(email, password, role);
      navigate(`/${user.activeRole.toLowerCase()}/home`);
    } catch (err) {
      setError(err.message || 'Login failed');
    } finally {
      setBusy(false);
    }
  }

  return (
    <>
      <div className="auth-screen">
        <div className="auth-card">
          <div className="auth-header">
            <Logo />
          </div>
          <form className="auth-body" onSubmit={onSubmit}>
            <h2>Login to your account</h2>
            <div className="form-group">
              <label>Email</label>
              <input type="email" value={email} onChange={(e) => setEmail(e.target.value)} required />
            </div>
            <div className="form-group">
              <label>Password</label>
              <input type="password" value={password} onChange={(e) => setPassword(e.target.value)} required />
            </div>
            <div className="role-pick">
              <div className="role-pick-label">Continue as</div>
              <div className="role-pick-hint">Pick the workspace — JWT active_role claim drives shell & API (BR-06).</div>
              {ROLES.map((r) => (
                <button
                  key={r.id}
                  type="button"
                  className={`role-option${role === r.id ? ' selected' : ''}`}
                  onClick={() => setRole(r.id)}
                >
                  <div className="radio" />
                  <div className="meta">
                    <strong>{r.label}</strong>
                    <span>{r.desc}</span>
                  </div>
                  {role === r.id ? <span className="pill">Selected</span> : null}
                </button>
              ))}
            </div>
            {error ? (
              <div className="callout warn" style={{ marginBottom: 12 }}>
                <div className="callout-title">Login failed</div>
                {error}
              </div>
            ) : null}
            <div style={{ textAlign: 'right', margin: '8px 0' }}>
              <a
                href="#forgot"
                style={{ color: 'var(--purple)', fontSize: 12 }}
                onClick={(e) => {
                  e.preventDefault();
                  setForgotOpen(true);
                }}
              >
                Forgot password?
              </a>
            </div>
            <button type="submit" className="btn btn-primary btn-block" disabled={busy}>
              {busy ? 'Signing in…' : `Continue as ${role}`}
            </button>
            <div className="auth-footer" style={{ marginTop: 12 }}>
              Demo: huang.yousheng@colearnx.com / Password123!
            </div>
          </form>
        </div>
      </div>

      <Modal open={forgotOpen} title="Reset Password" onClose={() => setForgotOpen(false)}>
        <p style={{ fontSize: 13, color: 'var(--slate)' }}>Password reset API will be wired in a later iteration.</p>
        <button type="button" className="btn btn-primary btn-block" onClick={() => setForgotOpen(false)}>
          Close
        </button>
      </Modal>
    </>
  );
}
