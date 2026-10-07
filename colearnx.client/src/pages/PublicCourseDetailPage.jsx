import { useEffect, useState } from 'react';
import { Link, Navigate, useParams } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { SessionLoading } from '../auth/authSession';
import { resolveAuthSession } from '../auth/resolveAuthSession';
import PublicSiteShell from '../components/PublicSiteShell';
import { useGuestGate } from '../components/useGuestGate';
import AuthRequiredModal from '../components/AuthRequiredModal';
import { coursesApi } from '../api';
import { loadOnce, peek } from '../api/readCache';
import { formatCount, formatUtcRange, userFacingError } from './businessPresentation';
import { mapPublicCourse } from './publicCourses';
import { Arrow, CourseArt } from './guest/GuestCourseCard';
import Reveal from './guest/Reveal';

function BackLink() {
  return <Link className="g-back" to="/courses"><span aria-hidden="true">←</span> All programs</Link>;
}

export default function PublicCourseDetailPage() {
  const { courseId } = useParams();
  const auth = useAuth();
  const session = resolveAuthSession(auth);
  const gate = useGuestGate();
  const detailKey = `course:${auth.token || 'guest'}:${courseId}`;
  const [trackedKey, setTrackedKey] = useState(detailKey);
  const [course, setCourse] = useState(() => {
    const hit = peek(detailKey);
    return hit ? mapPublicCourse(hit) : null;
  });
  const [error, setError] = useState('');

  if (trackedKey !== detailKey) {
    const hit = peek(detailKey);
    setTrackedKey(detailKey);
    setCourse(hit ? mapPublicCourse(hit) : null);
    setError('');
  }

  useEffect(() => {
    if (peek(detailKey)) return undefined;
    let active = true;
    loadOnce(detailKey, () => coursesApi.get(Number(courseId)))
      .then((item) => { if (active) setCourse(mapPublicCourse(item)); })
      .catch((reason) => { if (active) setError(userFacingError(reason, 'Could not load this course.')); });
    return () => { active = false; };
  }, [courseId, detailKey]);

  if (session === 'booting') return <SessionLoading />;
  if (session === 'member') {
    return <Navigate to={`/member/courses/${courseId}`} replace />;
  }

  if (!course) {
    return (
      <PublicSiteShell>
        <div className="g-detail-state">
          <BackLink />
          {error ? (
            <div className="g-callout" role="alert">
              <p>{error}</p>
              <Link className="g-btn g-btn-ink" to="/courses">Back to catalog</Link>
            </div>
          ) : (
            <div className="g-detail-loading" role="status">
              <span className="g-sr">Loading course detail…</span>
              <i /><i /><i />
            </div>
          )}
        </div>
      </PublicSiteShell>
    );
  }

  const sessions = course.sessions || [];
  const outcomes = course.outcomes || [];
  const fee = formatCount(course.credits, 'credit');

  return (
    <PublicSiteShell>
      <article className="g-detail">
        <header className="g-detail-hero">
          <div className="g-detail-copy">
            <BackLink />
            <p className="g-kicker g-load" style={{ '--i': 0 }}>{course.code} · {course.level || 'All levels'}</p>
            <h1 className="g-detail-title g-load" style={{ '--i': 1 }}>{course.title}</h1>
            <p className="g-detail-people g-load" style={{ '--i': 2 }}>
              {course.creatorName ? <>Created by <strong>{course.creatorName}</strong></> : 'Creator not specified'}
              {course.trainerNames?.length ? <> · Taught by <strong>{course.trainerNames.join(', ')}</strong></> : null}
            </p>
          </div>
          <div className="g-detail-art g-load" style={{ '--i': 2 }}>
            <CourseArt code={course.code} large />
          </div>
        </header>

        <div className="g-detail-body">
          <div className="g-detail-main">
            <Reveal as="section" className="g-detail-section" aria-labelledby="g-about">
              <h2 id="g-about">About this course</h2>
              <p className="g-detail-text">{course.description || 'A course description has not been provided yet.'}</p>
            </Reveal>

            <Reveal as="section" className="g-detail-section" aria-labelledby="g-outcomes">
              <h2 id="g-outcomes">What you&rsquo;ll learn</h2>
              {outcomes.length ? (
                <ol className="g-outcomes">
                  {outcomes.map((item) => <li key={item}>{item}</li>)}
                </ol>
              ) : <p className="g-muted">Learning outcomes have not been provided yet.</p>}
            </Reveal>

            <Reveal as="section" className="g-detail-section" aria-labelledby="g-schedule">
              <h2 id="g-schedule">Schedule</h2>
              {sessions.length ? (
                <ol className="g-timeline">
                  {sessions.map((item) => (
                    <li key={item.id}>
                      <strong>{item.label}</strong>
                      <span>{item.startsAt && item.endsAt ? formatUtcRange(item.startsAt, item.endsAt) : 'Time to be confirmed'}</span>
                    </li>
                  ))}
                </ol>
              ) : <p className="g-muted">No class is scheduled yet. Save it to your wishlist and we&rsquo;ll keep it close.</p>}
            </Reveal>

            <Reveal as="section" className="g-detail-section" aria-labelledby="g-team">
              <h2 id="g-team">Course team</h2>
              <dl className="g-team">
                <div><dt>Creator</dt><dd>{course.creatorName || 'Not specified'}</dd></div>
                <div><dt>Trainer</dt><dd>{course.trainerNames?.length ? course.trainerNames.join(', ') : 'Assigned when a class opens'}</dd></div>
              </dl>
            </Reveal>
          </div>

          <aside className="g-detail-aside" aria-label="Enrolment">
            <div className="g-price-card">
              <p className="g-price-label">Course fee</p>
              <p className="g-price"><strong>{course.credits ?? '—'}</strong> credits</p>
              <dl className="g-price-facts">
                <div><dt>Stage</dt><dd>{course.level || 'All levels'}</dd></div>
                <div><dt>Scheduled sessions</dt><dd>{sessions.length || 'None yet'}</dd></div>
              </dl>
              <button type="button" className="g-btn g-btn-primary g-btn-lg g-btn-block" onClick={gate.openAuth}>Reserve place <Arrow /></button>
              <button type="button" className="g-btn g-btn-quiet g-btn-block" onClick={gate.openAuth}>+ Add to Wishlist</button>
              <p className="g-price-note">Credits are held when you reserve a place. Sign in or create a free account to continue.</p>
            </div>
          </aside>
        </div>
      </article>

      <div className="g-mobile-bar">
        <span><strong>{fee}</strong><small>{course.code}</small></span>
        <button type="button" className="g-btn g-btn-primary" onClick={gate.openAuth}>Reserve place</button>
      </div>
      <AuthRequiredModal open={gate.authOpen} onClose={gate.closeAuth} />
    </PublicSiteShell>
  );
}
