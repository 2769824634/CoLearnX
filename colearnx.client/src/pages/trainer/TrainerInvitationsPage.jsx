import { useState } from 'react';
import { courseTrainersApi } from '../../api/courseTrainers';
import useTrainerQuery from './useTrainerQuery';
import { TrainerError, TrainerHeader, TrainerLoading } from './TrainerUi';
import { formatDate } from './intakeForm';
import TrainerIntakeInvitations from './TrainerIntakeInvitations';

const GROUPS = [
  { title: 'Waiting for you', statuses: ['Invited'], empty: 'No invitations are waiting for you.' },
  { title: 'Applications', statuses: ['Requested'], empty: 'You have no pending teaching applications.' },
  { title: 'My courses', statuses: ['Accepted'], empty: 'Accept an invitation to add a Course here.' },
  { title: 'History', statuses: ['Declined', 'Removed'], empty: 'Declined and closed invitations appear here.' },
];
const STATUS = {
  Invited: { label: 'Waiting', tone: 'pendingapproval' },
  Accepted: { label: 'Teaching', tone: 'published' },
  Declined: { label: 'Declined', tone: 'rejected' },
  Removed: { label: 'Closed', tone: 'draft' },
  Requested: { label: 'Applied', tone: 'pendingapproval' },
};

const loadInvitations = async (token, key, signal) => {
  const [invitations, available] = await Promise.all([
    courseTrainersApi.myInvitations(token, signal),
    courseTrainersApi.available(token, signal),
  ]);
  return { invitations, available };
};

const canApply = (course) => !course.status || course.status === 'Declined' || course.status === 'Removed';

function when(invitation) {
  if (invitation.status === 'Removed') return `Closed ${formatDate(invitation.removedAt)}`;
  if (invitation.status === 'Invited') return `Invited ${formatDate(invitation.invitedAt)}`;
  if (invitation.status === 'Requested') return `Applied ${formatDate(invitation.invitedAt)}`;
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
  const invitations = Array.isArray(query.data?.invitations) ? query.data.invitations : [];
  const available = Array.isArray(query.data?.available) ? query.data.available : [];

  async function answer(invitation, accept) {
    setBusyId(invitation.id);
    setErrors((current) => ({ ...current, [invitation.id]: null }));
    try {
      const updated = await (accept ? courseTrainersApi.accept : courseTrainersApi.decline)(query.token, invitation.id);
      query.setData({ ...query.data, invitations: invitations.map((item) => item.id === updated.id ? updated : item) });
    } catch (requestError) {
      setErrors((current) => ({ ...current, [invitation.id]: requestError }));
    } finally {
      setBusyId(null);
    }
  }

  async function apply(course) {
    setBusyId(`course-${course.courseId}`);
    setErrors((current) => ({ ...current, [`course-${course.courseId}`]: null }));
    try {
      const requested = await courseTrainersApi.apply(query.token, course.courseId);
      const existing = invitations.some((item) => item.id === requested.id);
      query.setData({
        invitations: existing
          ? invitations.map((item) => item.id === requested.id ? requested : item)
          : [...invitations, requested],
        available: available.map((item) => item.courseId === course.courseId ? { ...item, status: 'Requested' } : item),
      });
    } catch (requestError) {
      setErrors((current) => ({ ...current, [`course-${course.courseId}`]: requestError }));
    } finally {
      setBusyId(null);
    }
  }

  return <section className="trainer-page">
    <TrainerHeader eyebrow="Course / Teaching invitations" title="Teaching invitations" action={<button type="button" className="btn btn-ghost" onClick={query.refresh} disabled={query.loading}>Refresh</button>}>
      Review intake schedules Creators send you, apply for published Courses in your certified specialties, or respond when a Creator invites you to a Course.
    </TrainerHeader>
    <TrainerIntakeInvitations />
    {query.loading ? <TrainerLoading /> : query.error ? <TrainerError error={query.error} onRetry={query.refresh} />
      : <>
        <section aria-label="Available courses">
          <div className="trainer-section-heading"><h2>Available courses</h2><span className="trainer-group-count">{available.filter(canApply).length}</span></div>
          <div className="trainer-intake-list">{available.filter(canApply).map((course) => <article className="trainer-intake-row trainer-approval-row" key={course.courseId}>
            <div className="trainer-intake-title"><small>{course.courseCode} · {course.specialtyName}</small><h3>{course.courseTitle}</h3><p>Created by {course.creatorName}</p><TrainerError error={errors[`course-${course.courseId}`]} /></div>
            <button type="button" className="btn btn-primary btn-sm" disabled={busyId === `course-${course.courseId}`} onClick={() => apply(course)}>Apply to teach</button>
          </article>)}</div>
        </section>
        {!invitations.length && !available.length ? <div className="trainer-empty"><h2>No teaching invitations yet</h2><p>No matching published Courses or invitations are available right now.</p></div> : null}
        {GROUPS.map((group) => <InvitationGroup key={group.title} title={group.title} empty={group.empty} busyId={busyId} errors={errors} onAnswer={answer}
          invitations={invitations.filter((item) => group.statuses.includes(item.status))} />)}
      </>}
  </section>;
}
