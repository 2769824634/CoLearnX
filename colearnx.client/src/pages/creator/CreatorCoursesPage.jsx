import { Link } from 'react-router-dom';
import { creatorCoursesApi } from '../../api';
import useTrainerQuery from '../trainer/useTrainerQuery';
import { CreatorError, CreatorHeader } from './CreatorUi';
import { formatCount } from '../businessPresentation';

const loadCourses = (token, key, signal) => creatorCoursesApi.list(token, signal);

export default function CreatorCoursesPage() {
  const query = useTrainerQuery(loadCourses);
  const courses = query.data || [];

  return <section className="creator-page">
    <CreatorHeader eyebrow="Course management" title="Course workspace" action={<Link className="btn btn-primary" to="/creator/courses/new">Create Course</Link>}>
      Create Course drafts, submit them for Admin approval, and manage the Courses you own.
    </CreatorHeader>
    <Link className="creator-review-card" to="/creator/intakes">
      <span className="creator-eyebrow">Classes</span>
      <strong>Open and publish classes</strong>
      <p>Assign an accepted Trainer, set the schedule, and publish the class yourself.</p>
      <span>Open classes →</span>
    </Link>
    <div className="creator-section-heading"><div><p className="creator-eyebrow">Your catalogue</p><h2>Courses</h2></div></div>
    {query.loading ? <div className="creator-empty" role="status">Loading Courses…</div>
      : query.error ? <CreatorError error={query.error} onRetry={query.refresh} />
        : courses.length === 0 ? <div className="creator-empty"><strong>No Courses yet</strong><p>Create a draft to begin.</p></div>
          : <div className="creator-application-list">{courses.map((course) => <Link className="creator-application-row creator-course-row" key={course.id} to={`/creator/courses/${course.id}`}>
            <div className="creator-application-index">{course.code}</div>
            <div><p className="creator-eyebrow">{course.learningPathName} · {course.courseLevelName}</p><h2>{course.title}</h2><p>{formatCount(course.creditCost, 'credit')}</p></div>
            <span className={`creator-status ${course.status.toLowerCase()}`}>{course.status}</span>
          </Link>)}</div>}
  </section>;
}

