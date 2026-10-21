import { Link } from 'react-router-dom';
import { trainerIntakesApi } from '../../api/trainerIntakes';
import useTrainerQuery, { loadTrainerOverview } from './useTrainerQuery';
import { IntakeStatus, TrainerError, TrainerHeader, TrainerLoading } from './TrainerUi';
import { formatDate, intakeLink } from './intakeForm';

const GROUPS = [
  { title: 'Needs changes', statuses: ['Rejected'], empty: 'The Creator has not returned any Intakes.' },
  { title: 'Waiting for the Creator', statuses: ['PendingApproval'], empty: 'No Intakes are waiting for approval.' },
  { title: 'Approved', statuses: ['Published', 'InProgress', 'Completed'], empty: 'No Intakes are approved yet.' },
];

async function loadApprovals(token, key, signal) {
  const overview = await loadTrainerOverview(token, key, signal);
  const rejected = overview.intakes.filter((intake) => intake.status === 'Rejected');
  const notes = await Promise.all(rejected.map((intake) => trainerIntakesApi.get(token, intake.id, signal)
    .then((detail) => [intake.id, detail.confirmationNote], () => [intake.id, null])));
  return { ...overview, notes: Object.fromEntries(notes) };
}

function IntakeGroup({ title, empty, intakes, courseMap, notes }) {
  const headingId = `trainer-approvals-${title.replace(/\W+/g, '-').toLowerCase()}`;
  return <section aria-labelledby={headingId}>
    <div className="trainer-section-heading"><h2 id={headingId}>{title}</h2><span className="trainer-group-count">{intakes.length}</span></div>
    {!intakes.length ? <p className="trainer-group-empty">{empty}</p> : <div className="trainer-intake-list">{intakes.map((intake) => {
      const course = courseMap.get(intake.courseId);
      return <article className="trainer-intake-row trainer-approval-row" key={intake.id}>
        <div className="trainer-intake-title">
          <small><span>{course?.code || `Course #${intake.courseId}`}</span> · <span>Intake #{intake.id}</span></small>
          <h3><Link to={intakeLink(intake.id)}>{course?.title || `Course #${intake.courseId}`}</Link></h3>
          <p>{formatDate(intake.startsAt)} → {formatDate(intake.endsAt)}</p>
          {intake.status === 'Rejected' ? <div className="trainer-workflow-note"><strong>Creator feedback</strong><p>{notes[intake.id] || 'No note was recorded.'}</p></div> : null}
        </div>
        <IntakeStatus status={intake.status} />
        <Link className="trainer-open" to={intakeLink(intake.id)}>{intake.status === 'Rejected' ? 'Revise Intake →' : 'Open →'}</Link>
      </article>;
    })}</div>}
  </section>;
}

export default function TrainerApprovalsPage() {
  const query = useTrainerQuery(loadApprovals);
  const courseMap = new Map((query.data?.courses || []).map((course) => [course.id, course]));
  const submitted = (query.data?.intakes || []).filter((intake) => GROUPS.some((group) => group.statuses.includes(intake.status)));
  return <section className="trainer-page">
    <TrainerHeader eyebrow="Course / Approval status" title="Approval status" action={<button type="button" className="btn btn-ghost" onClick={query.refresh} disabled={query.loading}>Refresh</button>}>
      Follow each submitted Intake through Creator approval. Returned Intakes show the Creator feedback to address before you resubmit.
    </TrainerHeader>
    {query.loading ? <TrainerLoading /> : query.error ? <TrainerError error={query.error} onRetry={query.refresh} />
      : !submitted.length ? <div className="trainer-empty"><h2>No Intakes submitted yet</h2><p>Submit an Intake for Creator approval to track it here.</p></div>
        : GROUPS.map((group) => <IntakeGroup key={group.title} title={group.title} empty={group.empty} intakes={submitted.filter((intake) => group.statuses.includes(intake.status))} courseMap={courseMap} notes={query.data.notes} />)}
  </section>;
}
