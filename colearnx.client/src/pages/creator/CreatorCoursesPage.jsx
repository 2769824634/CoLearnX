import { Link } from 'react-router-dom';
import { creatorCoursesApi } from '../../api';
import useTrainerQuery from '../trainer/useTrainerQuery';
import { CreatorError, CreatorHeader } from './CreatorUi';
import { formatCount } from '../businessPresentation';
import { statusLabel } from '../trainer/intakeForm';

const loadCourses = (token, key, signal) => creatorCoursesApi.list(token, signal);

export default function CreatorCoursesPage() {
  const query = useTrainerQuery(loadCourses);
  const courses = query.data || [];

  return <section className="creator-page">
    <CreatorHeader eyebrow="Course management" title="Course workspace" action={<div className="trainer-actions"><button type="button" className="btn btn-ghost" onClick={query.refresh} disabled={query.loading}>Refresh</button><Link className="btn btn-primary" to="/creator/courses/new">Create Course</Link></div>}>
      Create a course, then open it to upload materials, invite a Trainer, create an intake, and add sessions.
    </CreatorHeader>
    <div className="creator-section-heading"><div><p className="creator-eyebrow">Your catalogue</p><h2>Courses</h2></div></div>
    {query.loading ? <div className="creator-empty" role="status">Loading Courses…</div>
      : query.error ? <CreatorError error={query.error} onRetry={query.refresh} />
        : courses.length === 0 ? <div className="creator-empty"><strong>No Courses yet</strong><p>Create a draft to begin.</p></div>
          : <div className="creator-application-list">{courses.map((course) =>
            <Link className="creator-application-row creator-course-row" to={`/creator/courses/${course.id}`} key={course.id}>
              <div className="creator-application-index">{course.code}</div>
              <div><p className="creator-eyebrow">{[course.courseSubjectName, course.courseLevelName].filter(Boolean).join(' · ') || course.learningPathName}</p><h2>{course.title}</h2><p>{formatCount(course.creditCost, 'credit')}</p></div>
              <span className={`creator-status ${course.status.toLowerCase()}`}>{statusLabel(course.status)}</span>
            </Link>
          )}</div>}
  </section>;
}

