import { Link } from 'react-router-dom';
import { useAuth } from '../../auth/AuthContext';
import useTrainerQuery, { loadTrainerOverview } from './useTrainerQuery';
import { IntakeRows, TrainerError, TrainerHeader, TrainerLoading } from './TrainerUi';
import { isEditable, localTimezone } from './intakeForm';

export default function TrainerHomePage() {
  const { user } = useAuth();
  const query = useTrainerQuery(loadTrainerOverview);
  const intakes = query.data?.intakes || [];
  const needsWork = intakes.filter((intake) => isEditable(intake.status));
  const pending = intakes.filter((intake) => intake.status === 'PendingApproval');
  const delivery = intakes.filter((intake) => ['Published', 'InProgress'].includes(intake.status));
  return <section className="trainer-page">
    <TrainerHeader title={`Your teaching workspace, ${user?.fullName?.split(' ')[0] || 'Trainer'}.`} action={<Link className="btn btn-primary" to="/trainer/courses/new">+ Create Intake</Link>}>
      Plan your next cohort. Build a schedule, then send it to the course Creator for confirmation.
    </TrainerHeader>
    {query.loading ? <TrainerLoading /> : query.error ? <TrainerError error={query.error} onRetry={query.refresh} /> : <>
      <div className="trainer-stats">
        {[['01', 'Ready to prepare', needsWork.length, 'Draft & rejected', 'editable'], ['02', 'With the Creator', pending.length, 'Pending approval', 'PendingApproval'], ['03', 'Confirmed delivery', delivery.length, 'Published & in progress', 'delivery']].map(([number, title, count, caption, status]) => <Link to={`/trainer/courses?status=${status}`} key={number}><span className="trainer-stat-index">{number}</span><h2>{title}</h2><strong>{count}</strong><span>{caption}</span></Link>)}
      </div>
      <div className="trainer-section-heading"><div><p className="trainer-eyebrow">Planning desk</p><h2>Pick up where you left off</h2></div><Link to="/trainer/courses">All Intakes →</Link></div>
      <TrainerError error={query.data.catalogError} onRetry={query.refresh} retryLabel="Reload course names" />
      <IntakeRows intakes={needsWork.slice(0, 5)} courses={query.data.courses} empty="Your planning queue is clear. Create an Intake when you are ready for the next cohort." />
      <div className="trainer-workflow-note"><strong>Your path to delivery</strong><p>Prepare a Draft → Add Sessions → Submit to Creator → Wait for confirmation.</p><span>Submitting does not publish an Intake. All dates are shown in {localTimezone}.</span></div>
    </>}
  </section>;
}
