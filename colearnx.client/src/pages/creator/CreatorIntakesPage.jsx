import { useEffect } from 'react';
import { Link } from 'react-router-dom';
import { creatorCoursesApi } from '../../api';
import { creatorIntakesApi } from '../../api/creatorIntakes';
import useTrainerQuery from '../trainer/useTrainerQuery';
import { formatDate, statusLabel } from '../trainer/intakeForm';
import { RowArrow } from '../trainer/TrainerUi';
import { CreatorError, CreatorHeader } from './CreatorUi';
import { invitationOf, isUnpublished } from './intakeInvitation';

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

export default function CreatorIntakesPage({ course, onLoaded }) {
  const embedded = Boolean(course);
  const query = useTrainerQuery(embedded ? loadCourseIntakes : loadIntakes, embedded ? String(course.id) : '');
  const courses = new Map((query.data?.courses || []).map((item) => [item.id, item]));
  if (course) courses.set(course.id, course);
  const intakes = query.data?.intakes || [];
  const loaded = query.data?.intakes;
  // The course page reads the intake list to tell the Creator what to do next.
  useEffect(() => { if (loaded) onLoaded?.(loaded); }, [loaded, onLoaded]);
  const openClass = <Link className="btn btn-primary" to={embedded ? `/creator/courses/${course.id}/intakes/new` : '/creator/intakes/new'}>Create an intake</Link>;
  const body = <>
    <CreatorHeader eyebrow="Intakes" title="Intakes" action={<div className="trainer-actions"><button type="button" className="btn btn-ghost" onClick={query.refresh} disabled={query.loading}>Refresh</button>{openClass}</div>}>
      Create an intake with its full schedule, invite a trainer to it, and publish once the trainer accepts.
    </CreatorHeader>
    {query.loading ? <div className="creator-empty" role="status">Loading intakes…</div>
      : query.error ? <CreatorError error={query.error} onRetry={query.refresh} />
        : intakes.length === 0 ? <div className="creator-empty"><strong>No intakes yet</strong><p>Create an intake and schedule its sessions.</p></div>
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
              {isUnpublished(intake) ? <span className={`creator-status ${invitationOf(intake).tone}`}>{invitationOf(intake).label}</span> : null}
              <RowArrow />
            </article>;
          })}</div>}
  </>;
  return embedded ? <div className="creator-course-classes" id="course-classes">{body}</div> : <section className="creator-page">{body}</section>;
}
