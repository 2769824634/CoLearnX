import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import MemberShell from '../../components/MemberShell';
import { recommendationsApi } from '../../api';
import { userFacingError } from '../businessPresentation';
import { useMemberData } from './memberDataState';

// Dashboard: credits, continue learning, progression recommendations.
export default function MemberHomePage() {
  const navigate = useNavigate();
  const { state, toggleWish, reload } = useMemberData();
  const active = state.enrolled.filter((e) => e.status === 'active');
  const completed = state.enrolled.filter((e) => e.status === 'completed');
  const [recommendations, setRecommendations] = useState(null);
  const [recommendationError, setRecommendationError] = useState('');

  useEffect(() => {
    let activeRequest = true;
    recommendationsApi.my().then((data) => { if (activeRequest) setRecommendations(data); })
      .catch((error) => { if (activeRequest) setRecommendationError(userFacingError(error, 'Could not load recommendations.')); });
    return () => { activeRequest = false; };
  }, []);

  return (
    <MemberShell
      title="Member Dashboard"
      subtitle={`Welcome back, ${state.user.displayName}`}
      onSearch={(q) => navigate(`/member/courses?q=${encodeURIComponent(q)}`)}
    >
      {state.loading ? <p className="page-sub" role="status">Loading your dashboard…</p> : null}
      {state.loadError ? <div className="callout warn" role="alert">{state.loadError} <button type="button" className="btn btn-ghost" onClick={reload}>Retry</button></div> : null}

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

      {!state.loading && !state.loadError ? <><div className="stat-grid">
        <div className="stat-box"><div className="label">Available Credits</div><div className="value">{state.credits}</div><div>On hold: {state.heldCredits ?? 0}</div></div>
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
            <div style={{ fontSize: 12, color: 'var(--slate)' }}>On hold: {state.heldCredits ?? 0} · Total: {state.totalCredits ?? state.credits}</div>
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
                    onClick={() => navigate(`/member/programs?tab=active&enrollmentId=${e.enrollmentId}`)}
                  >
                    Continue
                  </button>
                </div>
              </div>
            );
          })}
        </div>
      )}

      </> : null}
      <h3 className="section-title">Recommended for you</h3>
      {recommendationError ? <p role="alert" className="callout warn">{recommendationError}</p> : null}
      {!recommendations && !recommendationError ? <p role="status">Loading recommendations…</p> : null}
      {recommendations && !recommendations.items.length ? <div className="callout">No matching courses are open for registration right now. Browse the catalog or update your interests.</div> : null}
      <div className="grid-4">
        {(recommendations?.items || []).map((c) => (
          <div className="course-card" key={c.courseId}>
            <div className="thumb">{c.code}</div>
            <h4>{c.title}</h4>
            <div className="meta">{c.interests.map((item) => item.name).join(' · ')}</div>
            <span className="pill">{c.creditCost} Credits</span>{' '}
            <span className="pill neutral">{c.level}</span>{' '}
            <span className="pill neutral">{c.ratingCount ? `${Number(c.averageStars).toFixed(1)} ★ (${c.ratingCount})` : 'New'}</span>
            <div className="actions" style={{ marginTop: 10 }}>
              <button type="button" className="btn btn-primary btn-sm" style={{ flex: 1 }} onClick={() => navigate(`/member/courses/${c.courseId}`)}>
                View Details
              </button>
              <button type="button" className="btn btn-ghost btn-sm" onClick={() => toggleWish(c.courseId)}>
                {state.wishlist.includes(c.courseId) ? '♥ Saved' : '+ Wishlist'}
              </button>
            </div>
          </div>
        ))}
      </div>
    </MemberShell>
  );
}
