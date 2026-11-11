import { useEffect, useState } from 'react';
import { Link, useNavigate, useParams, useSearchParams } from 'react-router-dom';
import { creatorCoursesApi } from '../../api';
import { creatorIntakesApi } from '../../api/creatorIntakes';
import { ScheduleForm, SessionForm } from '../trainer/IntakeForms';
import { localTimezone, statusLabel } from '../trainer/intakeForm';
import useTrainerQuery from '../trainer/useTrainerQuery';
import { formatCount } from '../businessPresentation';
import { CreatorError, CreatorHeader } from './CreatorUi';
import EditClassScheduleForm from './EditClassScheduleForm';
import { localDateTime, timeLabel } from './openClass';
import { utcDate } from '../../utils/utcDates';

const loadIntake = (token, id, signal) => creatorIntakesApi.get(token, id, signal);

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
      <p className="creator-eyebrow">{localDateTime(session.startsAt)}–{timeLabel(utcDate(session.endsAt))}</p>
      <h2>{session.label}</h2>
      {session.meetingLink ? <p>{session.meetingLink}</p> : null}
      {session.physicalAddress ? <p>{session.physicalAddress} · {formatCount(session.physicalCapacity, 'seat')}</p> : null}
    </div>
    {editable ? <div className="trainer-actions"><button className="btn btn-ghost" type="button" onClick={onEdit}>Edit</button><button className="btn btn-ghost" type="button" onClick={onDelete}>Delete</button></div> : null}
  </article>;
}

export function IntakeWorkspace({ query, onOpenClass }) {
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
      if (onOpenClass) onOpenClass(replacement);
      else navigate(`/creator/courses/${intake.courseId}/intakes/${replacement.id}`);
    } catch (failure) { setError(failure); }
    finally { setBusy(false); }
  }

  const replacementTarget = `/creator/courses/${intake.courseId}/intakes/${intake.replacementIntakeId}`;
  return <>
    <CreatorHeader eyebrow={`${course?.code || course?.title || 'Course'} · ${localDateTime(intake.startsAt)}`} title={course?.title || course?.code || 'Course'} action={<span className={`creator-status ${intake.status.toLowerCase()}`}>{statusLabel(intake.status)}</span>}>
      Trainer {intake.trainerName || 'assigned'}. Times are shown in your local time zone ({localTimezone}).
    </CreatorHeader>
    {notice ? <div className="trainer-notice" role="status">{notice}</div> : null}
    <CreatorError error={error} />
    {intake.cancelledAt ? <p>Cancelled {localDateTime(intake.cancelledAt)}{intake.cancellationReason === 'MinimumEnrollmentNotMet' ? ' because the class did not reach the minimum.' : '.'}</p> : null}
    {intake.replacementIntakeId ? <p>Postponed to {onOpenClass
      ? <button type="button" className="btn btn-ghost" onClick={() => onOpenClass({ id: intake.replacementIntakeId })}>the replacement class</button>
      : <Link to={replacementTarget}>the replacement class</Link>}.</p> : null}
    {canPostpone ? <section>
      <h2>Postpone this class</h2>
      {editor?.type === 'postpone'
        ? <ScheduleForm intake={replacementSchedule} submitLabel="Publish replacement" busy={busy} error={error} onError={setError} onCancel={() => setEditor(null)} onSave={postpone} />
        : <button type="button" className="btn btn-primary" disabled={busy} onClick={() => setEditor({ type: 'postpone' })}>Create replacement class</button>}
    </section> : null}
    <div className="creator-section-heading"><h2>Schedule</h2>{editable && !editor ? <button type="button" className="btn btn-ghost" disabled={busy} onClick={() => setEditor({ type: 'schedule' })}>Edit schedule</button> : null}</div>
    <p className="creator-schedule-line">Registration {localDateTime(intake.registrationOpensAt)} → {localDateTime(intake.registrationClosesAt)} · teaching {localDateTime(intake.startsAt)} → {localDateTime(intake.endsAt)} · minimum {formatCount(intake.minEnrollment ?? 10, 'learner')}</p>
    {editor?.type === 'schedule' ? <EditClassScheduleForm key={intake.version} intake={intake} error={error} onError={setError} busy={busy} onCancel={() => setEditor(null)} onSave={(body) => mutate(() => creatorIntakesApi.update(query.token, intake.id, body), 'Schedule saved.')} /> : null}
    <div className="creator-section-heading"><h2>Sessions</h2>{editable && !editor ? <button type="button" className="btn btn-primary" disabled={busy} onClick={() => setEditor({ type: 'session' })}>Add session</button> : null}</div>
    {editor?.type === 'session' ? <SessionForm key={`${intake.version}:${editor.session?.id || 'new'}`} session={editor.session} intake={intake} error={error} onError={setError} busy={busy} onCancel={() => setEditor(null)} onSave={(body) => mutate(() => editor.session ? creatorIntakesApi.updateSession(query.token, intake.id, editor.session.id, body) : creatorIntakesApi.createSession(query.token, intake.id, body), editor.session ? 'Session updated.' : 'Session added.')} /> : null}
    {sessions.length ? <div className="creator-application-list creator-session-rows">{sessions.map((session) => <SessionCard key={session.id} session={session} editable={editable && !editor && !busy} onEdit={() => setEditor({ type: 'session', session })} onDelete={() => setEditor({ type: 'delete', session })} />)}</div> : <div className="creator-empty"><strong>No sessions yet</strong><p>Add a session before publishing.</p></div>}
    {editor?.type === 'delete' ? <div className="creator-confirm"><h2>Delete “{editor.session.label}”?</h2><button type="button" className="btn btn-primary" disabled={busy} onClick={() => mutate(() => creatorIntakesApi.deleteSession(query.token, intake.id, editor.session.id, intake.version), 'Session deleted.')}>Confirm deletion</button><button type="button" className="btn btn-ghost" disabled={busy} onClick={() => setEditor(null)}>Cancel</button></div> : null}
    {publishable ? <div className="creator-section-heading"><div><h2>Publish this class</h2><p>Publishing opens registration. It does not wait for a second approval.</p></div><button type="button" className="btn btn-primary" disabled={busy || !sessions.length} onClick={() => mutate(() => creatorIntakesApi.publish(query.token, intake.id, intake.version), 'Class published.')}>Publish</button></div> : null}
    {cancellable ? <div className="creator-section-heading"><div><h2>Cancel this class</h2><p>Reserved credits are released. Confirmed learners receive a full refund.</p></div>{editor?.type === 'cancel' ? <button type="button" className="btn btn-primary" disabled={busy} onClick={() => mutate(async () => { await creatorIntakesApi.cancel(query.token, intake.id); return creatorIntakesApi.get(query.token, intake.id); }, 'Class cancelled.')}>Confirm cancellation</button> : <button type="button" className="btn btn-ghost" disabled={busy} onClick={() => setEditor({ type: 'cancel' })}>Cancel class</button>}</div> : null}
  </>;
}

export function CreatorIntakeRedirect() {
  const { courseIntakeId } = useParams();
  const navigate = useNavigate();
  const query = useTrainerQuery(loadIntake, courseIntakeId);
  useEffect(() => {
    if (query.data?.courseId) navigate(`/creator/courses/${query.data.courseId}/intakes/${query.data.id}`, { replace: true });
  }, [navigate, query.data]);
  return <section className="creator-page">
    {query.error ? <CreatorError error={query.error} onRetry={query.refresh} /> : <div className="creator-empty" role="status">Opening the course…</div>}
  </section>;
}

export default function CreatorIntakeDetailPage({ intakeId, course }) {
  const params = useParams();
  const courseIntakeId = intakeId || params.courseIntakeId;
  const query = useTrainerQuery(loadDetail, courseIntakeId);
  const [, setSearchParams] = useSearchParams();
  const courseId = query.data?.intake?.courseId;
  const listed = query.data?.courses || [];
  const data = query.data && course && !listed.some((item) => item.id === course.id)
    ? { ...query.data, courses: [...listed, course] }
    : query.data;
  const workspaceQuery = data ? { ...query, data } : query;
  return <section className={course ? undefined : 'creator-page'}>
    {course ? null : <Link className="creator-back" to={courseId ? `/creator/courses/${courseId}` : '/creator/courses'}>← Course</Link>}
    {query.loading ? <div className="creator-empty" role="status">Loading class…</div>
      : query.error ? <CreatorError error={query.error} onRetry={query.refresh} />
        : <IntakeWorkspace key={`${query.token}:${courseIntakeId}`} query={workspaceQuery} onOpenClass={course ? (replacement) => setSearchParams({ class: String(replacement.id) }, { replace: true }) : undefined} />}
  </section>;
}
