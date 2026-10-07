import { useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import CourseCover from '../../components/CourseCover';
import MemberShell from '../../components/MemberShell';
import { recommendationsApi } from '../../api';
import { userFacingError } from '../businessPresentation';
import { useMemberData } from './memberDataState';

const programLink = (enrollment) => `/member/programs?tab=active&enrollmentId=${enrollment.enrollmentId}`;

function Progress({ title, value }) {
  const percent = Number(value) || 0;
  return (
    <div className="member-progress">
      <div className="progress-bar" role="progressbar" aria-label={`${title} progress`} aria-valuemin={0} aria-valuemax={100} aria-valuenow={percent}>
        <div className="fill" style={{ width: `${percent}%` }} />
      </div>
      <span>{percent}% complete</span>
    </div>
  );
}

// Dashboard: what to continue first, a compact learning summary, then recommendations.
export default function MemberHomePage() {
  const navigate = useNavigate();
  const { state, toggleWish, reload } = useMemberData();
  const active = state.enrolled.filter((e) => e.status === 'active');
  const completed = state.enrolled.filter((e) => e.status === 'completed');
  const [upNext, ...alsoActive] = active;
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
      title={`Welcome back, ${state.user.displayName}`}
      subtitle="Here is where your learning stands today."
      onSearch={(q) => navigate(`/member/courses?q=${encodeURIComponent(q)}`)}
    >
      {state.loading ? <p className="page-sub" role="status">Loading your dashboard…</p> : null}
      {state.loadError ? <div className="callout warn" role="alert">{state.loadError} <button type="button" className="btn btn-ghost" onClick={reload}>Retry</button></div> : null}

      {!state.loading && !state.loadError ? (
        <div className="member-home-top">
          <div className="member-home-learning">
            {upNext ? (
              <section className="member-up-next" aria-labelledby="member-up-next-title">
                <CourseCover code={upNext.courseCode} className="member-up-next-cover" />
                <div className="member-up-next-body">
                  <p className="member-up-next-label">Up next</p>
                  <h2 id="member-up-next-title">{upNext.courseTitle}</h2>
                  {upNext.trainer ? <p className="member-up-next-trainer">with {upNext.trainer}</p> : null}
                  <Progress title={upNext.courseTitle} value={upNext.progress} />
                  <Link className="btn btn-primary" to={programLink(upNext)}>Continue learning</Link>
                </div>
              </section>
            ) : (
              <section className="member-up-next member-up-next-empty" aria-labelledby="member-up-next-title">
                <div className="member-up-next-body">
                  <h2 id="member-up-next-title">No active programs</h2>
                  <p>Browse the catalog to enrol and start learning.</p>
                  <Link className="btn btn-primary" to="/member/courses">Browse Catalog</Link>
                </div>
              </section>
            )}
            {alsoActive.length ? (
              <ul className="member-also-active" aria-label="Also in progress">
                {alsoActive.map((e) => (
                  <li key={e.enrollmentId || e.id}>
                    <CourseCover code={e.courseCode} className="member-also-cover" />
                    <div>
                      <strong>{e.courseTitle}</strong>
                      <Progress title={e.courseTitle} value={e.progress} />
                    </div>
                    <Link to={programLink(e)}>Continue →</Link>
                  </li>
                ))}
              </ul>
            ) : null}
          </div>

          <aside className="member-summary" aria-label="Your learning summary">
            <dl className="member-counts">
              <div><dt>Active programs</dt><dd>{active.length}</dd></div>
              <div><dt>Completed</dt><dd>{completed.length}</dd></div>
              <div><dt>Certificates</dt><dd>{state.certificates.length}</dd></div>
              <div><dt>Wishlist</dt><dd>{state.wishlist.length}</dd></div>
            </dl>
          </aside>
        </div>
      ) : null}

      <div className="member-home-heading">
        <h2>Recommended for you</h2>
        <Link to="/member/courses">Browse Catalog →</Link>
      </div>
      {recommendationError ? <p role="alert" className="callout warn">{recommendationError}</p> : null}
      {!recommendations && !recommendationError ? <p role="status">Loading recommendations…</p> : null}
      {recommendations && !recommendations.items.length ? <div className="callout">No matching courses are open for registration right now. Browse the catalog or update your interests.</div> : null}
      <div className="member-course-grid">
        {(recommendations?.items || []).map((c) => (
          <article className="member-course-card" key={c.courseId}>
            <CourseCover code={c.code} />
            <div className="member-course-card-body">
              <h3>{c.title}</h3>
              <div className="meta">{c.interests.map((item) => item.name).join(' · ')}</div>
              <div className="member-course-pills">
                <span className="pill">{c.creditCost} Credits</span>
                <span className="pill neutral">{c.level}</span>
                <span className="pill neutral">{c.ratingCount ? `${Number(c.averageStars).toFixed(1)} ★ (${c.ratingCount})` : 'New'}</span>
              </div>
              <div className="actions">
                <button type="button" className="btn btn-primary btn-sm" onClick={() => navigate(`/member/courses/${c.courseId}`)}>
                  View Details
                </button>
                <button type="button" className="btn btn-ghost btn-sm" onClick={() => toggleWish(c.courseId)}>
                  {state.wishlist.includes(c.courseId) ? '♥ Saved' : '+ Wishlist'}
                </button>
              </div>
            </div>
          </article>
        ))}
      </div>
    </MemberShell>
  );
}
