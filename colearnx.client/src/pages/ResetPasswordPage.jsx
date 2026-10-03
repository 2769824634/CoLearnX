import { useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import Logo from '../components/Logo';
import { authApi } from '../api';
import { passwordIssues } from './passwordRules';

export default function ResetPasswordPage() {
  const [token, setToken] = useState(() => new URLSearchParams(window.location.hash.slice(1)).get('token') || '');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [confirmation, setConfirmation] = useState('');
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState('');
  const [error, setError] = useState('');
  const [linkStatus, setLinkStatus] = useState('checking');
  const [showPassword, setShowPassword] = useState(false);
  const [showConfirmation, setShowConfirmation] = useState(false);
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
    const issues = passwordIssues(password, email);
    if (issues.length > 0) { setError(issues.join(' ')); return; }
    inFlight.current = true; setBusy(true);
    try {
      setMessage((await authApi.resetPassword(initialToken.current, password, email.trim())).message);
      setToken(''); setEmail(''); setPassword(''); setConfirmation('');
    } catch (err) { setError(err.message || 'Could not reset your password. Request a fresh link and try again.'); }
    finally { inFlight.current = false; setBusy(false); }
  }
  return <div className="auth-screen"><div className="auth-card">
    <div className="auth-header"><Logo /></div>
    <div className="auth-body"><h2>Set a new password</h2>
      {message ? <><p className="callout" role="status">{message}</p><Link to="/login">Back to login</Link></> : !token || linkStatus === 'invalid' ? <><p role="alert">This reset link is missing or invalid. Open the complete link from your email.</p><Link to="/forgot-password">Request a new reset link</Link></> : linkStatus === 'checking' ? <p role="status">Checking reset link…</p> : <form onSubmit={submit}>
        <p>Enter the email registered on this account. The reset only succeeds if it matches that mailbox.</p>
        <div className="form-group"><label htmlFor="account-email">Account email</label><input id="account-email" type="email" autoComplete="email" required maxLength={254} value={email} onChange={(event) => { setEmail(event.target.value); setError(''); }} /></div>
        <p>Use 10–72 characters with uppercase, lowercase, a number, and a symbol. Do not include your email address.</p>
        {linkStatus === 'unavailable' ? <p className="callout warn" role="status">We could not verify this link right now. You can still try to reset your password; the server will validate it.</p> : null}
        <div className="form-group"><label htmlFor="new-password">New password</label><div style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}><input id="new-password" style={{ flex: '1 1 220px', minWidth: 0 }} type={showPassword ? 'text' : 'password'} autoComplete="new-password" required minLength={10} maxLength={72} value={password} onChange={(event) => { setPassword(event.target.value); setError(''); }} /><button type="button" className="btn btn-ghost btn-sm" aria-pressed={showPassword} onClick={() => setShowPassword((current) => !current)}>{showPassword ? 'Hide new password' : 'Show new password'}</button></div></div>
        <div className="form-group"><label htmlFor="confirm-password">Confirm password</label><div style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}><input id="confirm-password" style={{ flex: '1 1 220px', minWidth: 0 }} type={showConfirmation ? 'text' : 'password'} autoComplete="new-password" required value={confirmation} onChange={(event) => { setConfirmation(event.target.value); setError(''); }} /><button type="button" className="btn btn-ghost btn-sm" aria-pressed={showConfirmation} onClick={() => setShowConfirmation((current) => !current)}>{showConfirmation ? 'Hide confirmation' : 'Show confirmation'}</button></div></div>
        {error ? <p className="callout warn" role="alert">{error}</p> : null}
        <button className="btn btn-primary btn-block" disabled={busy}>{busy ? 'Resetting…' : 'Reset password'}</button>
        <p className="auth-footer"><Link to="/forgot-password">Request a new reset link</Link></p>
      </form>}
    </div>
  </div></div>;
}
