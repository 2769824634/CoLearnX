import { useState } from 'react';
import { trainerIntakesApi } from '../../api/trainerIntakes';
import SessionCalendar from '../../components/SessionCalendar';
import { localDateTime, timeLabel } from '../creator/openClass';
import { utcDate } from '../../utils/utcDates';
import useTrainerQuery from './useTrainerQuery';
import { TrainerError } from './TrainerUi';
import { formatDate } from './intakeForm';

const loadInvitations = (token, key, signal) => trainerIntakesApi.invitations(token, signal);
const STATUS = {
  Invited: { label: 'Waiting for you', tone: 'pendingapproval' },
  Accepted: { label: 'Accepted', tone: 'published' },
  Declined: { label: 'Declined', tone: 'rejected' },
};
// Accepting publishes the Intake, which then appears under Courses and leaves this page.
// An accepted Intake only stays here when it could not be published automatically and waits for the Creator.
const unpublished = (invitation) => invitation.intakeStatus === 'Draft' || invitation.intakeStatus === 'Rejected';

function answered(invitation) {
  if (invitation.status === 'Accepted') return `Accepted ${formatDate(invitation.respondedAt)}. The intake could not be published automatically, so the creator still has to publish it.`;
  const reason = invitation.declineReason ? ` Your reason: “${invitation.declineReason}”` : '';
  return `Declined ${formatDate(invitation.respondedAt)}.${reason}`;
}

function SessionList({ sessions }) {
  return <details className="trainer-intake-sessions">
    <summary>All {sessions.length} sessions as a list</summary>
    <ol>{sessions.map((session) => <li key={session.id}>
      {session.label}: {localDateTime(session.startsAt)}–{timeLabel(utcDate(session.endsAt))}
      {session.physicalAddress ? ` · ${session.physicalAddress}` : session.meetingLink ? ' · online' : ''}
      {session.clashes?.length ? <strong className="trainer-field-error"> Clashes with {session.clashes.map((clash) => `${clash.courseCode} ${clash.sessionLabel}`).join(', ')}</strong> : null}
    </li>)}</ol>
  </details>;
}

function InvitationCard({ invitation, busy, error, onAccept, onDecline }) {
  const [declining, setDeclining] = useState(false);
  const [reason, setReason] = useState('');
  const { label, tone } = STATUS[invitation.status] || { label: invitation.status, tone: '' };
  const reasonId = `intake-decline-${invitation.intakeId}`;
  return <article className="trainer-intake-invitation">
    <div className="trainer-section-heading">
      <div className="trainer-intake-title">
        <small><span>{invitation.courseCode}</span> · <span>from {invitation.creatorName}</span> · <span>Intake #{invitation.intakeId}</span></small>
        <h3>{invitation.courseTitle}</h3>
        <p>{invitation.status === 'Invited'
          ? `Sent ${formatDate(invitation.invitedAt)}. Registration closes ${localDateTime(invitation.registrationClosesAt)}; at least ${invitation.minEnrollment} learners are needed for it to run.`
          : answered(invitation)}</p>
      </div>
      <span className={`trainer-status ${tone}`}>{label}</span>
    </div>
    {invitation.status === 'Invited' ? <SessionCalendar sessions={invitation.sessions} caption="Check these dates against your own calendar" /> : null}
    {invitation.status === 'Invited' ? <SessionList sessions={invitation.sessions} /> : null}
    <TrainerError error={error} />
    {invitation.status === 'Invited' ? declining
      ? <form className="trainer-form" onSubmit={(event) => { event.preventDefault(); onDecline(invitation, reason.trim()); }}>
        <div className="form-group"><label htmlFor={reasonId}>Reason for the creator (optional)</label>
          <textarea id={reasonId} maxLength={500} rows={2} value={reason} onChange={(event) => setReason(event.target.value)} /></div>
        <div className="trainer-actions">
          <button type="submit" className="btn btn-primary btn-sm" disabled={busy}>Confirm decline</button>
          <button type="button" className="btn btn-ghost btn-sm" disabled={busy} onClick={() => setDeclining(false)}>Back</button>
        </div>
      </form>
      : <div className="trainer-actions">
        <button type="button" className="btn btn-ghost btn-sm" disabled={busy} onClick={() => setDeclining(true)}>Decline</button>
        <button type="button" className="btn btn-primary btn-sm" disabled={busy} onClick={() => onAccept(invitation)}>Accept schedule</button>
      </div> : null}
  </article>;
}

function Group({ title, empty, items, busyId, errors, onAccept, onDecline }) {
  const headingId = `trainer-intake-invitations-${title.replace(/\W+/g, '-').toLowerCase()}`;
  return <section aria-labelledby={headingId}>
    <div className="trainer-section-heading"><h2 id={headingId}>{title}</h2><span className="trainer-group-count">{items.length}</span></div>
    {!items.length ? <p className="trainer-group-empty">{empty}</p>
      : <div className="trainer-intake-list">{items.map((invitation) => <InvitationCard key={invitation.intakeId} invitation={invitation}
        busy={busyId === invitation.intakeId} error={errors[invitation.intakeId]} onAccept={onAccept} onDecline={onDecline} />)}</div>}
  </section>;
}

// Intake schedules a Creator sent this Trainer: review every session, then accept or decline the whole timetable.
export default function TrainerIntakeInvitations() {
  const query = useTrainerQuery(loadInvitations);
  const [busyId, setBusyId] = useState(null);
  const [errors, setErrors] = useState({});
  const [notice, setNotice] = useState('');
  const items = (Array.isArray(query.data) ? query.data : []).filter(unpublished);

  async function reply(invitation, command) {
    setBusyId(invitation.intakeId);
    setErrors((current) => ({ ...current, [invitation.intakeId]: null }));
    setNotice('');
    try {
      const updated = await command();
      // A published Intake leaves this page, so say where it went.
      if (updated.status === 'Accepted' && !unpublished(updated)) {
        setNotice(`You accepted ${updated.courseTitle} (Intake #${updated.intakeId}). It is now published and listed under Courses.`);
      }
      query.setData((Array.isArray(query.data) ? query.data : []).map((item) => item.intakeId === updated.intakeId ? updated : item));
    } catch (requestError) {
      setErrors((current) => ({ ...current, [invitation.intakeId]: requestError }));
    } finally {
      setBusyId(null);
    }
  }
  const accept = (invitation) => reply(invitation, () => trainerIntakesApi.acceptInvitation(query.token, invitation.intakeId));
  const decline = (invitation, reason) => reply(invitation, () => trainerIntakesApi.declineInvitation(query.token, invitation.intakeId, reason));

  if (query.loading) return <div className="trainer-empty" role="status">Loading intake schedules…</div>;
  if (query.error) return <TrainerError error={query.error} onRetry={query.refresh} />;
  const shared = { busyId, errors, onAccept: accept, onDecline: decline };
  return <>
    {notice ? <p className="trainer-notice" role="status">{notice}</p> : null}
    <Group title="Intake schedules to review" empty="No intake schedules are waiting for you." items={items.filter((item) => item.status === 'Invited')} {...shared} />
    {items.some((item) => item.status !== 'Invited')
      ? <Group title="Answered intake schedules" empty="" items={items.filter((item) => item.status !== 'Invited')} {...shared} />
      : null}
  </>;
}
