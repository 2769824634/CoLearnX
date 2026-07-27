import { useMemo, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import MemberShell from '../../components/MemberShell';
import { useMemberData } from './MemberDataContext';

// Program catalog + left filters. Shared: MemberCatalogPage
export default function MemberCatalogPage() {
  const navigate = useNavigate();
  const [params] = useSearchParams();
  const { state, showToast, toggleWish } = useMemberData();
  const [tab, setTab] = useState('all');
  const [search, setSearch] = useState(params.get('q') || '');
  const [topic, setTopic] = useState('');
  const [level, setLevel] = useState('');
  const [credits, setCredits] = useState('');

  const list = useMemo(() => {
    return state.courses.filter((c) => {
      if (tab === 'wishlist' && !state.wishlist.includes(c.id)) return false;
      const q = search.toLowerCase();
      if (q && !(`${c.code} ${c.title} ${c.trainer}`).toLowerCase().includes(q)) return false;
      if (topic && c.topic !== topic) return false;
      if (level && c.level !== level) return false;
      if (credits === 'low' && c.credits > 25) return false;
      if (credits === 'high' && c.credits < 30) return false;
      return true;
    });
  }, [state.courses, state.wishlist, tab, search, topic, level, credits]);

  return (
    <MemberShell
      title="Program Catalog"
      subtitle="MBR-02 · Search, filter and save programs to your wishlist"
      onSearch={(q) => setSearch(q)}
      onNotify={() => showToast('3 notifications')}
    >
      <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', marginBottom: 16, alignItems: 'center' }}>
        <input
          type="search"
          placeholder="Search by title, code, trainer..."
          style={{ flex: 1, minWidth: 200, padding: '8px 12px', border: '1px solid var(--border)', borderRadius: 6 }}
          value={search}
          onChange={(e) => setSearch(e.target.value)}
        />
        <select value={topic} onChange={(e) => setTopic(e.target.value)}>
          <option value="">All Topics</option>
          <option>Programming</option>
          <option>Design</option>
          <option>Business</option>
        </select>
        <select value={level} onChange={(e) => setLevel(e.target.value)}>
          <option value="">All Levels</option>
          <option>Beginner</option>
          <option>Intermediate</option>
          <option>Advanced</option>
        </select>
        <select value={credits} onChange={(e) => setCredits(e.target.value)}>
          <option value="">Any Credits</option>
          <option value="low">≤ 25 credits</option>
          <option value="high">≥ 30 credits</option>
        </select>
      </div>

      <div className="tabs">
        <button type="button" className={`tab${tab === 'all' ? ' active' : ''}`} onClick={() => setTab('all')}>
          All Programs ({state.courses.length})
        </button>
        <button type="button" className={`tab${tab === 'wishlist' ? ' active' : ''}`} onClick={() => setTab('wishlist')}>
          Wishlist ({state.wishlist.length})
        </button>
      </div>

      <div className="grid-4">
        {list.map((c) => (
          <div className="course-card" key={c.id}>
            <div className="thumb">{c.code}</div>
            <h4>{c.title}</h4>
            <div className="meta">Trainer: {c.trainer}</div>
            <span className="pill">{c.credits} Credits</span>{' '}
            <span className="pill neutral">{c.level}</span>
            <div className="actions" style={{ marginTop: 10 }}>
              <button type="button" className="btn btn-primary btn-sm" style={{ flex: 1 }} onClick={() => navigate(`/member/courses/${c.id}`)}>
                View Details
              </button>
              <button type="button" className="btn btn-ghost btn-sm" onClick={() => toggleWish(c.id)}>
                {state.wishlist.includes(c.id) ? '♥ Saved' : '+ Wishlist'}
              </button>
            </div>
          </div>
        ))}
      </div>
    </MemberShell>
  );
}
