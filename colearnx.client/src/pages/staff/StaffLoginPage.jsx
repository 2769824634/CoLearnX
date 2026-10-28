import { useState } from 'react';
import { Link, Navigate, useLocation } from 'react-router-dom';
import Logo from '../../components/Logo';
import PasswordInput from '../../components/PasswordInput';
import useStaffAuth from '../../auth/useStaffAuth';

export default function StaffLoginPage() {
  const { login, isAuthenticated, booting } = useStaffAuth();
  const location = useLocation();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const requested = location.state?.from?.pathname;
  const destination = requested?.startsWith('/staff/') ? requested : '/staff/home';
  if (!booting && isAuthenticated) return <Navigate to={destination} replace />;

  async function submit(event) {
    event.preventDefault();
    setBusy(true);
    setError('');
    try { await login(email, password); }
    catch (err) { setError(err.message || 'Staff sign-in failed.'); }
    finally { setBusy(false); }
  }

  return <div className="auth-screen admin-auth-screen">
    <main className="login-card">
      <section className="login-intro"><Logo /><p className="login-kicker">CoLearnX platform staff</p><h1>Teach as the platform.</h1><p>Manage platform-owned courses with your numbered instructor identity.</p></section>
      <form className="login-form" onSubmit={submit} autoComplete="off">
        <p className="login-eyebrow">Independent staff account</p><h2>Staff sign in</h2>
        <div className="form-group"><label htmlFor="staff-login-email">Staff email</label><input id="staff-login-email" type="email" required value={email} onChange={(event) => setEmail(event.target.value)} /></div>
        <div className="form-group"><label htmlFor="staff-login-password">Password</label><PasswordInput id="staff-login-password" label={null} required value={password} onChange={(event) => setPassword(event.target.value)} /></div>
        {error ? <div className="callout warn" role="alert">{error}</div> : null}
        <button type="submit" className="btn btn-primary btn-block" disabled={busy}>{busy ? 'Signing in…' : 'Enter staff workspace'}</button>
        <Link className="login-home-link" to="/">Back to home</Link>
      </form>
    </main>
  </div>;
}
