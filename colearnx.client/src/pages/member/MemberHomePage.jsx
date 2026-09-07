import { useNavigate } from 'react-router-dom';
import MemberShell from '../../components/MemberShell';
import { useMemberData } from './MemberDataContext';

// Dashboard: credits, continue learning, featured.
export default function MemberHomePage() {
  const navigate = useNavigate();
  const { state, showToast, toggleWish } = useMemberData();
  const active = state.enrolled.filter((e) => e.status === 'active');
  const completed = state.enrolled.filter((e) => e.status === 'completed');
  const featured = state.courses.filter((c) => c.featured).slice(0, 4);

  return (
    <MemberShell
      title="Member Dashboard"
      subtitle={`Welcome back, ${state.user.displayName}`}
      onSearch={(q) => navigate(`/member/courses?q=${encodeURIComponent(q)}`)}
      onNotify={() => showToast('No new notifications')}
    >
      {state.loading ? <p className="page-sub">Loading…</p> : null}

      <div className="btn-row">
        <button type="button" className="btn btn-teal" onClick={() => navigate('/member/courses')}>
          Browse Catalog
        </button>
        <button type="button" className="btn btn-ghost" onClick={() => navigate('/member/programs')}>
          My Programs
        </button>
        <button type="button" className="btn btn-ghost" onClick={() => navigate('/member/payment')}>
          Top Up Credits
        </button>
      </div>

      <div className="stat-grid">
        <div className="stat-box"><div className="label">Credit Balance</div><div className="value">{state.credits}</div></div>
        <div className="stat-box"><div className="label">Active Programs</div><div className="value">{active.length}</div></div>
        <div className="stat-box"><div className="label">Completed</div><div className="value">{completed.length}</div></div>
        <div className="stat-box"><div className="label">Wishlist</div><div className="value">{state.wishlist.length}</div></div>
        <div className="stat-box"><div className="label">Certificates</div><div className="value">{state.certificates.length}</div></div>
      </div>

      <div className="card purple-bg" style={{ marginBottom: 16 }}>
        <div className="card-body" style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
          <div>
            <div style={{ fontSize: 12, color: 'var(--slate)' }}>My Credits</div>
            <div style={{ fontSize: 32, fontWeight: 700, color: 'var(--purple)' }}>{state.credits}</div>
            <div style={{ fontSize: 12, color: 'var(--teal)' }}>Credits Available</div>
          </div>
          <button type="button" className="btn btn-teal" onClick={() => navigate('/member/payment')}>
            Top Up Credits
          </button>
        </div>
      </div>

      <h3 className="section-title">Continue Learning</h3>
      {active.length === 0 ? (
        <div className="callout" style={{ marginBottom: 20 }}>
          <div className="callout-title">No active programs</div>
          Browse the catalog to enrol and start learning.
        </div>
      ) : (
        <div className="continue-grid" style={{ marginBottom: 24 }}>
          {active.map((e) => {
            const codeParts = String(e.courseCode || '').split(/\s+/);
            return (
              <div className="card continue-card" key={e.enrollmentId || e.id}>
                <div className="continue-card-body">
                  <div className="continue-thumb" aria-hidden="true">
                    {codeParts.length > 1 ? (
                      <>
                        <span className="continue-thumb-prefix">{codeParts[0]}</span>
                        <span className="continue-thumb-num">{codeParts.slice(1).join(' ')}</span>
                      </>
                    ) : (
                      <span className="continue-thumb-num">{e.courseCode}</span>
                    )}
                  </div>
                  <div className="continue-meta">
                    <div className="continue-title">{e.courseTitle}</div>
                    <div className="continue-progress-label">{e.progress}% complete</div>
                    <div className="progress-bar continue-progress">
                      <div className="fill" style={{ width: `${e.progress}%` }} />
                    </div>
                  </div>
                  <button
                    type="button"
                    className="btn btn-primary continue-btn"
                    onClick={() => navigate('/member/programs')}
                  >
                    Continue
                  </button>
                </div>
              </div>
            );
          })}
        </div>
      )}

      <h3 className="section-title">Featured Training Programs</h3>
      <div className="grid-4">
        {(featured.length ? featured : state.courses.slice(0, 4)).map((c) => (
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
