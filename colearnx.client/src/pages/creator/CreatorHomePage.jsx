import { Link } from 'react-router-dom';
import { creatorCoursesApi } from '../../api';
import { creatorIntakeApplicationsApi } from '../../api/creatorIntakeApplications';
import { useAuth } from '../../auth/AuthContext';
import useTrainerQuery from '../trainer/useTrainerQuery';
import { formatCount } from '../businessPresentation';
import { CreatorApplicationRows, CreatorError, CreatorHeader } from './CreatorUi';

const PIPELINE = [
  ['Draft', 'Drafts', 'Edit and attach materials'],
  ['PendingApproval', 'In Admin review', 'Waiting for a decision'],
  ['Published', 'Published', 'Open in the catalogue'],
];

const QUICK_ACTIONS = [
  ['/creator/upload', 'Upload Material', 'PDF, PPTX, DOCX or images for a Course.'],
  ['/creator/usage', 'Usage Records', 'Where your materials have been used.'],
];

async function loadCreatorOverview(token, key, signal) {
  const [courses, applications] = await Promise.all([
    creatorCoursesApi.list(token, signal),
    creatorIntakeApplicationsApi.list(token, signal),
  ]);
  return { courses, applications };
}

function attentionSummary(approvals, returned) {
  const items = [
    approvals ? formatCount(approvals, 'session approval') : null,
    returned ? formatCount(returned, 'returned course') : null,
  ].filter(Boolean);
  if (!items.length) return 'Nothing needs your attention right now.';
  return `${items.join(' and ')} ${approvals + returned === 1 ? 'needs' : 'need'} your attention.`;
}

export default function CreatorHomePage() {
  const { user } = useAuth();
  const query = useTrainerQuery(loadCreatorOverview);
  const courses = query.data?.courses || [];
  const pending = (query.data?.applications || []).filter((item) => item.status === 'Pending');
  const returned = courses.filter((course) => course.status === 'Rejected');
  const firstName = user?.fullName?.split(' ')[0] || 'Creator';

  return <section className="creator-page">
    <CreatorHeader eyebrow="Creator workspace" title={`Welcome back, ${firstName}.`} action={<Link className="btn btn-primary" to="/creator/courses/new">+ Create Course</Link>}>
      {query.data ? attentionSummary(pending.length, returned.length) : 'Create Courses, submit them for Admin approval, and upload materials for the Trainer library.'}
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
            <section aria-labelledby="creator-approvals-title">
              <div className="creator-home-heading"><h2 id="creator-approvals-title">Waiting for your approval</h2><Link to="/creator/courses/intake-applications">All session approvals →</Link></div>
              <CreatorApplicationRows applications={pending.slice(0, 3)} />
            </section>
          </div>
          <section className="creator-quick-actions" aria-labelledby="creator-actions-title">
            <h2 id="creator-actions-title">Quick actions</h2>
            {QUICK_ACTIONS.map(([to, label, desc]) => <Link key={to} to={to}><strong>{label}</strong><span>{desc}</span></Link>)}
          </section>
        </div>}
  </section>;
}
