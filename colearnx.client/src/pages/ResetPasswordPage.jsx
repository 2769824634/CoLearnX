import { useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import Logo from '../components/Logo';
import { authApi } from '../api';

export default function ResetPasswordPage() {
  const [token, setToken] = useState(() => new URLSearchParams(window.location.hash.slice(1)).get('token') || '');
  const [password, setPassword] = useState('');
  const [confirmation, setConfirmation] = useState('');
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState('');
  const [error, setError] = useState('');
  const inFlight = useRef(false);
  useEffect(() => { window.history.replaceState(window.history.state, '', window.location.pathname + window.location.search); }, []);
  async function submit(event) {
    event.preventDefault();
    if (inFlight.current) return;
    setError('');
    if (password !== confirmation) { setError('Passwords do not match.'); return; }
    inFlight.current = true; setBusy(true);
    try {
      setMessage((await authApi.resetPassword(token, password)).message);
      setToken(''); setPassword(''); setConfirmation('');
    } catch (err) { setError(err.message || 'Could not reset your password. Request a fresh link and try again.'); }
    finally { inFlight.current = false; setBusy(false); }
  }
  return <div className="auth-screen"><div className="auth-card">
    <div className="auth-header"><Logo /></div>
    <div className="auth-body"><h2>Set a new password</h2>
      {message ? <><p className="callout" role="status">{message}</p><Link to="/login">Back to login</Link></> : !token ? <><p role="alert">This reset link is missing or invalid. Open the complete link from your email.</p><Link to="/forgot-password">Request a new reset link</Link></> : <form onSubmit={submit}>
        <p>Use 10–72 characters with uppercase, lowercase, a number, and a symbol. Do not include your email address.</p>
        <div className="form-group"><label htmlFor="new-password">New password</label><input id="new-password" type="password" autoComplete="new-password" required minLength={10} maxLength={72} value={password} onChange={(event) => setPassword(event.target.value)} /></div>
        <div className="form-group"><label htmlFor="confirm-password">Confirm password</label><input id="confirm-password" type="password" autoComplete="new-password" required value={confirmation} onChange={(event) => setConfirmation(event.target.value)} /></div>
        {error ? <p className="callout warn" role="alert">{error}</p> : null}
        <button className="btn btn-primary btn-block" disabled={busy}>{busy ? 'Resetting…' : 'Reset password'}</button>
        <p className="auth-footer"><Link to="/forgot-password">Request a new reset link</Link></p>
      </form>}
    </div>
  </div></div>;
}
