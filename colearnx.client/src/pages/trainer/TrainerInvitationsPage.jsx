import { useState } from 'react';
import { courseTrainersApi } from '../../api/courseTrainers';
import useTrainerQuery from './useTrainerQuery';
import { TrainerError, TrainerHeader, TrainerLoading } from './TrainerUi';
import { formatDate } from './intakeForm';

const GROUPS = [
  { title: 'Waiting for you', statuses: ['Invited'], empty: 'No invitations are waiting for you.' },
  { title: 'My courses', statuses: ['Accepted'], empty: 'Accept an invitation to add a Course here.' },
  { title: 'History', statuses: ['Declined', 'Removed'], empty: 'Declined and closed invitations appear here.' },
];
const STATUS = {
  Invited: { label: 'Waiting', tone: 'pendingapproval' },
  Accepted: { label: 'Teaching', tone: 'published' },
  Declined: { label: 'Declined', tone: 'rejected' },
  Removed: { label: 'Closed', tone: 'draft' },
};

const loadInvitations = (token, key, signal) => courseTrainersApi.myInvitations(token, signal);

function when(invitation) {
  if (invitation.status === 'Removed') return `Closed ${formatDate(invitation.removedAt)}`;
  if (invitation.status === 'Invited') return `Invited ${formatDate(invitation.invitedAt)}`;
  return `${invitation.status === 'Accepted' ? 'Accepted' : 'Declined'} ${formatDate(invitation.respondedAt)}`;
}

function InvitationRow({ invitation, busy, error, onAnswer }) {
  const { label, tone } = STATUS[invitation.status] || { label: invitation.status, tone: '' };
  return <article className="trainer-intake-row trainer-approval-row">
    <div className="trainer-intake-title">
      <small><span>{invitation.courseCode}</span> · <span>from {invitation.creatorName}</span></small>
      <h3>{invitation.courseTitle}</h3>
      <p>{when(invitation)}</p>
      <TrainerError error={error} />
    </div>
    <span className={`trainer-status ${tone}`}>{label}</span>
    {invitation.status === 'Invited' ? <div className="trainer-actions" style={{ margin: 0 }}>
      <button type="button" className="btn btn-ghost btn-sm" disabled={busy} onClick={() => onAnswer(invitation, false)}>Decline</button>
      <button type="button" className="btn btn-primary btn-sm" disabled={busy} onClick={() => onAnswer(invitation, true)}>Accept</button>
    </div> : null}
  </article>;
}

function InvitationGroup({ title, empty, invitations, busyId, errors, onAnswer }) {
  const headingId = `trainer-invitations-${title.replace(/\W+/g, '-').toLowerCase()}`;
  return <section aria-labelledby={headingId}>
    <div className="trainer-section-heading"><h2 id={headingId}>{title}</h2><span className="trainer-group-count">{invitations.length}</span></div>
    {!invitations.length ? <p className="trainer-group-empty">{empty}</p>
      : <div className="trainer-intake-list">{invitations.map((invitation) => <InvitationRow key={invitation.id} invitation={invitation}
        busy={busyId === invitation.id} error={errors[invitation.id]} onAnswer={onAnswer} />)}</div>}
  </section>;
}

export default function TrainerInvitationsPage() {
  const query = useTrainerQuery(loadInvitations);
  const [busyId, setBusyId] = useState(null);
  const [errors, setErrors] = useState({});
  const invitations = Array.isArray(query.data) ? query.data : [];

  async function answer(invitation, accept) {
    setBusyId(invitation.id);
    setErrors((current) => ({ ...current, [invitation.id]: null }));
    try {
      const updated = await (accept ? courseTrainersApi.accept : courseTrainersApi.decline)(query.token, invitation.id);
      query.setData(invitations.map((item) => item.id === updated.id ? updated : item));
    } catch (requestError) {
      setErrors((current) => ({ ...current, [invitation.id]: requestError }));
    } finally {
      setBusyId(null);
    }
  }

  return <section className="trainer-page">
    <TrainerHeader eyebrow="Course / Teaching invitations" title="Teaching invitations" action={<button type="button" className="btn btn-ghost" onClick={query.refresh} disabled={query.loading}>Refresh</button>}>
      Creators invite you to teach their Courses. Accept an invitation to become available when they schedule an Intake.
    </TrainerHeader>
    {query.loading ? <TrainerLoading /> : query.error ? <TrainerError error={query.error} onRetry={query.refresh} />
      : !invitations.length ? <div className="trainer-empty"><h2>No teaching invitations yet</h2><p>When a Creator invites you to teach a Course, it will appear here.</p></div>
        : GROUPS.map((group) => <InvitationGroup key={group.title} title={group.title} empty={group.empty} busyId={busyId} errors={errors} onAnswer={answer}
          invitations={invitations.filter((item) => group.statuses.includes(item.status))} />)}
  </section>;
}
