import { Link } from 'react-router-dom';
import { creatorCoursesApi } from '../../api';
import { useAuth } from '../../auth/AuthContext';
import useTrainerQuery from '../trainer/useTrainerQuery';
import { formatCount } from '../businessPresentation';
import { CreatorError, CreatorHeader } from './CreatorUi';

const PIPELINE = [
  ['Draft', 'Drafts', 'Edit and attach materials'],
  ['PendingApproval', 'In Admin review', 'Waiting for a decision'],
  ['Published', 'Published', 'Open in the catalogue'],
];

const QUICK_ACTIONS = [
  ['/creator/usage', 'Usage Records', 'Where your materials have been used.'],
];

const loadCreatorOverview = (token, key, signal) => creatorCoursesApi.list(token, signal);

function attentionSummary(returned) {
  if (!returned) return 'Nothing needs your attention right now.';
  return `${formatCount(returned, 'returned course')} ${returned === 1 ? 'needs' : 'need'} your attention.`;
}

export default function CreatorHomePage() {
  const { user } = useAuth();
  const query = useTrainerQuery(loadCreatorOverview);
  const courses = query.data || [];
  const returned = courses.filter((course) => course.status === 'Rejected');
  const firstName = user?.fullName?.split(' ')[0] || 'Creator';

  return <section className="creator-page">
    <CreatorHeader eyebrow="Creator workspace" title={`Welcome back, ${firstName}.`} action={<Link className="btn btn-primary" to="/creator/courses/new">+ Create Course</Link>}>
      {query.data ? attentionSummary(returned.length) : 'Create Courses, submit them for Admin approval, then open classes for accepted Trainers.'}
    </CreatorHeader>
    {query.loading ? <div className="creator-empty" role="status">Loading your workspace…</div>
      : query.error ? <CreatorError error={query.error} onRetry={query.refresh} />
        : <div className="creator-home-layout">
          <div className="creator-home-main">
            <section aria-labelledby="creator-pipeline-title">
              <div className="creator-home-heading"><h2 id="creator-pipeline-title">Course pipeline</h2><Link to="/creator/courses">All Courses →</Link></div>
              <ol className="creator-pipeline" aria-label="Course pipeline">
                {PIPELINE.map(([status, label, caption]) => <li key={status} data-stage={status.toLowerCase()}>
                  <span className="creator-stage-count">{courses.filter((course) => course.status === status).length}</span>
                  <strong>{label}</strong>
                  <span>{caption}</span>
                </li>)}
              </ol>
              {returned.length ? <div className="creator-returned">
                <strong>{formatCount(returned.length, 'course')} returned by Admin</strong>
                <p>Open it to read the feedback, edit, then submit again.</p>
                <ul>{returned.map((course) => <li key={course.id}><Link to={`/creator/courses/${course.id}`}>{course.code} · {course.title} →</Link></li>)}</ul>
              </div> : null}
            </section>
            <section aria-labelledby="creator-classes-title">
              <div className="creator-home-heading"><h2 id="creator-classes-title">Classes</h2><Link to="/creator/intakes">All classes →</Link></div>
              <p>Open a class on a published course and assign a Trainer who has accepted it.</p>
            </section>
          </div>
          <section className="creator-quick-actions" aria-labelledby="creator-actions-title">
            <h2 id="creator-actions-title">Quick actions</h2>
            {QUICK_ACTIONS.map(([to, label, desc]) => <Link key={to} to={to}><strong>{label}</strong><span>{desc}</span></Link>)}
          </section>
        </div>}
  </section>;
}
