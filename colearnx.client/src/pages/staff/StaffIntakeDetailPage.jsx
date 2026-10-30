import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { staffCoursesApi } from '../../api';
import useStaffAuth from '../../auth/useStaffAuth';
import { SessionForm } from '../trainer/IntakeForms';
import { formatDate, initialSchedule, localTimezone, statusLabel } from '../trainer/intakeForm';
import IntakeTeachingDatesEditor from '../../components/IntakeTeachingDatesEditor';
import { canEditTeachingDates } from '../../utils/intakeTeachingDates';

export default function StaffIntakeDetailPage() {
  const { courseIntakeId } = useParams();
  const { token } = useStaffAuth();
  const [intake, setIntake] = useState(null);
  const [editor, setEditor] = useState(false);
  const [datesOpen, setDatesOpen] = useState(false);
  const [message, setMessage] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);
  useEffect(() => {
    const controller = new AbortController();
    staffCoursesApi.getIntake(token, courseIntakeId, controller.signal).then(setIntake)
      .catch((failure) => { if (failure.name !== 'AbortError') setError(failure); });
    return () => controller.abort();
  }, [courseIntakeId, token]);
  async function addSession(body) {
    setBusy(true); setError(null);
    try { setIntake(await staffCoursesApi.createSession(token, intake.id, body)); setEditor(false); }
    catch (failure) { setError(failure); }
    finally { setBusy(false); }
  }
  async function publish() {
    setBusy(true); setError(null);
    try { setIntake(await staffCoursesApi.publishIntake(token, intake.id, intake.version)); }
    catch (failure) { setError(failure); }
    finally { setBusy(false); }
  }
  if (!intake) return <section className="creator-page">{error ? <div className="callout warn">{error.message}</div> : 'Loading class…'}</section>;
  const publishable = ['Draft', 'Rejected'].includes(intake.status);
  const teachingDates = initialSchedule(intake);
  return <section className="creator-page"><Link className="creator-back" to="/staff/intakes">← All classes</Link>
    <div className="page-header"><div><p className="eyebrow">Class #{intake.id}</p><h1>Platform class schedule</h1><p>{formatDate(intake.startsAt)} → {formatDate(intake.endsAt)}</p></div><span className={`creator-status ${intake.status.toLowerCase()}`}>{statusLabel(intake.status)}</span></div>
    {error ? <div className="callout warn">{error.message}</div> : null}
    {message ? <p role="status">{message}</p> : null}
    <div className="creator-section-heading"><div><h2>Teaching dates</h2><p>{teachingDates.deliveryStartDate} → {teachingDates.deliveryEndDate} · {localTimezone}</p></div>
      {canEditTeachingDates(intake.status) ? <button type="button" className="btn btn-ghost" disabled={busy || editor} onClick={() => { setMessage(''); setDatesOpen(true); }}>Edit teaching dates</button> : null}
    </div>
    {datesOpen ? <IntakeTeachingDatesEditor key={intake.version} intake={intake}
      onSave={async (body) => { setIntake(await staffCoursesApi.updateTeachingDates(token, intake.id, body)); }}
      onReload={async () => { setIntake(await staffCoursesApi.getIntake(token, intake.id)); }}
      onClose={(saved) => { setDatesOpen(false); if (saved) setMessage('Teaching dates saved.'); }} /> : null}
    <div className="creator-section-heading"><h2>Sessions</h2>{publishable && !editor ? <button className="btn btn-primary" type="button" onClick={() => setEditor(true)}>Add session</button> : null}</div>
    {editor ? <SessionForm intake={intake} onSave={addSession} onCancel={() => setEditor(false)} busy={busy} error={error} onError={setError} /> : null}
    {(intake.sessions || []).map((session) => <article className="creator-application-row" key={session.id}><div><h2>{session.label}</h2><p>{formatDate(session.startsAt)} → {formatDate(session.endsAt)}</p>{session.meetingLink ? <p>{session.meetingLink}</p> : null}</div>{['Published', 'InProgress'].includes(intake.status) ? <Link className="btn btn-ghost" to={`/staff/intakes/${intake.id}/sessions/${session.id}`}>Open Session</Link> : null}</article>)}
    {publishable ? <div className="creator-section-heading"><div><h2>Publish this class</h2><p>Publishing opens registration to members.</p></div><button className="btn btn-primary" type="button" disabled={busy || !intake.sessions?.length} onClick={publish}>Publish</button></div> : null}
  </section>;
}
