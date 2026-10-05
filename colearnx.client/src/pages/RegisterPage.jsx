import { useState } from 'react';
import { Link, Navigate, useNavigate } from 'react-router-dom';
import Logo from '../components/Logo';
import PasswordInput from '../components/PasswordInput';
import { authApi } from '../api';
import { useAuth } from '../auth/AuthContext';
import { maskEmail } from '../data/memberMock';
import { PASSWORD_RULES, passwordIssues } from './passwordRules';

export default function RegisterPage() {
  const { register, isAuthenticated, activeRole, booting } = useAuth();
  const navigate = useNavigate();
  const [fullName, setFullName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [confirm, setConfirm] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');
  const [resendNote, setResendNote] = useState('');

  if (!booting && isAuthenticated && activeRole) {
    return <Navigate to={`/${activeRole}/home`} replace />;
  }

  async function onSubmit(e) {
    e.preventDefault();
    setError('');
    setMessage('');
    if (password !== confirm) {
      setError('Passwords do not match.');
      return;
    }
    const issues = passwordIssues(password, email);
    if (issues.length > 0) {
      setError(issues.join(' '));
      return;
    }
    setBusy(true);
    try {
      const result = await register({ email, password, fullName });
      if (result.emailVerificationRequired) {
        const serverMessage = result.message || 'Check your email to verify your account before signing in.';
        setMessage(email.trim() ? serverMessage.replaceAll(email.trim(), maskEmail(email.trim())) : serverMessage);
        return;
      }
      navigate(`/${(result.activeRole || 'Member').toLowerCase()}/home`);
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
        {message ? (
          <div className="auth-body">
            <h2>Check your email</h2>
            <p className="callout" role="status">{message}</p>
            <p>We sent the verification link to <strong>{maskEmail(email.trim())}</strong> when the account is eligible.</p>
            <p>Check your inbox and spam or junk folder. Delivery can take a few minutes.</p>
            {resendNote ? <p className="callout" role="status">{resendNote}</p> : null}
            <button
              type="button"
              className="btn btn-primary btn-block"
              disabled={busy}
              onClick={async () => {
                setBusy(true);
                setResendNote('');
                try {
                  const result = await authApi.resendVerification(email);
                  setResendNote(result.message || 'If the account is eligible, a verification link will be sent.');
                } catch (err) {
                  setResendNote(err.message || 'Could not resend the verification email.');
                } finally {
                  setBusy(false);
                }
              }}
            >
              {busy ? 'Sending…' : 'Resend verification email'}
            </button>
            <p className="auth-footer"><Link to="/login">Back to login</Link></p>
          </div>
        ) : <form className="auth-body" onSubmit={onSubmit} autoComplete="on">
          <h2>Create your account</h2>
          <p className="auth-lead">New accounts start as Member. Trainer and Creator roles are granted later.</p>
          <div className="form-group">
            <label htmlFor="register-name">Full name</label>
            <input id="register-name" name="name" autoComplete="name" value={fullName} onChange={(e) => { setFullName(e.target.value); setError(''); }} required minLength={2} maxLength={80} />
          </div>
          <div className="form-group">
            <label htmlFor="register-email">Email</label>
            <input id="register-email" name="email" type="email" autoComplete="username" spellCheck={false} value={email} onChange={(e) => { setEmail(e.target.value); setError(''); }} required maxLength={254} />
          </div>
          <div className="form-group">
            <PasswordInput
              id="register-password"
              label="Password"
              name="new-password"
              autoComplete="new-password"
              value={password}
              onChange={(e) => { setPassword(e.target.value); setError(''); }}
              required
              minLength={10}
              maxLength={72}
            />
          </div>
          <ul className="password-checks">
            {PASSWORD_RULES.map((item) => (
              <li key={item.id} className={item.test(password) ? 'ok' : ''}>{item.hint}</li>
            ))}
          </ul>
          <div className="form-group">
            <PasswordInput
              id="register-confirm"
              label="Confirm password"
              autoComplete="new-password"
              value={confirm}
              onChange={(e) => { setConfirm(e.target.value); setError(''); }}
              required
              minLength={10}
              maxLength={72}
            />
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
        </form>}
      </div>
    </div>
  );
}
