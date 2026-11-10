import { useEffect, useState } from 'react';
import { adminStaffApi, staffAuthApi } from '../api';
import { formatUtcDateTime } from '../pages/businessPresentation';
import { statusLabel } from '../pages/trainer/intakeForm';

export default function StaffDetails({ token, staffId }) {
  const [detail, setDetail] = useState(null);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const [confirmReset, setConfirmReset] = useState(false);
  const [password, setPassword] = useState('');
  useEffect(() => {
    const controller = new AbortController();
    const request = staffId ? adminStaffApi.detail(token, staffId, controller.signal) : staffAuthApi.profile(token, controller.signal);
    request.then((value) => { if (!controller.signal.aborted) setDetail(value); })
      .catch((err) => { if (!controller.signal.aborted) setError(err.message); });
    return () => controller.abort();
  }, [token, staffId]);
  async function download() {
    setBusy(true); setError('');
    try {
      if (staffId) await adminStaffApi.document(token, staffId, detail.documentName || 'staff-document');
      else await staffAuthApi.document(token, detail.documentName || 'staff-document');
    } catch (err) { setError(err.message || 'Could not download document.'); }
    finally { setBusy(false); }
  }
  async function resetPassword() {
    if (busy) return;
    setBusy(true); setError(''); setPassword('');
    try {
      const result = await adminStaffApi.resetPassword(token, staffId);
      setPassword(result.initialPassword); setConfirmReset(false);
    } catch (err) { setError(err.message || 'Could not reset password.'); }
    finally { setBusy(false); }
  }
  const account = detail?.account;
  return <div className="account-details">
    {error ? <p className="callout warn" role="alert">{error}</p> : null}
    {!detail && !error ? <p role="status">Loading employee details…</p> : null}
    {detail ? <>
      <dl className="account-detail-grid">
        {Object.entries({ 'Employee': account.fullName, 'Public name': account.displayName, 'Staff number': String(account.staffNumber).padStart(3, '0'), 'Email': account.email, 'Phone': account.phone, 'Status': account.isActive ? 'Active' : 'Inactive', 'Created (UTC)': formatUtcDateTime(account.createdAt) }).map(([label, value]) => <div key={label}><dt>{label}</dt><dd>{value || '—'}</dd></div>)}
      </dl>
      <section className="later-panel"><h2>Uploaded document</h2>{detail.hasDocument ? <button type="button" className="btn btn-ghost" disabled={busy} onClick={download}>Download staff document</button> : <p>No staff document has been uploaded.</p>}</section>
      <section className="later-panel"><h2>Password</h2><p>Existing passwords cannot be displayed. {staffId ? 'Resetting creates a new password and signs this employee out.' : 'Contact an administrator if you need a password reset.'}</p>
        {staffId ? confirmReset ? <div><p>Reset this employee’s password now?</p><button type="button" className="btn btn-primary" disabled={busy} onClick={resetPassword}>{busy ? 'Resetting…' : 'Confirm password reset'}</button> <button type="button" className="btn btn-ghost" disabled={busy} onClick={() => setConfirmReset(false)}>Cancel</button></div> : <button type="button" className="btn btn-ghost" disabled={busy} onClick={() => setConfirmReset(true)}>Reset password</button> : null}
        {password ? <div className="callout success" role="status"><strong>New password — copy it before closing:</strong> <code>{password}</code><button className="btn btn-ghost" type="button" onClick={() => setPassword('')}>Hide password</button></div> : null}
      </section>
      <section className="later-panel"><h2>Assigned courses</h2>{detail.courses.length ? <ul>{detail.courses.map(course => <li key={course.id}><strong>{course.code} · {course.title}</strong> — {course.status}</li>)}</ul> : <p>No assigned courses.</p>}</section>
      <section className="later-panel"><h2>Teaching classes</h2>{detail.intakes.length ? <div className="later-table-wrap"><table className="later-table"><thead><tr><th>Class</th><th>Status</th><th>Starts (UTC)</th><th>Ends (UTC)</th></tr></thead><tbody>{detail.intakes.map(intake => <tr key={intake.id}><td>{intake.courseTitle}</td><td>{statusLabel(intake.status)}</td><td>{formatUtcDateTime(intake.startsAt)}</td><td>{formatUtcDateTime(intake.endsAt)}</td></tr>)}</tbody></table></div> : <p>No teaching classes.</p>}</section>
    </> : null}
  </div>;
}
