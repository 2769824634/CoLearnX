import { Link } from 'react-router-dom';
import { useAuth } from '../../auth/AuthContext';
import { utcDate } from '../../utils/utcDates.js';
import { formatCount } from '../businessPresentation';
import useTrainerQuery, { loadTrainerOverview } from './useTrainerQuery';
import { IntakeRows, IntakeStatus, TrainerError, TrainerHeader, TrainerLoading } from './TrainerUi';
import { formatDate, intakeLink } from './intakeForm';

const PIPELINE = [
  ['Published', 'Upcoming', 'Assigned and published', (status) => status === 'Published'],
  ['InProgress', 'Teaching now', 'In progress', (status) => status === 'InProgress'],
  ['Completed', 'Finished', 'Completed classes', (status) => status === 'Completed'],
];

const QUICK_ACTIONS = [
  ['/trainer/courses', 'Attendance', 'Choose a Course, Intake and Session to record attendance.'],
  ['/trainer/courses', 'Learner List', 'Choose a Course to manage its learners.'],
];

const DELIVERY_STEPS = ['Accept the teaching invitation', 'Open the assigned class', 'Update the meeting link', 'Record attendance'];

function attentionSummary(upcoming, teaching) {
  const items = [
    upcoming ? formatCount(upcoming, 'upcoming class', 'upcoming classes') : null,
    teaching ? `${formatCount(teaching, 'class')} in progress` : null,
  ].filter(Boolean);
  return items.length ? `${items.join(' and ')}.` : 'Nothing needs your attention right now.';
}

function teachingNext(intakes) {
  const now = Date.now();
  const live = intakes.filter((intake) => intake.status === 'InProgress');
  const upcoming = intakes
    .filter((intake) => intake.status === 'Published' && new Date(intake.endsAt).getTime() > now)
    .sort((a, b) => new Date(a.startsAt) - new Date(b.startsAt));
  return live[0] || upcoming[0] || null;
}

function TeachingNext({ intake, courses }) {
  if (!intake) {
    return <section className="trainer-next trainer-next-empty" aria-labelledby="trainer-next-title">
      <div className="trainer-next-body">
        <p className="trainer-eyebrow" id="trainer-next-title">Teaching next</p>
        <h2>No confirmed delivery yet</h2>
        <p>Published classes assigned to you appear here.</p>
      </div>
    </section>;
  }
  const course = courses.find((item) => item.id === intake.courseId);
  const starts = utcDate(intake.startsAt);
  return <section className="trainer-next" aria-labelledby="trainer-next-title">
    <div className="trainer-next-date" aria-hidden="true">
      <strong>{starts.getUTCDate()}</strong>
      <span>{starts.toLocaleDateString('en-GB', { month: 'short', year: 'numeric', timeZone: 'UTC' })}</span>
    </div>
    <div className="trainer-next-body">
      <p className="trainer-eyebrow" id="trainer-next-title">Teaching next</p>
      <small>{course?.code || `Course #${intake.courseId}`} · Intake #{intake.id}</small>
      <h2>{course?.title || `Course #${intake.courseId}`}</h2>
      <p>{formatDate(intake.startsAt)} → {formatDate(intake.endsAt)}</p>
      <div className="trainer-next-actions">
        <IntakeStatus status={intake.status} />
        <Link className="btn btn-primary" to={intakeLink(intake.id)}>Open Intake</Link>
      </div>
    </div>
  </section>;
}

export default function TrainerHomePage() {
  const { user } = useAuth();
  const query = useTrainerQuery(loadTrainerOverview);
  const intakes = query.data?.intakes || [];
  const [upcoming, teaching] = PIPELINE.map(([, , , matches]) => intakes.filter((intake) => matches(intake.status)));
  const firstName = user?.fullName?.split(' ')[0] || 'Trainer';

  return <section className="trainer-page">
    <TrainerHeader title={`Welcome back, ${firstName}.`} action={<Link className="btn btn-primary" to="/trainer/invitations">Teaching invitations</Link>}>
      {query.data ? attentionSummary(upcoming.length, teaching.length) : 'Classes assigned to you appear here after you accept a teaching invitation.'}
    </TrainerHeader>
    {query.loading ? <TrainerLoading /> : query.error ? <TrainerError error={query.error} onRetry={query.refresh} /> : <div className="trainer-home-layout">
      <div className="trainer-home-main">
        <TeachingNext intake={teachingNext(intakes)} courses={query.data.courses} />
        <ol className="trainer-pipeline" aria-label="Intake pipeline">
          {PIPELINE.map(([status, label, caption, matches]) => <li key={status} data-stage={status.toLowerCase()}>
            <Link to={`/trainer/courses?status=${status}`}>
              <span className="trainer-stage-count">{intakes.filter((intake) => matches(intake.status)).length}</span>
              <strong>{label}</strong>
              <span>{caption}</span>
            </Link>
          </li>)}
        </ol>
        <section aria-labelledby="trainer-prepare-title">
          <div className="trainer-section-heading"><h2 id="trainer-prepare-title">Pick up where you left off</h2><Link to="/trainer/courses">All Courses →</Link></div>
          <TrainerError error={query.data.catalogError} onRetry={query.refresh} retryLabel="Reload course names" />
          <IntakeRows intakes={[...teaching, ...upcoming].slice(0, 5)} courses={query.data.courses} empty="No assigned classes yet. Accept a teaching invitation to be added to a course." />
        </section>
      </div>
      <aside className="trainer-home-side">
        <section className="trainer-quick-actions" aria-labelledby="trainer-actions-title">
          <h2 id="trainer-actions-title">Quick actions</h2>
          {QUICK_ACTIONS.map(([to, label, desc]) => <Link key={label} to={to}><strong>{label}</strong><span>{desc}</span></Link>)}
        </section>
        <section className="trainer-delivery-path" aria-labelledby="trainer-path-title">
          <h2 id="trainer-path-title">Your path to delivery</h2>
          <ol>{DELIVERY_STEPS.map((step) => <li key={step}>{step}</li>)}</ol>
          <p>The course Creator publishes the schedule. All displayed dates use UTC.</p>
        </section>
      </aside>
    </div>}
  </section>;
}
