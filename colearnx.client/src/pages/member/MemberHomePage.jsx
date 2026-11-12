import { useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { CourseArt } from '../guest/GuestCourseCard';
import MemberShell from '../../components/MemberShell';
import { recommendationsApi } from '../../api';
import { userFacingError } from '../businessPresentation';
import { useAuth } from '../../auth/AuthContext';
import { useMemberSlices } from './memberDataState';
import MemberOnboardingModal from './MemberOnboardingModal';
import { needsMemberOnboarding } from './needsMemberOnboarding';

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
  const { state, toggleWish, reload } = useMemberSlices('catalog', 'enrollments', 'certificates');
  const active = state.enrolled.filter((e) => e.status === 'active');
  const completed = state.enrolled.filter((e) => e.status === 'completed');
  const [upNext, ...alsoActive] = active;
  const [recommendations, setRecommendations] = useState(null);
  const [recommendationError, setRecommendationError] = useState('');

  const { user } = useAuth();
  const onboarding = needsMemberOnboarding(user);

  // Recommendations start from the chosen interests, so they are requested once the interest steps are done.
  useEffect(() => {
    if (onboarding) return undefined;
    let activeRequest = true;
    recommendationsApi.my().then((data) => { if (activeRequest) setRecommendations(data); })
      .catch((error) => { if (activeRequest) setRecommendationError(userFacingError(error, 'Could not load recommendations.')); });
    return () => { activeRequest = false; };
  }, [onboarding]);

  return (
    <MemberShell
      title={`Welcome back, ${state.user.displayName}`}
      subtitle="Here is where your learning stands today."
      onSearch={(q) => navigate(`/member/courses?q=${encodeURIComponent(q)}`)}
    >
      {state.loading ? <p className="page-sub" role="status">Loading your dashboard…</p> : null}
      {state.loadError ? <div className="callout warn" role="alert">{state.loadError} <button type="button" className="btn btn-ghost" onClick={reload}>Retry</button></div> : null}

      <div className="cx-catalog">
      {!state.loading && !state.loadError ? (
        <div className="member-home-top">
          <div className="member-home-learning">
            {upNext ? (
              <section className="member-up-next" aria-labelledby="member-up-next-title">
                <div className="member-up-next-cover"><CourseArt code={upNext.courseCode} path={upNext.learningPath} /></div>
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
                    <span className="cx-prog-thumb member-also-cover"><CourseArt code={e.courseCode} path={e.learningPath} small /></span>
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

      <div className="cx-home-heading">
        <h2 className="section-title">Recommended for you</h2>
        <Link to="/member/courses">Show all courses</Link>
      </div>
      {recommendationError ? <p role="alert" className="callout warn">{recommendationError}</p> : null}
      {!recommendations && !recommendationError ? <p role="status">Loading recommendations…</p> : null}
      {recommendations && !recommendations.items.length ? <div className="callout">No courses match your interests yet. Browse the catalog or update your interests.</div> : null}
      <div className="cx-grid">
        {(recommendations?.items || []).map((c) => {
          const open = () => navigate(`/member/courses/${c.courseId}`);
          const saved = state.wishlist.includes(c.courseId);
          return (
            <article className="cx-card" key={c.courseId}>
              <button type="button" className="cx-card-art" tabIndex={-1} aria-hidden="true" onClick={open}>
                <CourseArt code={c.code} path={c.learningPath} />
              </button>
              <div className="cx-card-body">
                <div className="cx-card-meta">
                  <span className="cx-chip">{c.level}</span>
                  <span className="cx-card-credits"><strong>{c.creditCost}</strong> Credits</span>
                </div>
                <h4 className="cx-card-title">{c.title}</h4>
                {c.suggestedPrerequisite ? <p className="cx-card-people">Suggested first: {c.suggestedPrerequisite}</p> : null}
                {c.hasOpenClass === false ? <p className="cx-card-people">No class currently open for registration</p> : null}
                {c.interests?.length ? <p className="cx-card-people">{c.interests.map((item) => item.name).join(' · ')}</p> : null}
                {c.hasOpenClass === false ? <p className="cx-card-people">No class scheduled yet</p> : null}
                <p className="cx-card-rating">
                  {c.ratingCount > 0 && c.averageStars != null
                    ? <><span className="cx-star" aria-hidden="true">★</span> <b>{Number(c.averageStars).toFixed(1)}</b> ({c.ratingCount})</>
                    : 'New · No ratings yet'}
                </p>
                <div className="cx-card-actions">
                  <button type="button" className="btn btn-primary btn-sm" onClick={open}>View Details</button>
                  <button type="button" className={`btn btn-ghost btn-sm cx-wish${saved ? ' is-saved' : ''}`} onClick={() => toggleWish(c.courseId)}>
                    {saved ? '♥ Saved' : '+ Wishlist'}
                  </button>
                </div>
              </div>
            </article>
          );
        })}
      </div>
      </div>
      {onboarding ? <MemberOnboardingModal /> : null}
    </MemberShell>
  );
}
