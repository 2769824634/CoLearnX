import { useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import Logo from '../components/Logo';
import { authApi } from '../api';

export default function ForgotPasswordPage() {
  const [email, setEmail] = useState('');
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState('');
  const [error, setError] = useState('');
  const inFlight = useRef(false);
  async function submit(event) {
    event.preventDefault();
    if (inFlight.current) return;
    inFlight.current = true;
    setBusy(true); setError(''); setMessage('');
    try { setMessage((await authApi.forgotPassword(email.trim())).message); }
    catch (err) { setError(err.message || 'Unable to request a link. Try again.'); }
    finally { inFlight.current = false; setBusy(false); }
  }
  return <div className="auth-screen"><div className="auth-card">
    <div className="auth-header"><Logo /></div>
    <form className="auth-body" onSubmit={submit}>
      <h2>Forgot your password?</h2>
      <p>Enter the email for your Member, Trainer or Creator account. Check your inbox and spam folder for a one-time reset link.</p>
      <div className="form-group"><label htmlFor="reset-email">Email</label><input id="reset-email" type="email" autoComplete="email" required maxLength={254} value={email} onChange={(event) => setEmail(event.target.value)} /></div>
      {message ? <p className="callout" role="status">{message}</p> : null}
      {error ? <p className="callout warn" role="alert">{error}</p> : null}
      <button className="btn btn-primary btn-block" disabled={busy}>{busy ? 'Sending…' : 'Send reset link'}</button>
      <p className="auth-footer"><Link to="/login">Back to login</Link></p>
    </form>
  </div></div>;
}
