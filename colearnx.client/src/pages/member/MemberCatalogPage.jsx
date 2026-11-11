import { useMemo, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import MemberShell from '../../components/MemberShell';
import { useMemberSlices } from './memberDataState';
import { learningPathOptions } from '../publicCourses';
import { CourseArt } from '../guest/GuestCourseCard';

function SearchIcon() {
  return (
    <svg viewBox="0 0 20 20" aria-hidden="true" focusable="false">
      <circle cx="9" cy="9" r="5.5" fill="none" stroke="currentColor" strokeWidth="1.7" />
      <path d="m13.2 13.2 3.8 3.8" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" />
    </svg>
  );
}

// Program catalog + filters.
export default function MemberCatalogPage() {
  const navigate = useNavigate();
  const [params] = useSearchParams();
  const { state, toggleWish, reload } = useMemberSlices('catalog');
  const [tab, setTab] = useState('all');
  const [search, setSearch] = useState(params.get('q') || '');
  const [learningPath, setLearningPath] = useState('');
  const [level, setLevel] = useState('');
  const [credits, setCredits] = useState('');

  const list = useMemo(() => {
    return state.courses.filter((c) => {
      if (tab === 'wishlist' && !state.wishlist.includes(c.id)) return false;
      const q = search.toLowerCase();
      if (q && !(`${c.code} ${c.title} ${c.creatorName || ''} ${(c.trainerNames || []).join(' ')}`).toLowerCase().includes(q)) return false;
      if (learningPath && c.learningPath !== learningPath) return false;
      if (level && c.level !== level) return false;
      if (credits === 'low' && c.credits > 25) return false;
      if (credits === 'high' && c.credits < 30) return false;
      return true;
    });
  }, [state.courses, state.wishlist, tab, search, learningPath, level, credits]);
  const learningPaths = useMemo(() => learningPathOptions(state.courses), [state.courses]);
  const ready = !state.loading && !state.loadError;

  return (
    <MemberShell
      title="Program Catalog"
      onSearch={(q) => setSearch(q)}
    >
      <div className="cx-catalog">
        <div className="cx-filters">
          <label className="cx-search">
            <SearchIcon />
            <input
              type="search"
              placeholder="Search by title, code, trainer..."
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
          </label>
          <select aria-label="Learning path" value={learningPath} onChange={(e) => setLearningPath(e.target.value)}>
            <option value="">All learning paths</option>
            {learningPaths.map((path) => <option key={path}>{path}</option>)}
          </select>
          <select aria-label="Stage" value={level} onChange={(e) => setLevel(e.target.value)}>
            <option value="">All stages</option>
            <option value="Beginner">Foundation</option>
            <option>Intermediate</option>
            <option>Advanced</option>
          </select>
          <select value={credits} onChange={(e) => setCredits(e.target.value)}>
            <option value="">Any Credits</option>
            <option value="low">≤ 25 credits</option>
            <option value="high">≥ 30 credits</option>
          </select>
        </div>

        <div className="cx-tabs">
          <button type="button" className={`cx-tab${tab === 'all' ? ' active' : ''}`} onClick={() => setTab('all')}>
            All Programs <span className="cx-tab-count">{state.courses.length}</span>
          </button>
          <button type="button" className={`cx-tab${tab === 'wishlist' ? ' active' : ''}`} onClick={() => setTab('wishlist')}>
            Wishlist <span className="cx-tab-count">{state.wishlist.length}</span>
          </button>
          {ready && list.length ? <p className="cx-result-count" aria-live="polite">{list.length} {list.length === 1 ? 'result' : 'results'}</p> : null}
        </div>

        <div className="cx-grid">
          {state.loading ? <p role="status">Loading programs…</p> : state.loadError ? <div className="callout warn" role="alert">{state.loadError} <button type="button" className="btn btn-ghost" onClick={reload}>Retry</button></div> : list.length === 0 ? <div className="callout"><p>{state.courses.length || search || learningPath || level || credits || tab === 'wishlist' ? 'No programs match your filters.' : 'No programs are published yet.'}</p><button type="button" className="btn btn-ghost" onClick={() => { setSearch(''); setLearningPath(''); setLevel(''); setCredits(''); setTab('all'); }}>Clear filters</button></div> : list.map((c) => {
            const saved = state.wishlist.includes(c.id);
            const open = () => navigate(`/member/courses/${c.id}`);
            return (
              <article className="cx-card" key={c.id}>
                <button type="button" className="cx-card-art" tabIndex={-1} aria-hidden="true" onClick={open}>
                  <CourseArt code={c.code} path={c.learningPath} />
                </button>
                <div className="cx-card-body">
                  <div className="cx-card-meta">
                    <span className="cx-chip">{c.level}</span>
                    <span className="cx-card-credits"><strong>{c.credits}</strong> Credits</span>
                  </div>
                  <h4 className="cx-card-title">{c.title}</h4>
                  <p className="cx-card-people">{c.trainerNames?.length ? <>Trainer <b>{c.trainerNames.join(', ')}</b></> : 'Trainer assigned when a class opens'}</p>
                  {c.creatorName ? <p className="cx-card-people">Creator <b>{c.creatorName}</b></p> : null}
                  <p className="cx-card-rating">
                    {c.ratingCount > 0 && c.averageStars != null
                      ? <><span className="cx-star" aria-hidden="true">★</span> <b>{Number(c.averageStars).toFixed(1)}</b> ({c.ratingCount} {c.ratingCount === 1 ? 'rating' : 'ratings'})</>
                      : 'New · No ratings yet'}
                  </p>
                  <div className="cx-card-actions">
                    <button type="button" className="btn btn-primary btn-sm" onClick={open}>
                      View Details
                    </button>
                    <button type="button" className={`btn btn-ghost btn-sm cx-wish${saved ? ' is-saved' : ''}`} onClick={() => toggleWish(c.id)}>
                      {saved ? '♥ Saved' : '+ Wishlist'}
                    </button>
                  </div>
                </div>
              </article>
            );
          })}
        </div>
      </div>
    </MemberShell>
  );
}
