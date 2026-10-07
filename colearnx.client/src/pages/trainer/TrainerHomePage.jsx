import { Link } from 'react-router-dom';
import { useAuth } from '../../auth/AuthContext';
import { formatCount } from '../businessPresentation';
import useTrainerQuery, { loadTrainerOverview } from './useTrainerQuery';
import { IntakeRows, IntakeStatus, TrainerError, TrainerHeader, TrainerLoading } from './TrainerUi';
import { formatDate, intakeLink, isEditable, localTimezone } from './intakeForm';

const PIPELINE = [
  ['editable', 'Ready to prepare', 'Draft & rejected', (status) => isEditable(status)],
  ['PendingApproval', 'With the Creator', 'Pending approval', (status) => status === 'PendingApproval'],
  ['delivery', 'Confirmed delivery', 'Published & in progress', (status) => ['Published', 'InProgress'].includes(status)],
];

const QUICK_ACTIONS = [
  ['/trainer/attendance', 'Attendance', 'Record attendance for your Sessions.'],
  ['/trainer/learners', 'Learner List', 'Learners enrolled in your Intakes.'],
];

const DELIVERY_STEPS = ['Prepare a Draft', 'Add Sessions', 'Submit to Creator', 'Creator confirms'];

function attentionSummary(prepare, waiting) {
  const items = [
    prepare ? `${formatCount(prepare, 'Intake')} to prepare` : null,
    waiting ? `${formatCount(waiting, 'Intake')} waiting for the Creator` : null,
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
        <p>Once the course Creator confirms an Intake, it appears here.</p>
      </div>
    </section>;
  }
  const course = courses.find((item) => item.id === intake.courseId);
  const starts = new Date(intake.startsAt);
  return <section className="trainer-next" aria-labelledby="trainer-next-title">
    <div className="trainer-next-date" aria-hidden="true">
      <strong>{starts.getDate()}</strong>
      <span>{starts.toLocaleDateString('en-GB', { month: 'short', year: 'numeric' })}</span>
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
  const [prepare, waiting] = PIPELINE.map(([, , , matches]) => intakes.filter((intake) => matches(intake.status)));
  const firstName = user?.fullName?.split(' ')[0] || 'Trainer';

  return <section className="trainer-page">
    <TrainerHeader title={`Welcome back, ${firstName}.`} action={<Link className="btn btn-primary" to="/trainer/courses/new">+ Create Intake</Link>}>
      {query.data ? attentionSummary(prepare.length, waiting.length) : 'Plan your next cohort. Build a schedule, then send it to the course Creator for confirmation.'}
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
          <div className="trainer-section-heading"><h2 id="trainer-prepare-title">Pick up where you left off</h2><Link to="/trainer/courses">All Intakes →</Link></div>
          <TrainerError error={query.data.catalogError} onRetry={query.refresh} retryLabel="Reload course names" />
          <IntakeRows intakes={prepare.slice(0, 5)} courses={query.data.courses} empty="Your planning queue is clear. Create an Intake when you are ready for the next cohort." />
        </section>
      </div>
      <aside className="trainer-home-side">
        <section className="trainer-quick-actions" aria-labelledby="trainer-actions-title">
          <h2 id="trainer-actions-title">Quick actions</h2>
          {QUICK_ACTIONS.map(([to, label, desc]) => <Link key={to} to={to}><strong>{label}</strong><span>{desc}</span></Link>)}
        </section>
        <section className="trainer-delivery-path" aria-labelledby="trainer-path-title">
          <h2 id="trainer-path-title">Your path to delivery</h2>
          <ol>{DELIVERY_STEPS.map((step) => <li key={step}>{step}</li>)}</ol>
          <p>Submitting does not publish an Intake. All dates are shown in {localTimezone}.</p>
        </section>
      </aside>
    </div>}
  </section>;
}
