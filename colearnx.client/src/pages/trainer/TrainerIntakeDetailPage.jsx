import { Link, useParams } from 'react-router-dom';
import { formatDate, safeMeetingLink } from './intakeForm';
import useTrainerQuery, { loadTrainerDetail } from './useTrainerQuery';
import { IntakeStatus, TrainerError, TrainerHeader, TrainerLoading } from './TrainerUi';
import TrainerReservationPanel from './TrainerReservationPanel';
import { formatCount, formatUtcDate } from '../businessPresentation';

function SessionCard({ intakeId, session }) {
  const meetingLink = safeMeetingLink(session.meetingLink);
  return <article className="trainer-session-card">
    <div className="trainer-section-heading"><div><span className="trainer-eyebrow">Session #{session.id}</span><h3><Link to={`/trainer/courses/intakes/${intakeId}/sessions/${session.id}`}>{session.label}</Link></h3></div><Link className="btn btn-ghost" to={`/trainer/courses/intakes/${intakeId}/sessions/${session.id}`}>Open Session</Link></div>
    <p className="trainer-session-time">{formatDate(session.startsAt)} → {formatDate(session.endsAt)}</p>
    <div className="trainer-session-locations">
      {session.meetingLink ? <div><strong>Online</strong>{meetingLink ? <a href={meetingLink} target="_blank" rel="noopener noreferrer">{meetingLink}</a> : <span>Invalid meeting link</span>}</div> : null}
      {session.physicalAddress ? <div><strong>Physical · {formatCount(session.physicalCapacity, 'seat')}</strong><span>{session.physicalAddress}</span><small>Booking closes {formatDate(session.physicalBookingDeadline)}</small></div> : null}
    </div>
  </article>;
}

function IntakeWorkspace({ query }) {
  const { intake, courses, catalogError } = query.data;
  const course = courses.find((item) => item.id === intake.courseId);
  const sessions = [...intake.sessions].sort((first, second) => new Date(first.startsAt) - new Date(second.startsAt));

  return <>
    <TrainerHeader eyebrow={`${course?.code || `Course #${intake.courseId}`} / Intake #${intake.id}`} title={course?.title || `Course #${intake.courseId}`} action={<IntakeStatus status={intake.status} />}>
      This class was assigned to you. The course Creator owns the schedule.
    </TrainerHeader>
    {catalogError ? <p className="trainer-help">Course names are unavailable ({catalogError.code || 'NETWORK_ERROR'}). Your Intake data is still available.</p> : null}
    {intake.confirmedToRunAt ? <p className="trainer-notice">Class confirmed to run {formatDate(intake.confirmedToRunAt)}. Reserved credits captured.</p> : null}
    {intake.cancelledAt ? <p className="trainer-help">Cancelled {formatDate(intake.cancelledAt)}{intake.cancellationReason === 'MinimumEnrollmentNotMet' ? ': minimum enrollment was not met' : '.'}</p> : null}
    {intake.replacementForIntakeId ? <p>Replacement for <Link to={`/trainer/courses/intakes/${intake.replacementForIntakeId}`}>Intake #{intake.replacementForIntakeId}</Link>.</p> : null}
    {intake.replacementIntakeId ? <p>Postponed to <Link to={`/trainer/courses/intakes/${intake.replacementIntakeId}`}>Intake #{intake.replacementIntakeId}</Link>.</p> : null}
    <div className="trainer-section-heading"><h2>Registration & delivery</h2></div>
    <dl className="trainer-schedule-summary">{[['Registration opens', intake.registrationOpensAt, formatUtcDate], ['Registration closes', intake.registrationClosesAt, formatUtcDate], ['Delivery starts', intake.startsAt, formatDate], ['Delivery ends', intake.endsAt, formatDate]].map(([label, value, format]) => <div key={label}><dt>{label}</dt><dd>{format(value)}</dd></div>)}<div><dt>Minimum enrollment</dt><dd>{formatCount(intake.minEnrollment ?? 10, 'learner')}</dd></div></dl>
    <TrainerReservationPanel key={intake.id} intake={intake} />
    <div className="trainer-section-heading"><div><p className="trainer-eyebrow">Teaching plan</p><h2>Sessions <span className="trainer-count">{sessions.length}</span></h2></div></div>
    {sessions.length ? <div className="trainer-sessions">{sessions.map((session) => <SessionCard key={session.id} intakeId={intake.id} session={session} />)}</div> : <div className="trainer-empty"><strong>No sessions yet</strong><p>The course Creator adds sessions before this class is published.</p></div>}
    <p className="trainer-detail-footer">Intake #{intake.id} · Course #{intake.courseId}</p>
  </>;
}

export default function TrainerIntakeDetailPage() {
  const { courseIntakeId } = useParams();
  const query = useTrainerQuery(loadTrainerDetail, courseIntakeId);
  return <section className="trainer-page"><div className="trainer-detail-nav"><Link className="trainer-back" to="/trainer/courses">← All Intakes</Link></div>
    {query.loading ? <TrainerLoading /> : query.error ? <TrainerError error={query.error} onRetry={query.refresh} /> : <IntakeWorkspace key={`${query.token}:${courseIntakeId}`} query={query} />}
  </section>;
}
