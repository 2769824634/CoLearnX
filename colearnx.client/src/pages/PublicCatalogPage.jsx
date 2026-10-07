import { useMemo, useState } from 'react';
import { Navigate, useNavigate, useSearchParams } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { SessionLoading } from '../auth/authSession';
import { resolveAuthSession } from '../auth/resolveAuthSession';
import PublicSiteShell from '../components/PublicSiteShell';
import { useGuestGate } from '../components/useGuestGate';
import AuthRequiredModal from '../components/AuthRequiredModal';
import { usePublicCourses } from './publicCourses';
import GuestCourseCard, { GuestCardSkeleton } from './guest/GuestCourseCard';
import { GUEST_LEVELS } from './guest/guestContent';

export default function PublicCatalogPage() {
  const navigate = useNavigate();
  const [params, setParams] = useSearchParams();
  const { courses, loading, error } = usePublicCourses();
  const gate = useGuestGate();
  const session = resolveAuthSession(useAuth());
  const search = params.get('q') || '';
  const [topic, setTopic] = useState('');
  const [level, setLevel] = useState('');
  const topics = useMemo(
    () => [...new Set(courses.map((course) => course.category).filter(Boolean))].sort(),
    [courses],
  );

  const list = useMemo(() => courses.filter((course) => {
    const query = search.toLowerCase();
    if (query && !(`${course.code} ${course.title} ${course.creatorName || ''} ${(course.trainerNames || []).join(' ')}`).toLowerCase().includes(query)) return false;
    if (topic && course.topic !== topic && course.category !== topic) return false;
    if (level && course.level !== level) return false;
    return true;
  }), [courses, search, topic, level]);

  function setSearch(value) {
    const next = new URLSearchParams(params);
    if (value) next.set('q', value);
    else next.delete('q');
    setParams(next, { replace: true });
  }

  function resetFilters() {
    setSearch('');
    setTopic('');
    setLevel('');
  }

  if (session === 'booting') return <SessionLoading />;
  if (session === 'member') {
    const query = params.toString();
    return <Navigate to={`/member/courses${query ? `?${query}` : ''}`} replace />;
  }

  const filtered = Boolean(search || topic || level);

  return (
    <PublicSiteShell>
      <header className="g-page-head">
        <p className="g-kicker g-load" style={{ '--i': 0 }}>Catalog</p>
        <h1 className="g-page-h1 g-load" style={{ '--i': 1 }}>Program Catalog</h1>
        <p className="g-page-lede g-load" style={{ '--i': 2 }}>
          Scheduled classes with real trainers. Pick a stage that fits you, see the dates, and reserve a place with credits.
        </p>
      </header>

      <div className="g-filters g-load" style={{ '--i': 3 }}>
        <label className="g-field g-field-search">
          <span className="g-sr">Search programs</span>
          <svg viewBox="0 0 20 20" aria-hidden="true" focusable="false"><circle cx="9" cy="9" r="5.5" fill="none" stroke="currentColor" strokeWidth="1.7" /><path d="m13.2 13.2 3.8 3.8" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" /></svg>
          <input type="search" placeholder="Search by title, code, trainer..." value={search} onChange={(event) => setSearch(event.target.value)} />
        </label>
        <div className="g-segment" role="group" aria-label="Stage">
          {['', ...GUEST_LEVELS].map((value) => (
            <button key={value || 'all'} type="button" aria-pressed={level === value} onClick={() => setLevel(value)}>
              {value || 'All stages'}
            </button>
          ))}
        </div>
        <label className="g-field g-field-select">
          <span className="g-sr">Topic</span>
          <select value={topic} onChange={(event) => setTopic(event.target.value)}>
            <option value="">All topics</option>
            {topics.map((item) => <option key={item}>{item}</option>)}
          </select>
        </label>
      </div>

      <p className="g-result-count" aria-live="polite">
        {loading ? 'Loading programs…' : `${list.length} ${list.length === 1 ? 'program' : 'programs'}${filtered ? ' match your filters' : ' open'}`}
      </p>

      {error ? <p className="g-callout" role="alert">{error}</p> : null}
      <div className="g-grid g-grid-catalog">
        {loading ? <GuestCardSkeleton count={8} /> : list.map((course, index) => (
          <GuestCourseCard
            key={course.id}
            course={course}
            index={index}
            onOpen={() => navigate(`/courses/${course.id}`)}
            onWishlist={gate.openAuth}
          />
        ))}
      </div>
      {!loading && !error && !list.length ? (
        <div className="g-empty">
          <p className="g-empty-mark" aria-hidden="true">?</p>
          <h2>No programs match yet.</h2>
          <p>Try another stage or a shorter search term.</p>
          {filtered ? <button type="button" className="g-btn g-btn-ink" onClick={resetFilters}>Clear filters</button> : null}
        </div>
      ) : null}
      <AuthRequiredModal open={gate.authOpen} onClose={gate.closeAuth} />
    </PublicSiteShell>
  );
}
