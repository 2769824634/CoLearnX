import { useState } from 'react';
import { Link, Navigate, useNavigate } from 'react-router-dom';
import Logo from '../components/Logo';
import { useAuth } from '../auth/AuthContext';

const CHECKS = [
  { id: 'len', label: '10–72 characters', test: (p) => p.length >= 10 && p.length <= 72 },
  { id: 'case', label: 'Upper and lowercase letters', test: (p) => /[a-z]/.test(p) && /[A-Z]/.test(p) },
  { id: 'num', label: 'A number', test: (p) => /\d/.test(p) },
  { id: 'sym', label: 'A symbol', test: (p) => /[^A-Za-z0-9]/.test(p) },
];

function passwordReady(password, email) {
  if (!CHECKS.every((item) => item.test(password))) return false;
  const local = email.split('@')[0] || '';
  if (email && password.toLowerCase().includes(email.toLowerCase())) return false;
  if (local.length >= 3 && password.toLowerCase().includes(local.toLowerCase())) return false;
  return true;
}

export default function RegisterPage() {
  const { register, isAuthenticated, activeRole, booting } = useAuth();
  const navigate = useNavigate();
  const [fullName, setFullName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [confirm, setConfirm] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');

  if (!booting && isAuthenticated && activeRole) {
    return <Navigate to={`/${activeRole}/home`} replace />;
  }

  async function onSubmit(e) {
    e.preventDefault();
    setError('');
    if (password !== confirm) {
      setError('Passwords do not match.');
      return;
    }
    if (!passwordReady(password, email)) {
      setError('Choose a stronger password that does not include your email.');
      return;
    }
    setBusy(true);
    try {
      const user = await register({ email, password, fullName });
      navigate(`/${(user.activeRole || 'Member').toLowerCase()}/home`);
    } catch (err) {
      setError(err.message || 'Registration failed');
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="auth-screen">
      <div className="auth-card">
        <div className="auth-header">
          <Logo />
        </div>
        <form className="auth-body" onSubmit={onSubmit} autoComplete="on">
          <h2>Create your account</h2>
          <p className="auth-lead">New accounts start as Member. Trainer and Creator roles are granted later.</p>
          <div className="form-group">
            <label htmlFor="register-name">Full name</label>
            <input id="register-name" name="name" autoComplete="name" value={fullName} onChange={(e) => setFullName(e.target.value)} required minLength={2} maxLength={80} />
          </div>
          <div className="form-group">
            <label htmlFor="register-email">Email</label>
            <input id="register-email" name="email" type="email" autoComplete="username" spellCheck={false} value={email} onChange={(e) => setEmail(e.target.value)} required maxLength={254} />
          </div>
          <div className="form-group">
            <label htmlFor="register-password">Password</label>
            <input id="register-password" name="new-password" type="password" autoComplete="new-password" value={password} onChange={(e) => setPassword(e.target.value)} required minLength={10} maxLength={72} />
          </div>
          <ul className="password-checks">
            {CHECKS.map((item) => (
              <li key={item.id} className={item.test(password) ? 'ok' : ''}>{item.label}</li>
            ))}
          </ul>
          <div className="form-group">
            <label htmlFor="register-confirm">Confirm password</label>
            <input id="register-confirm" type="password" autoComplete="new-password" value={confirm} onChange={(e) => setConfirm(e.target.value)} required minLength={10} maxLength={72} />
          </div>
          {error ? (
            <div className="callout warn" style={{ marginBottom: 12 }}>
              <div className="callout-title">Could not create account</div>
              {error}
            </div>
          ) : null}
          <button type="submit" className="btn btn-primary btn-block" disabled={busy}>
            {busy ? 'Creating account…' : 'Create account'}
          </button>
          <p className="auth-footer">
            Already have an account? <Link to="/login">Sign in</Link>
          </p>
        </form>
      </div>
    </div>
  );
}
