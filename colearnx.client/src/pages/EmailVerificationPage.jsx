import { useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import Logo from '../components/Logo';
import { authApi } from '../api';

export default function EmailVerificationPage() {
  const token = useRef(new URLSearchParams(window.location.hash.slice(1)).get('token') || '');
  const request = useRef(null);
  const [message, setMessage] = useState('');
  const [error, setError] = useState('');

  useEffect(() => {
    window.history.replaceState(window.history.state, '', window.location.pathname + window.location.search);
    if (!token.current) {
      setError('This verification link is missing or invalid.');
      return;
    }
    request.current ??= authApi.verifyEmail(token.current);
    let cancelled = false;
    request.current
      .then((result) => { if (!cancelled) setMessage(result.message); })
      .catch((err) => { if (!cancelled) setError(err.message || 'Could not verify your email.'); });
    return () => { cancelled = true; };
  }, []);

  return <div className="auth-screen"><div className="auth-card">
    <div className="auth-header"><Logo /></div>
    <div className="auth-body">
      <h2>Verify your email</h2>
      {!message && !error ? <p role="status">Verifying…</p> : null}
      {message ? <p className="callout" role="status">{message}</p> : null}
      {error ? <p className="callout warn" role="alert">{error}</p> : null}
      <p className="auth-footer"><Link to="/login">Sign in</Link></p>
    </div>
  </div></div>;
}
