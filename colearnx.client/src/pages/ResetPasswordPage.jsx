import { useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import Logo from '../components/Logo';
import PasswordInput from '../components/PasswordInput';
import { authApi } from '../api';
import { passwordIssues } from './passwordRules';

export default function ResetPasswordPage() {
  const [token, setToken] = useState(() => new URLSearchParams(window.location.hash.slice(1)).get('token') || '');
  const [password, setPassword] = useState('');
  const [confirmation, setConfirmation] = useState('');
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState('');
  const [error, setError] = useState('');
  const [linkStatus, setLinkStatus] = useState('checking');
  const inFlight = useRef(false);
  const initialToken = useRef(token);
  useEffect(() => {
    window.history.replaceState(window.history.state, '', window.location.pathname + window.location.search);
    if (!initialToken.current) {
      setLinkStatus('invalid');
      return undefined;
    }
    let cancelled = false;
    const preflight = typeof authApi.resetStatus === 'function'
      ? authApi.resetStatus(initialToken.current)
      : Promise.resolve({ valid: true });
    preflight
      .then((result) => {
        if (!cancelled) setLinkStatus(result?.valid ? 'valid' : 'invalid');
      })
      .catch(() => {
        if (!cancelled) setLinkStatus('unavailable');
      });
    return () => { cancelled = true; };
  }, []);
  async function submit(event) {
    event.preventDefault();
    if (inFlight.current) return;
    setError('');
    if (password !== confirmation) { setError('Passwords do not match.'); return; }
    const issues = passwordIssues(password);
    if (issues.length > 0) { setError(issues.join(' ')); return; }
    inFlight.current = true; setBusy(true);
    try {
      setMessage((await authApi.resetPassword(initialToken.current, password)).message);
      setToken(''); setPassword(''); setConfirmation('');
    } catch (err) { setError(err.message || 'Could not reset your password. Request a fresh link and try again.'); }
    finally { inFlight.current = false; setBusy(false); }
  }
  return <div className="auth-screen"><div className="auth-card">
    <div className="auth-header"><Logo /></div>
    <div className="auth-body"><h2>Set a new password</h2>
      {message ? <><p className="callout" role="status">{message}</p><Link to="/login">Back to login</Link></> : !token || linkStatus === 'invalid' ? <><p role="alert">This reset link is missing or invalid. Open the complete link from your email.</p><Link to="/forgot-password">Request a new reset link</Link></> : linkStatus === 'checking' ? <p role="status">Checking reset link…</p> : <form onSubmit={submit}>
        <p>This link is already tied to your account. Choose a new password to continue.</p>
        <p>Use 10–72 characters with uppercase, lowercase, a number, and a symbol.</p>
        {linkStatus === 'unavailable' ? <p className="callout warn" role="status">We could not verify this link right now. You can still try to reset your password; the server will validate it.</p> : null}
        <div className="form-group"><PasswordInput id="new-password" label="New password" autoComplete="new-password" required minLength={10} maxLength={72} value={password} onChange={(event) => { setPassword(event.target.value); setError(''); }} /></div>
        <div className="form-group"><PasswordInput id="confirm-password" label="Confirm password" autoComplete="new-password" required value={confirmation} onChange={(event) => { setConfirmation(event.target.value); setError(''); }} /></div>
        {error ? <p className="callout warn" role="alert">{error}</p> : null}
        <button className="btn btn-primary btn-block" disabled={busy}>{busy ? 'Resetting…' : 'Reset password'}</button>
        <p className="auth-footer"><Link to="/forgot-password">Request a new reset link</Link></p>
      </form>}
    </div>
  </div></div>;
}
