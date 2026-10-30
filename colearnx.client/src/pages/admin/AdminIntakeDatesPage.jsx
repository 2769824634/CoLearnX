import { useEffect, useRef, useState } from 'react';
import { adminCourseReviewsApi } from '../../api';
import useAdminAuth from '../../auth/useAdminAuth';
import IntakeTeachingDatesEditor from '../../components/IntakeTeachingDatesEditor';
import { canEditTeachingDates } from '../../utils/intakeTeachingDates';
import { initialSchedule, localTimezone, statusLabel } from '../trainer/intakeForm';
import AdminDataState from './AdminDataState';
import useAdminQuery from './useAdminQuery';

const loadIntakes = (token, _queryKey, signal) => adminCourseReviewsApi.listIntakes(token, signal);

export default function AdminIntakeDatesPage() {
  const { token } = useAdminAuth();
  const query = useAdminQuery(loadIntakes);
  const [intake, setIntake] = useState(null);
  const [opening, setOpening] = useState(false);
  const [error, setError] = useState(null);
  const [message, setMessage] = useState('');
  const requestRef = useRef(null);
  useEffect(() => () => requestRef.current?.abort(), [token]);

  async function openDates(id) {
    const controller = new AbortController();
    requestRef.current?.abort();
    requestRef.current = controller;
    setOpening(true); setError(null); setMessage('');
    try { setIntake(await adminCourseReviewsApi.getIntake(token, id, controller.signal)); }
    catch (failure) { if (failure.name !== 'AbortError') setError(failure); }
    finally { if (!controller.signal.aborted) setOpening(false); }
  }

  return <section className="admin-review-page later-page">
    <header className="admin-review-header"><div><p className="admin-form-eyebrow">Class schedules</p><h1>Class dates</h1>
      <p>Update teaching dates for existing classes. Dates are shown in {localTimezone}; the last date is inclusive.</p></div>
      <button className="btn btn-ghost" type="button" onClick={query.refresh} disabled={query.loading}>Refresh classes</button>
    </header>
    <AdminDataState loading={query.loading} error={query.error} onRetry={query.refresh} />
    {error ? <div className="callout warn" role="alert">{error.message}</div> : null}
    {message ? <p role="status">{message}</p> : null}
    {query.data ? <div className="later-table-wrap"><table className="later-table"><thead><tr>
      <th>Class / Course</th><th>Trainer</th><th>Teaching dates</th><th>Status</th><th>Action</th>
    </tr></thead><tbody>{query.data.map((item) => {
      const dates = initialSchedule(item);
      return <tr key={item.id}><td><strong>Class #{item.id} · {item.courseTitle}</strong><small>{item.courseCode}</small></td>
        <td>{item.trainerName}</td><td>{dates.deliveryStartDate} → {dates.deliveryEndDate}</td><td>{statusLabel(item.status)}</td>
        <td>{canEditTeachingDates(item.status) ? <button className="btn btn-ghost btn-sm" type="button" disabled={opening} onClick={() => openDates(item.id)}>Edit teaching dates</button> : '—'}</td></tr>;
    })}</tbody></table>{!query.data.length ? <div className="admin-review-state">No classes are available yet.</div> : null}</div> : null}
    {opening ? <p role="status">Loading class…</p> : null}
    {intake ? <IntakeTeachingDatesEditor key={`${intake.id}-${intake.version}`} intake={intake}
      onSave={async (body) => { await adminCourseReviewsApi.updateTeachingDates(token, intake.id, body); query.refresh(); }}
      onReload={async () => { setIntake(await adminCourseReviewsApi.getIntake(token, intake.id)); query.refresh(); }}
      onClose={(saved) => { setIntake(null); if (saved) setMessage('Teaching dates saved.'); }} /> : null}
  </section>;
}
