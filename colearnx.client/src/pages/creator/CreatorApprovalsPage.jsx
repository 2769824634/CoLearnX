import { Link } from 'react-router-dom';
import { creatorCoursesApi } from '../../api';
import useTrainerQuery from '../trainer/useTrainerQuery';
import { CreatorError, CreatorHeader } from './CreatorUi';
import { formatCount } from '../businessPresentation';

const loadCourses = (token, key, signal) => creatorCoursesApi.list(token, signal);

const GROUPS = [
  { status: 'Rejected', title: 'Needs changes', empty: 'Admin has not returned any Courses.' },
  { status: 'PendingApproval', title: 'Waiting for Admin', empty: 'No Courses are waiting for review.' },
  { status: 'Published', title: 'Published', empty: 'No Courses are published yet.' },
];

function CourseGroup({ title, empty, courses }) {
  const headingId = `creator-approvals-${title.replace(/\W+/g, '-').toLowerCase()}`;
  return <section aria-labelledby={headingId}>
    <div className="creator-section-heading"><h2 id={headingId}>{title}</h2><span className="creator-group-count">{courses.length}</span></div>
    {!courses.length ? <p className="creator-group-empty">{empty}</p> : <div className="creator-application-list">{courses.map((course) => <article className="creator-application-row" key={course.id}>
      <div className="creator-application-index">{course.code}</div>
      <div>
        <p className="creator-eyebrow">{course.learningPathName} · {course.courseLevelName}</p><h2>{course.title}</h2>
        {course.status === 'Rejected' ? <div className="creator-feedback"><strong>Admin feedback</strong><p>{course.reviewReason || 'No feedback was recorded.'}</p></div> : <p>{formatCount(course.creditCost, 'credit')}</p>}
      </div>
      <span className={`creator-status ${course.status.toLowerCase()}`}>{course.status === 'PendingApproval' ? 'Pending approval' : course.status}</span>
      <Link to={`/creator/courses/${course.id}`}>{course.status === 'Rejected' ? 'Edit and resubmit →' : 'Open →'}</Link>
    </article>)}</div>}
  </section>;
}

export default function CreatorApprovalsPage() {
  const query = useTrainerQuery(loadCourses);
  const courses = query.data || [];
  const submitted = courses.filter((course) => GROUPS.some((group) => group.status === course.status));
  return <section className="creator-page">
    <CreatorHeader eyebrow="Course review" title="Approval status" action={<button type="button" className="btn btn-ghost" onClick={query.refresh} disabled={query.loading}>Refresh</button>}>
      Follow each submitted Course through Admin review. Returned Courses show the Admin feedback to address before you resubmit.
    </CreatorHeader>
    {query.loading ? <div className="creator-empty" role="status">Loading approval status…</div>
      : query.error ? <CreatorError error={query.error} onRetry={query.refresh} />
        : !submitted.length ? <div className="creator-empty"><strong>No Courses submitted yet</strong><p>Submit a Course draft for Admin approval to track it here.</p></div>
          : GROUPS.map((group) => <CourseGroup key={group.status} title={group.title} empty={group.empty} courses={submitted.filter((course) => course.status === group.status)} />)}
  </section>;
}
