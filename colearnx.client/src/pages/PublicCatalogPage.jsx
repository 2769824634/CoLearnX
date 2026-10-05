import { useMemo, useState } from 'react';
import { Navigate, useNavigate, useSearchParams } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { resolveAuthSession, SessionLoading } from '../auth/authSession';
import PublicSiteShell, { useGuestGate } from '../components/PublicSiteShell';
import AuthRequiredModal from '../components/AuthRequiredModal';
import { usePublicCourses } from './publicCourses';

export default function PublicCatalogPage() {
  const navigate = useNavigate();
  const [params] = useSearchParams();
  const { courses, loading, error } = usePublicCourses();
  const gate = useGuestGate();
  const session = resolveAuthSession(useAuth());
  const [search, setSearch] = useState(params.get('q') || '');
  const [topic, setTopic] = useState('');
  const [level, setLevel] = useState('');

  const list = useMemo(() => courses.filter((course) => {
    const query = search.toLowerCase();
    if (query && !(`${course.code} ${course.title} ${course.creatorName || ''} ${(course.trainerNames || []).join(' ')}`).toLowerCase().includes(query)) return false;
    if (topic && course.topic !== topic && course.category !== topic) return false;
    if (level && course.level !== level) return false;
    return true;
  }), [courses, search, topic, level]);

  if (session === 'booting') return <SessionLoading />;
  if (session === 'member') {
    const query = params.toString();
    return <Navigate to={`/member/courses${query ? `?${query}` : ''}`} replace />;
  }

  return (
    <PublicSiteShell title="Program Catalog">
      <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', marginBottom: 16, alignItems: 'center' }}>
        <input
          type="search"
          placeholder="Search by title, code, trainer..."
          style={{ flex: 1, minWidth: 200, padding: '8px 12px', border: '1px solid var(--border)', borderRadius: 6 }}
          value={search}
          onChange={(event) => setSearch(event.target.value)}
        />
        <select value={topic} onChange={(event) => setTopic(event.target.value)}>
          <option value="">All Topics</option>
          <option>Programming</option>
          <option>Design</option>
          <option>Business</option>
        </select>
        <select value={level} onChange={(event) => setLevel(event.target.value)}>
          <option value="">All Levels</option>
          <option>Beginner</option>
          <option>Intermediate</option>
          <option>Advanced</option>
        </select>
      </div>
      <div className="grid-4">
        {loading ? <p role="status">Loading programs…</p> : error ? <p className="callout warn" role="alert">{error}</p> : list.map((course) => (
          <div className="course-card" key={course.id}>
            <div className="thumb">{course.code}</div>
            <h4>{course.title}</h4>
            <div className="meta">{course.trainerNames?.length ? `Trainer: ${course.trainerNames.join(', ')}` : 'Trainer assigned when a class opens'}</div>
            {course.creatorName ? <div className="meta">Creator: {course.creatorName}</div> : null}
            <span className="pill">{course.credits} Credits</span>{' '}
            <span className="pill neutral">{course.level}</span>
            <div className="actions" style={{ marginTop: 10 }}>
              <button type="button" className="btn btn-primary btn-sm" style={{ flex: 1 }} onClick={() => navigate(`/courses/${course.id}`)}>
                View Details
              </button>
              <button type="button" className="btn btn-ghost btn-sm" onClick={gate.openAuth}>
                + Wishlist
              </button>
            </div>
          </div>
        ))}
      </div>
      <AuthRequiredModal open={gate.authOpen} onClose={gate.closeAuth} />
    </PublicSiteShell>
  );
}
