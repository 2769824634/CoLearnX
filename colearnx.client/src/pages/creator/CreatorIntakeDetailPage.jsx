import { useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { creatorCoursesApi } from '../../api';
import { creatorIntakesApi } from '../../api/creatorIntakes';
import { ScheduleForm, SessionForm } from '../trainer/IntakeForms';
import { formatDate, statusLabel } from '../trainer/intakeForm';
import useTrainerQuery from '../trainer/useTrainerQuery';
import { formatCount, formatUtcDate } from '../businessPresentation';
import { CreatorError, CreatorHeader } from './CreatorUi';

async function loadDetail(token, id, signal) {
  const [intake, courses] = await Promise.all([
    creatorIntakesApi.get(token, id, signal),
    creatorCoursesApi.list(token, signal),
  ]);
  return { intake, courses };
}

const structuralStatuses = new Set(['Draft', 'Rejected', 'Published', 'InProgress']);

function SessionCard({ session, editable, onEdit, onDelete }) {
  return <article className="creator-application-row">
    <div>
      <p className="creator-eyebrow">Session #{session.id}</p>
      <h2>{session.label}</h2>
      <p>{formatDate(session.startsAt)} → {formatDate(session.endsAt)}</p>
      {session.meetingLink ? <p>{session.meetingLink}</p> : null}
      {session.physicalAddress ? <p>{session.physicalAddress} · {formatCount(session.physicalCapacity, 'seat')}</p> : null}
    </div>
    {editable ? <div className="trainer-actions"><button className="btn btn-ghost" type="button" onClick={onEdit}>Edit</button><button className="btn btn-ghost" type="button" onClick={onDelete}>Delete</button></div> : null}
  </article>;
}

function IntakeWorkspace({ query }) {
  const navigate = useNavigate();
  const { intake, courses } = query.data;
  const course = courses.find((item) => item.id === intake.courseId);
  const [editor, setEditor] = useState(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);
  const [notice, setNotice] = useState('');
  const editable = structuralStatuses.has(intake.status);
  const publishable = intake.status === 'Draft' || intake.status === 'Rejected';
  const cancellable = intake.status === 'Published' || intake.status === 'InProgress';
  const [now] = useState(() => Date.now());
  const canPostpone = intake.status === 'Cancelled' && intake.cancellationReason === 'MinimumEnrollmentNotMet'
    && !intake.replacementIntakeId && new Date(intake.postponementAvailableUntil).getTime() >= now;
  const replacementStart = Math.max(new Date(intake.startsAt).getTime() + 14 * 86400000, now + 21 * 86400000);
  const replacementSchedule = {
    registrationOpensAt: new Date(now).toISOString(),
    registrationClosesAt: new Date(replacementStart - 10 * 86400000).toISOString(),
    startsAt: new Date(replacementStart).toISOString(),
    endsAt: new Date(replacementStart + new Date(intake.endsAt).getTime() - new Date(intake.startsAt).getTime()).toISOString(),
    minEnrollment: intake.minEnrollment ?? 10,
  };
  const sessions = [...(intake.sessions || [])].sort((a, b) => new Date(a.startsAt) - new Date(b.startsAt));

  async function mutate(command, message) {
    if (busy) return;
    setBusy(true);
    setError(null);
    setNotice('');
    try {
      const updated = await command();
      query.setData({ ...query.data, intake: updated });
      setEditor(null);
      setNotice(message);
    } catch (failure) { setError(failure); }
    finally { setBusy(false); }
  }

  async function postpone(body) {
    if (busy) return;
    setBusy(true);
    setError(null);
    try {
      const replacement = await creatorIntakesApi.postpone(query.token, intake.id, { ...body, trainerId: intake.trainerId });
      navigate(`/creator/intakes/${replacement.id}`);
    } catch (failure) { setError(failure); }
    finally { setBusy(false); }
  }

  return <>
    <CreatorHeader eyebrow={`${course?.code || `Course #${intake.courseId}`} / Class #${intake.id}`} title={course?.title || `Course #${intake.courseId}`} action={<span className={`creator-status ${intake.status.toLowerCase()}`}>{statusLabel(intake.status)}</span>}>
      Assigned Trainer #{intake.trainerId}. Times are shown in UTC.
    </CreatorHeader>
    {notice ? <div className="trainer-notice" role="status">{notice}</div> : null}
    <CreatorError error={error} />
    {intake.cancelledAt ? <p>Cancelled {formatDate(intake.cancelledAt)}{intake.cancellationReason === 'MinimumEnrollmentNotMet' ? ' because the class did not reach the minimum.' : '.'}</p> : null}
    {intake.replacementIntakeId ? <p>Postponed to <Link to={`/creator/intakes/${intake.replacementIntakeId}`}>class #{intake.replacementIntakeId}</Link>.</p> : null}
    {canPostpone ? <section>
      <h2>Postpone this class</h2>
      {editor?.type === 'postpone'
        ? <ScheduleForm intake={replacementSchedule} submitLabel="Publish replacement" busy={busy} error={error} onError={setError} onCancel={() => setEditor(null)} onSave={postpone} />
        : <button type="button" className="btn btn-primary" disabled={busy} onClick={() => setEditor({ type: 'postpone' })}>Create replacement class</button>}
    </section> : null}
    <div className="creator-section-heading"><h2>Schedule</h2>{editable && !editor ? <button type="button" className="btn btn-ghost" disabled={busy} onClick={() => setEditor({ type: 'schedule' })}>Edit schedule</button> : null}</div>
    <p>{formatUtcDate(intake.registrationOpensAt)} → {formatUtcDate(intake.registrationClosesAt)} registration · {formatDate(intake.startsAt)} → {formatDate(intake.endsAt)} delivery · minimum {formatCount(intake.minEnrollment ?? 10, 'learner')}</p>
    {editor?.type === 'schedule' ? <ScheduleForm key={intake.version} intake={intake} error={error} onError={setError} busy={busy} onCancel={() => setEditor(null)} onSave={(body) => mutate(() => creatorIntakesApi.update(query.token, intake.id, body), 'Schedule saved.')} /> : null}
    <div className="creator-section-heading"><h2>Sessions</h2>{editable && !editor ? <button type="button" className="btn btn-primary" disabled={busy} onClick={() => setEditor({ type: 'session' })}>Add session</button> : null}</div>
    {editor?.type === 'session' ? <SessionForm key={`${intake.version}:${editor.session?.id || 'new'}`} session={editor.session} intake={intake} error={error} onError={setError} busy={busy} onCancel={() => setEditor(null)} onSave={(body) => mutate(() => editor.session ? creatorIntakesApi.updateSession(query.token, intake.id, editor.session.id, body) : creatorIntakesApi.createSession(query.token, intake.id, body), editor.session ? 'Session updated.' : 'Session added.')} /> : null}
    {sessions.length ? sessions.map((session) => <SessionCard key={session.id} session={session} editable={editable && !editor && !busy} onEdit={() => setEditor({ type: 'session', session })} onDelete={() => setEditor({ type: 'delete', session })} />) : <div className="creator-empty"><strong>No sessions yet</strong><p>Add a session before publishing.</p></div>}
    {editor?.type === 'delete' ? <div><h2>Delete “{editor.session.label}”?</h2><button type="button" className="btn btn-primary" disabled={busy} onClick={() => mutate(() => creatorIntakesApi.deleteSession(query.token, intake.id, editor.session.id, intake.version), 'Session deleted.')}>Confirm deletion</button><button type="button" className="btn btn-ghost" disabled={busy} onClick={() => setEditor(null)}>Cancel</button></div> : null}
    {publishable ? <div className="creator-section-heading"><div><h2>Publish this class</h2><p>Publishing opens registration. It does not wait for a second approval.</p></div><button type="button" className="btn btn-primary" disabled={busy || !sessions.length} onClick={() => mutate(() => creatorIntakesApi.publish(query.token, intake.id, intake.version), 'Class published.')}>Publish</button></div> : null}
    {cancellable ? <div className="creator-section-heading"><div><h2>Cancel this class</h2><p>Reserved credits are released. Confirmed learners receive a full refund.</p></div>{editor?.type === 'cancel' ? <button type="button" className="btn btn-primary" disabled={busy} onClick={() => mutate(async () => { await creatorIntakesApi.cancel(query.token, intake.id); return creatorIntakesApi.get(query.token, intake.id); }, 'Class cancelled.')}>Confirm cancellation</button> : <button type="button" className="btn btn-ghost" disabled={busy} onClick={() => setEditor({ type: 'cancel' })}>Cancel class</button>}</div> : null}
  </>;
}

export default function CreatorIntakeDetailPage() {
  const { courseIntakeId } = useParams();
  const query = useTrainerQuery(loadDetail, courseIntakeId);
  return <section className="creator-page">
    <Link className="creator-back" to="/creator/intakes">← All classes</Link>
    {query.loading ? <div className="creator-empty" role="status">Loading class…</div>
      : query.error ? <CreatorError error={query.error} onRetry={query.refresh} />
        : <IntakeWorkspace key={`${query.token}:${courseIntakeId}`} query={query} />}
  </section>;
}
