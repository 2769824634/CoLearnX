import { useEffect, useState } from 'react';
import { Link, useNavigate, useParams, useSearchParams } from 'react-router-dom';
import { creatorCoursesApi } from '../../api';
import { creatorIntakesApi } from '../../api/creatorIntakes';
import SessionCalendar from '../../components/SessionCalendar';
import { ScheduleForm, SessionForm } from '../trainer/IntakeForms';
import { localTimezone, statusLabel } from '../trainer/intakeForm';
import useTrainerQuery from '../trainer/useTrainerQuery';
import { formatCount } from '../businessPresentation';
import { CreatorError, CreatorHeader } from './CreatorUi';
import EditClassScheduleForm from './EditClassScheduleForm';
import InviteTrainerDialog from './InviteTrainerDialog';
import { localDateTime, timeLabel } from './openClass';
import { invitationOf, isUnpublished } from './intakeInvitation';
import { utcDate } from '../../utils/utcDates';

const loadIntake = (token, id, signal) => creatorIntakesApi.get(token, id, signal);

async function loadDetail(token, id, signal) {
  const [intake, courses] = await Promise.all([
    creatorIntakesApi.get(token, id, signal),
    creatorCoursesApi.list(token, signal),
  ]);
  return { intake, courses };
}

function invitationText(intake, name) {
  const state = intake.trainerInvitationStatus || 'NotSent';
  if (!isUnpublished(intake)) return `${name} accepted this schedule. You can still move a session that has not started; ${name} and booked learners are told.`;
  if (intake.hasTrainer === false) return 'Invite a certified trainer in this course’s specialty. They receive every session below and accept or decline the whole schedule.';
  if (state === 'Invited') return `Sent ${localDateTime(intake.trainerInvitedAt)}. ${name} is checking the dates. The schedule is locked until they reply.`;
  if (state === 'Accepted') return `${name} accepted the schedule ${localDateTime(intake.trainerRespondedAt)}, but the intake could not be published automatically. Publish it below, or withdraw the invitation to change the schedule.`;
  if (state === 'Declined') {
    const reason = intake.trainerDeclineReason ? ` Reason: “${intake.trainerDeclineReason}”.` : '';
    return `${name} declined ${localDateTime(intake.trainerRespondedAt)}.${reason} Invite another trainer, or change the schedule first.`;
  }
  return `${name} has not been sent this schedule. Invite a trainer once every session is in place.`;
}

// Where the Trainer stands on this schedule, and what the Creator can do about it.
function TrainerInvitation({ intake, sessions, busy, error, editor, setEditor, mutate, token }) {
  const name = intake.trainerName || 'The trainer';
  const state = intake.trainerInvitationStatus || 'NotSent';
  const open = isUnpublished(intake);
  const { label, tone } = !open ? { label: 'Accepted', tone: 'published' }
    : intake.hasTrainer === false ? { label: 'No trainer yet', tone: 'draft' } : invitationOf(intake);
  const canInvite = open && (state === 'NotSent' || state === 'Declined');
  const invite = (trainerId) => mutate(() => creatorIntakesApi.inviteTrainer(token, intake.id, trainerId, intake.version),
    'Schedule sent. The intake is published as soon as the trainer accepts.');
  const withdraw = () => mutate(() => creatorIntakesApi.withdrawInvitation(token, intake.id, intake.version), 'Invitation withdrawn. You can change the schedule again.');
  return <section className="creator-intake-trainer" aria-labelledby="intake-trainer-heading">
    <div className="creator-section-heading">
      <div><h2 id="intake-trainer-heading">Trainer · {intake.hasTrainer === false ? 'No trainer yet' : name}</h2><p>{invitationText(intake, name)}</p></div>
      <span className={`creator-status ${tone}`}>{label}</span>
    </div>
    {open ? <div className="trainer-actions">
      {canInvite ? <button type="button" className="btn btn-primary" disabled={busy || !sessions.length || Boolean(editor)} onClick={() => setEditor({ type: 'invite' })}>Invite trainer</button> : null}
      {canInvite && !sessions.length ? <small className="trainer-help">Add at least one session first.</small> : null}
      {state === 'Invited' || state === 'Accepted' ? <button type="button" className="btn btn-ghost" disabled={busy} onClick={withdraw}>Withdraw invitation</button> : null}
    </div> : null}
    {editor?.type === 'invite' ? <InviteTrainerDialog courseId={intake.courseId} busy={busy} error={error} onInvite={invite} onClose={() => setEditor(null)} /> : null}
  </section>;
}

function SessionCard({ session, editable, deletable, onEdit, onDelete }) {
  return <article className="creator-application-row">
    <div>
      <p className="creator-eyebrow">{localDateTime(session.startsAt)}–{timeLabel(utcDate(session.endsAt))}</p>
      <h2>{session.label}</h2>
      {session.meetingLink ? <p>{session.meetingLink}</p> : null}
      {session.physicalAddress ? <p>{session.physicalAddress} · {formatCount(session.physicalCapacity, 'seat')}</p> : null}
    </div>
    {editable ? <div className="trainer-actions"><button className="btn btn-ghost" type="button" onClick={onEdit}>Edit</button>{deletable ? <button className="btn btn-ghost" type="button" onClick={onDelete}>Delete</button> : null}</div> : null}
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
  // Before publishing, the schedule changes only while no Trainer is deciding on it. After publishing, a session that has
  // not started can still move; the Trainer and booked learners are told.
  const editable = isUnpublished(intake) && ['NotSent', 'Declined'].includes(intake.trainerInvitationStatus || 'NotSent');
  const running = intake.status === 'Published' || intake.status === 'InProgress';
  const publishable = isUnpublished(intake) && intake.trainerInvitationStatus === 'Accepted';
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
  // An Intake that was confirmed and has run is closed by the Creator once its last session has ended.
  const completable = intake.status === 'InProgress' || (intake.status === 'Published' && Boolean(intake.confirmedToRunAt));
  const lastSessionEnds = sessions.length ? Math.max(...sessions.map((session) => utcDate(session.endsAt).getTime())) : utcDate(intake.endsAt).getTime();
  const finished = lastSessionEnds <= now;

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
      {intake.hasTrainer === false ? 'No trainer yet' : `Trainer ${intake.trainerName || 'assigned'}`}. Times are shown in your local time zone ({localTimezone}).
    </CreatorHeader>
    {notice ? <div className="trainer-notice" role="status">{notice}</div> : null}
    <CreatorError error={error} />
    {intake.cancelledAt ? <p>Cancelled {localDateTime(intake.cancelledAt)}{intake.cancellationReason === 'MinimumEnrollmentNotMet' ? ' because the intake did not reach the minimum.' : '.'}</p> : null}
    {intake.replacementIntakeId ? <p>Postponed to {onOpenClass
      ? <button type="button" className="btn btn-ghost" onClick={() => onOpenClass({ id: intake.replacementIntakeId })}>the replacement intake</button>
      : <Link to={replacementTarget}>the replacement intake</Link>}.</p> : null}
    {canPostpone ? <section>
      <h2>Postpone this intake</h2>
      {editor?.type === 'postpone'
        ? <ScheduleForm intake={replacementSchedule} submitLabel="Publish replacement" busy={busy} error={error} onError={setError} onCancel={() => setEditor(null)} onSave={postpone} />
        : <button type="button" className="btn btn-primary" disabled={busy} onClick={() => setEditor({ type: 'postpone' })}>Create replacement intake</button>}
    </section> : null}
    <TrainerInvitation intake={intake} sessions={sessions} busy={busy} error={error} editor={editor} setEditor={setEditor} mutate={mutate} token={query.token} />
    <div className="creator-section-heading"><h2>Schedule</h2>{editable && !editor ? <button type="button" className="btn btn-ghost" disabled={busy} onClick={() => setEditor({ type: 'schedule' })}>Edit schedule</button> : null}</div>
    <p className="creator-schedule-line">Registration {localDateTime(intake.registrationOpensAt)} → {localDateTime(intake.registrationClosesAt)} · teaching {localDateTime(intake.startsAt)} → {localDateTime(intake.endsAt)} · minimum {formatCount(intake.minEnrollment ?? 10, 'learner')}</p>
    {editor?.type === 'schedule' ? <EditClassScheduleForm key={intake.version} intake={intake} error={error} onError={setError} busy={busy} onCancel={() => setEditor(null)} onSave={(body) => mutate(() => creatorIntakesApi.update(query.token, intake.id, body), 'Schedule saved.')} /> : null}
    <div className="creator-section-heading"><h2>Sessions</h2>{editable && !editor ? <button type="button" className="btn btn-primary" disabled={busy} onClick={() => setEditor({ type: 'session' })}>Add session</button> : null}</div>
    {editor?.type === 'session' ? <SessionForm key={`${intake.version}:${editor.session?.id || 'new'}`} session={editor.session} intake={intake} error={error} onError={setError} busy={busy} onCancel={() => setEditor(null)} onSave={(body) => mutate(() => editor.session ? creatorIntakesApi.updateSession(query.token, intake.id, editor.session.id, body) : creatorIntakesApi.createSession(query.token, intake.id, body), editor.session ? (running ? 'Session moved. The trainer and booked learners were told.' : 'Session updated.') : 'Session added.')} /> : null}
    <SessionCalendar sessions={sessions} />
    {sessions.length ? <div className="creator-application-list creator-session-rows">{sessions.map((session) => <SessionCard key={session.id} session={session} deletable={editable}
      editable={(editable || (running && utcDate(session.startsAt).getTime() > now)) && !editor && !busy} onEdit={() => setEditor({ type: 'session', session })} onDelete={() => setEditor({ type: 'delete', session })} />)}</div> : <div className="creator-empty"><strong>No sessions yet</strong><p>Add every session before inviting a trainer.</p></div>}
    {editor?.type === 'delete' ? <div className="creator-confirm"><h2>Delete “{editor.session.label}”?</h2><button type="button" className="btn btn-primary" disabled={busy} onClick={() => mutate(() => creatorIntakesApi.deleteSession(query.token, intake.id, editor.session.id, intake.version), 'Session deleted.')}>Confirm deletion</button><button type="button" className="btn btn-ghost" disabled={busy} onClick={() => setEditor(null)}>Cancel</button></div> : null}
    {publishable ? <div className="creator-section-heading"><div><h2>Publish this intake</h2><p>An intake is normally published as soon as the trainer accepts. This one could not be, so publish it here once the problem in your notification is fixed. Publishing opens registration and fixes the timetable.</p></div><button type="button" className="btn btn-primary" disabled={busy || !sessions.length} onClick={() => mutate(() => creatorIntakesApi.publish(query.token, intake.id, intake.version), 'Intake published.')}>Publish</button></div> : null}
    {intake.status === 'Completed' ? <p>This intake is completed. The Trainer can still record attendance and final results, and learners can request their certificates.</p> : null}
    {completable ? <div className="creator-section-heading"><div><h2>Complete this intake</h2>
      <p>{finished
        ? 'Closes the intake for everyone. The Trainer can still record attendance and final results. This cannot be undone.'
        : `You can complete the intake after the last session ends on ${localDateTime(new Date(lastSessionEnds))}.`}</p></div>
      {editor?.type === 'complete'
        ? <div className="trainer-actions"><button type="button" className="btn btn-primary" disabled={busy} onClick={() => mutate(() => creatorIntakesApi.complete(query.token, intake.id), 'Intake completed.')}>Confirm completion</button><button type="button" className="btn btn-ghost" disabled={busy} onClick={() => setEditor(null)}>Keep intake open</button></div>
        : <button type="button" className="btn btn-primary" disabled={busy || !finished || Boolean(editor)} onClick={() => setEditor({ type: 'complete' })}>Complete intake</button>}
    </div> : null}
    {cancellable ? <div className="creator-section-heading"><div><h2>Cancel this intake</h2><p>Reserved credits are released. Confirmed learners receive a full refund.</p></div>{editor?.type === 'cancel' ? <button type="button" className="btn btn-primary" disabled={busy} onClick={() => mutate(async () => { await creatorIntakesApi.cancel(query.token, intake.id); return creatorIntakesApi.get(query.token, intake.id); }, 'Intake cancelled.')}>Confirm cancellation</button> : <button type="button" className="btn btn-ghost" disabled={busy} onClick={() => setEditor({ type: 'cancel' })}>Cancel intake</button>}</div> : null}
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
    {query.loading ? <div className="creator-empty" role="status">Loading intake…</div>
      : query.error ? <CreatorError error={query.error} onRetry={query.refresh} />
        : <IntakeWorkspace key={`${query.token}:${courseIntakeId}`} query={workspaceQuery} onOpenClass={course ? (replacement) => setSearchParams({ class: String(replacement.id) }, { replace: true }) : undefined} />}
  </section>;
}
