import { useState } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import Logo from '../components/Logo';
import PasswordInput from '../components/PasswordInput';
import { authApi } from '../api';
import { useAuth } from '../auth/AuthContext';
import { useNoCredentialAutofill } from '../auth/noCredentialAutofill';
import { maskEmail } from '../data/memberMock';
import { PASSWORD_RULES, passwordIssues } from './passwordRules';

const SIGNUP_ROLES = [
  { role: 'Member', hint: 'Join classes and earn certificates' },
  { role: 'Trainer', hint: 'Teach scheduled classes' },
  { role: 'Creator', hint: 'Design and publish courses' },
];
// Links such as /register?role=Trainer open the form on that account type.
const roleFromQuery = (value) => SIGNUP_ROLES.find((item) => item.role.toLowerCase() === String(value || '').toLowerCase())?.role || 'Member';

export default function RegisterPage() {
  const { register, isAuthenticated, activeRole, booting, user, logout } = useAuth();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const [fullName, setFullName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [confirm, setConfirm] = useState('');
  const [role, setRole] = useState(() => roleFromQuery(searchParams.get('role')));
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');
  const [signInRequired, setSignInRequired] = useState(false);
  const [resendNote, setResendNote] = useState('');
  const nameAutofill = useNoCredentialAutofill();
  const emailAutofill = useNoCredentialAutofill();
  const passwordAutofill = useNoCredentialAutofill();
  const confirmAutofill = useNoCredentialAutofill();

  // A browser that is still signed in cannot create a second account. Say so here: sending the person to
  // their workspace made an uncertified Trainer or Creator land on the certification page without warning.
  if (!booting && isAuthenticated && activeRole && !busy) {
    const roleName = activeRole.charAt(0).toUpperCase() + activeRole.slice(1);
    return (
      <div className="auth-screen">
        <div className="auth-card">
          <div className="auth-header"><Logo /></div>
          <div className="auth-body">
            <h2>You are already signed in</h2>
            <p className="auth-lead">
              This browser is signed in as {user?.fullName || user?.email || 'an existing account'}, a {roleName} account.
              One account has one role, so sign out first to create another account.
            </p>
            <Link className="btn btn-primary btn-block" to={`/${activeRole}/home`}>Go to my workspace</Link>
            <button type="button" className="btn btn-ghost btn-block" style={{ marginTop: 10 }} onClick={logout}>
              Sign out and create another account
            </button>
          </div>
        </div>
      </div>
    );
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
      const result = await register({ email, password, fullName, role });
      if (result.emailVerificationRequired) {
        const serverMessage = result.message || 'Check your email to verify your account before signing in.';
        setMessage(email.trim() ? serverMessage.replaceAll(email.trim(), maskEmail(email.trim())) : serverMessage);
        return;
      }
      if (result.signInRequired) {
        setPassword('');
        setConfirm('');
        setSignInRequired(true);
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
        {signInRequired ? (
          <div className="auth-body">
            <h2>Account created</h2>
            <p className="callout" role="status">Your account was created, but automatic sign-in was not completed. Please sign in to continue.</p>
            <Link className="btn btn-primary" to="/login">Sign in</Link>
          </div>
        ) : message ? (
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
        ) : <form className="auth-body" onSubmit={onSubmit} autoComplete="off">
          <h2>Create your account</h2>
          <div className="form-group">
            <span className="auth-role-label" id="register-role-label">Account type</span>
            <div className="auth-role-picker" role="group" aria-labelledby="register-role-label">
              {SIGNUP_ROLES.map((item) => (
                <button
                  key={item.role}
                  type="button"
                  className={`auth-role-option${role === item.role ? ' is-selected' : ''}`}
                  aria-pressed={role === item.role}
                  aria-label={`Sign up as ${item.role}`}
                  onClick={() => { setRole(item.role); setError(''); }}
                >
                  <strong>{item.role}</strong>
                  <small>{item.hint}</small>
                </button>
              ))}
            </div>
          </div>
          {role !== 'Member' ? (
            <p className="auth-lead auth-role-note" role="note">You can sign in and edit your profile straight away after any required email verification. {role} features open after an administrator approves your certification, where you also choose your specialties.</p>
          ) : null}
          <div className="form-group">
            <label htmlFor="register-name">Full name</label>
            <input id="register-name" value={fullName} onChange={(e) => { setFullName(e.target.value); setError(''); }} required minLength={2} maxLength={80} {...nameAutofill} />
          </div>
          <div className="form-group">
            <label htmlFor="register-email">Email</label>
            <input id="register-email" type="email" spellCheck={false} value={email} onChange={(e) => { setEmail(e.target.value); setError(''); }} required maxLength={254} {...emailAutofill} />
          </div>
          <div className="form-group">
            <PasswordInput id="register-password" label="Password" value={password} onChange={(e) => { setPassword(e.target.value); setError(''); }} required minLength={10} maxLength={72} {...passwordAutofill} />
          </div>
          <ul className="password-checks">
            {PASSWORD_RULES.map((item) => (
              <li key={item.id} className={item.test(password) ? 'ok' : ''}>{item.hint}</li>
            ))}
          </ul>
          <div className="form-group">
            <PasswordInput id="register-confirm" label="Confirm password" value={confirm} onChange={(e) => { setConfirm(e.target.value); setError(''); }} required minLength={10} maxLength={72} {...confirmAutofill} />
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
