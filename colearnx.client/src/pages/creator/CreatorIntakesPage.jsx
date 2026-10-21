import { Link } from 'react-router-dom';
import { creatorCoursesApi } from '../../api';
import { creatorIntakesApi } from '../../api/creatorIntakes';
import useTrainerQuery from '../trainer/useTrainerQuery';
import { formatDate, statusLabel } from '../trainer/intakeForm';
import { CreatorError, CreatorHeader } from './CreatorUi';

async function loadIntakes(token, key, signal) {
  const [intakes, courses] = await Promise.all([
    creatorIntakesApi.list(token, null, signal),
    creatorCoursesApi.list(token, signal),
  ]);
  return { intakes, courses };
}

export default function CreatorIntakesPage() {
  const query = useTrainerQuery(loadIntakes);
  const courses = new Map((query.data?.courses || []).map((course) => [course.id, course]));
  const intakes = query.data?.intakes || [];
  return <section className="creator-page">
    <CreatorHeader eyebrow="Classes" title="Classes you open" action={<Link className="btn btn-primary" to="/creator/intakes/new">Open a class</Link>}>
      Create a class on a published course, assign a Trainer who has accepted it, then publish the schedule.
    </CreatorHeader>
    {query.loading ? <div className="creator-empty" role="status">Loading classes…</div>
      : query.error ? <CreatorError error={query.error} onRetry={query.refresh} />
        : intakes.length === 0 ? <div className="creator-empty"><strong>No classes yet</strong><p>Open a class after a Trainer accepts the course.</p></div>
          : <div className="creator-application-list">{intakes.map((intake) => {
            const course = courses.get(intake.courseId);
            return <article className="creator-application-row" key={intake.id}>
              <div className="creator-application-index">#{intake.id}</div>
              <div>
                <p className="creator-eyebrow">{course?.code || `Course #${intake.courseId}`}</p>
                <h2>{course?.title || `Course #${intake.courseId}`}</h2>
                <p>{formatDate(intake.startsAt)} → {formatDate(intake.endsAt)}</p>
              </div>
              <span className={`creator-status ${(intake.status || '').toLowerCase()}`}>{statusLabel(intake.status)}</span>
              <Link to={`/creator/intakes/${intake.id}`}>Open →</Link>
            </article>;
          })}</div>}
  </section>;
}
