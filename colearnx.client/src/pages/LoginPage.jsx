import { useState } from 'react';
import { Navigate, useNavigate } from 'react-router-dom';
import Logo from '../components/Logo';
import Modal from '../components/Modal';
import { authApi } from '../api';
import { useAuth } from '../auth/AuthContext';

const ROLE_META = {
  Member: { label: 'Member', desc: 'Learn & enrol with credits' },
  Trainer: { label: 'Trainer', desc: 'Run courses & issue certs' },
  Creator: { label: 'Creator', desc: 'Upload materials & royalties' },
  Admin: { label: 'Admin', desc: 'Approvals, ledger & audit' },
};

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
  const [forgotOpen, setForgotOpen] = useState(false);
  const [roleOpen, setRoleOpen] = useState(false);
  const [availableRoles, setAvailableRoles] = useState([]);
  const [pickedRole, setPickedRole] = useState('');

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
              {busy && !roleOpen ? 'Signing in…' : 'Sign in'}
            </button>
          </form>
        </div>
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

      <Modal open={forgotOpen} title="Reset Password" onClose={() => setForgotOpen(false)}>
        <p style={{ fontSize: 13, color: 'var(--slate)' }}>Enter the email on your account and we will send a reset link.</p>
        <button type="button" className="btn btn-primary btn-block" onClick={() => setForgotOpen(false)}>
          Close
        </button>
      </Modal>
    </>
  );
}
