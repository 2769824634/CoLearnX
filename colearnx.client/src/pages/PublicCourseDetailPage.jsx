import { useEffect, useState } from 'react';
import { Navigate, useNavigate, useParams } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { resolveAuthSession, SessionLoading } from '../auth/authSession';
import PublicSiteShell, { useGuestGate } from '../components/PublicSiteShell';
import AuthRequiredModal from '../components/AuthRequiredModal';
import { coursesApi } from '../api';
import { loadOnce, peek } from '../api/readCache';
import { formatCount, formatUtcRange, userFacingError } from './businessPresentation';
import { mapPublicCourse } from './publicCourses';

export default function PublicCourseDetailPage() {
  const { courseId } = useParams();
  const navigate = useNavigate();
  const auth = useAuth();
  const session = resolveAuthSession(auth);
  const gate = useGuestGate();
  const detailKey = `course:${auth.token || 'guest'}:${courseId}`;
  const cached = peek(detailKey);
  const [course, setCourse] = useState(() => (cached ? mapPublicCourse(cached) : null));
  const [error, setError] = useState('');

  useEffect(() => {
    const hit = peek(detailKey);
    if (hit) {
      setCourse(mapPublicCourse(hit));
      setError('');
      return undefined;
    }
    let active = true;
    setCourse(null);
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
      <PublicSiteShell title="Course">
        {error ? (
          <div role="alert" className="callout warn">
            {error}
            <button type="button" className="btn btn-ghost" onClick={() => navigate('/courses')}>Back to catalog</button>
          </div>
        ) : <p className="page-sub" role="status">Loading course detail…</p>}
      </PublicSiteShell>
    );
  }

  const sessions = course.sessions || [];

  return (
    <PublicSiteShell title={`${course.code} — ${course.title}`}>
      <div className="grid-2-1">
        <div>
          <section className="card member-course-overview" style={{ marginBottom: 16 }}>
            <div className="card-header">About this course</div>
            <div className="card-body">
              <p>{course.description || 'A course description has not been provided yet.'}</p>
              <div className="btn-row">
                <span className="pill neutral">{course.level || 'Level not specified'}</span>
              </div>
            </div>
          </section>
          <div className="card" style={{ marginBottom: 12 }}>
            <div className="card-header">Learning Outcomes</div>
            <div className="card-body" style={{ fontSize: 13, lineHeight: 1.8 }}>
              {!(course.outcomes || []).length ? <p>Learning outcomes have not been provided yet.</p> : null}
              {(course.outcomes || []).map((item) => <div key={item}>• {item}</div>)}
            </div>
          </div>
          <div className="card">
            <div className="card-header">Training Sessions</div>
            <div className="card-body">
              {sessions.length === 0 ? (
                <p className="page-sub" style={{ margin: 0 }}>This course has no published session yet.</p>
              ) : sessions.map((session) => (
                <div key={session.id} className="session-option member-session-option">
                  <strong>{session.label}</strong>
                  {session.startsAt && session.endsAt ? ` · ${formatUtcRange(session.startsAt, session.endsAt)}` : ''}
                </div>
              ))}
            </div>
          </div>
        </div>
        <div>
          <div className="card" style={{ marginBottom: 12 }}>
            <div className="card-header">Course team</div>
            <div className="card-body">
              <p>Creator</p><strong>{course.creatorName || 'Creator not specified'}</strong>
              <p>Trainer</p>
              {course.trainerNames?.length ? course.trainerNames.map((name) => <div key={name}>{name}</div>) : <p>A Trainer is assigned when a class opens.</p>}
            </div>
          </div>
          <div className="card" style={{ marginBottom: 12 }}>
            <div className="card-header">Course Fee</div>
            <div className="card-body">
              <strong style={{ color: 'var(--purple)', fontSize: 18 }}>{formatCount(course.credits, 'credit')}</strong>
            </div>
          </div>
          <button type="button" className="btn btn-ghost btn-block" onClick={gate.openAuth}>+ Add to Wishlist</button>
        </div>
      </div>
      <div className="enrol-bar">
        <span className="credits">{formatCount(course.credits, 'credit')}</span>
        <span style={{ fontSize: 13, color: 'var(--slate)' }}>{course.code}</span>
        <span style={{ flex: 1 }} />
        <button type="button" className="btn btn-primary" onClick={gate.openAuth}>Reserve place</button>
      </div>
      <AuthRequiredModal open={gate.authOpen} onClose={gate.closeAuth} />
    </PublicSiteShell>
  );
}
