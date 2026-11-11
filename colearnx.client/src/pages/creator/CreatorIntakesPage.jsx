import { Link } from 'react-router-dom';
import { creatorCoursesApi } from '../../api';
import { creatorIntakesApi } from '../../api/creatorIntakes';
import useTrainerQuery from '../trainer/useTrainerQuery';
import { formatDate, statusLabel } from '../trainer/intakeForm';
import { RowArrow } from '../trainer/TrainerUi';
import { CreatorError, CreatorHeader } from './CreatorUi';

async function loadIntakes(token, key, signal) {
  const [intakes, courses] = await Promise.all([
    creatorIntakesApi.list(token, null, signal),
    creatorCoursesApi.list(token, signal),
  ]);
  return { intakes, courses };
}

async function loadCourseIntakes(token, courseId, signal) {
  const intakes = await creatorIntakesApi.list(token, courseId, signal);
  return { intakes: Array.isArray(intakes) ? intakes : [] };
}

export default function CreatorIntakesPage({ course }) {
  const embedded = Boolean(course);
  const query = useTrainerQuery(embedded ? loadCourseIntakes : loadIntakes, embedded ? String(course.id) : '');
  const courses = new Map((query.data?.courses || []).map((item) => [item.id, item]));
  if (course) courses.set(course.id, course);
  const intakes = query.data?.intakes || [];
  const openClass = <Link className="btn btn-primary" to={embedded ? `/creator/courses/${course.id}/intakes/new` : '/creator/intakes/new'}>Open a class</Link>;
  const body = <>
    <CreatorHeader eyebrow="Classes" title="Classes you open" action={<div className="trainer-actions"><button type="button" className="btn btn-ghost" onClick={query.refresh} disabled={query.loading}>Refresh</button>{openClass}</div>}>
      Create a class on a published course, assign a Trainer who has accepted it, then publish the schedule.
    </CreatorHeader>
    {query.loading ? <div className="creator-empty" role="status">Loading classes…</div>
      : query.error ? <CreatorError error={query.error} onRetry={query.refresh} />
        : intakes.length === 0 ? <div className="creator-empty"><strong>No classes yet</strong><p>Open a class after a Trainer accepts the course.</p></div>
          : <div className="creator-application-list">{intakes.map((intake) => {
            const item = courses.get(intake.courseId);
            return <article className="creator-application-row creator-row-link" key={intake.id}>
              <div className="creator-application-index">{formatDate(intake.startsAt).slice(0, 11)}</div>
              <div>
                <p className="creator-eyebrow">{item?.code || 'Course'}</p>
                <h2><Link to={embedded ? `/creator/courses/${course.id}/intakes/${intake.id}` : `/creator/intakes/${intake.id}`}>{item?.title || item?.code || 'Course'}</Link></h2>
                <p>{formatDate(intake.startsAt)} → {formatDate(intake.endsAt)}</p>
              </div>
              <span className={`creator-status ${(intake.status || '').toLowerCase()}`}>{statusLabel(intake.status)}</span>
              <RowArrow />
            </article>;
          })}</div>}
  </>;
  return embedded ? <div className="creator-course-classes">{body}</div> : <section className="creator-page">{body}</section>;
}
