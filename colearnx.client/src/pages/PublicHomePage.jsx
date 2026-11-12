import { Link, Navigate, useNavigate } from 'react-router-dom';
import PublicSiteShell from '../components/PublicSiteShell';
import { useGuestGate } from '../components/useGuestGate';
import MemberShell from '../components/MemberShell';
import { MemberNotificationsProvider } from '../components/MemberNotificationsProvider';
import AuthRequiredModal from '../components/AuthRequiredModal';
import { useAuth } from '../auth/AuthContext';
import { SessionLoading } from '../auth/authSession';
import { resolveAuthSession } from '../auth/resolveAuthSession';
import { SITE_TITLE } from '../siteTitle';
import MemberOnboardingModal from './member/MemberOnboardingModal';
import { usePublicCourses } from './publicCourses';
import GuestHome from './guest/GuestHome';
import { CourseArt } from './guest/GuestCourseCard';

// Same card as the member catalog: learning-path cover, level, credits, people.
function CoursePreview({ course, onOpen }) {
  const open = () => onOpen(course.id);
  return (
    <article className="cx-card">
      <button type="button" className="cx-card-art" tabIndex={-1} aria-hidden="true" onClick={open}>
        <CourseArt code={course.code} path={course.learningPath} />
      </button>
      <div className="cx-card-body">
        <div className="cx-card-meta">
          <span className="cx-chip">{course.level}</span>
          <span className="cx-card-credits"><strong>{course.credits}</strong> Credits</span>
        </div>
        <h4 className="cx-card-title">{course.title}</h4>
        <p className="cx-card-people">{course.trainerNames?.length ? <>Trainer <b>{course.trainerNames.join(', ')}</b></> : 'Trainer assigned when a class opens'}</p>
        {course.creatorName ? <p className="cx-card-people">Creator <b>{course.creatorName}</b></p> : null}
        <div className="cx-card-actions">
          <button type="button" className="btn btn-primary btn-sm" onClick={open}>
            View Details
          </button>
        </div>
      </div>
    </article>
  );
}

export default function PublicHomePage({ onboardingMode = 'auto' }) {
  const navigate = useNavigate();
  const auth = useAuth();
  const session = resolveAuthSession(auth);
  const { courses, loading, error } = usePublicCourses();
  const gate = useGuestGate();
  // The interests API only serves the Member role, so other sessions would get a dialog that can neither load nor skip.
  const showOnboarding = session === 'member' && onboardingMode === 'edit';

  if (session === 'booting') return <SessionLoading />;
  // A signed-in Member has a dashboard of their own; this page is for visitors.
  if (session === 'member' && onboardingMode !== 'edit') return <Navigate to="/member/home" replace />;

  if (session === 'member') {
    const platformCourses = courses.filter((course) => course.isPlatformOwned);
    const otherCourses = courses.filter((course) => !course.isPlatformOwned);
    return (
      <MemberNotificationsProvider>
        <MemberShell onSearch={(query) => navigate(`/member/courses?q=${encodeURIComponent(query)}`)}>
          <div className="cx-catalog cx-member-home">
          <div className="public-home-hero cx-home-hero">
            <h1>{SITE_TITLE}</h1>
            <p>Your Path to What's neXt.</p>
            <Link className="btn btn-primary" to="/member/courses">Browse all courses</Link>
          </div>
          {platformCourses.length ? <section className="cx-home-section">
            <div className="section-heading cx-home-heading"><h2 className="section-title">CoLearnX courses</h2><Link to="/courses">View all platform courses</Link></div>
            <div className="cx-grid">{platformCourses.map((course) => <CoursePreview key={course.id} course={course} onOpen={(id) => navigate(`/member/courses/${id}`)} />)}</div>
          </section> : null}
          <section className="cx-home-section">
            <div className="section-heading cx-home-heading"><h2 className="section-title">Training programs</h2>{courses.length ? <Link to="/member/courses">Show all courses</Link> : null}</div>
            {loading ? <p role="status">Loading programs…</p> : null}
            {error ? <p className="callout warn" role="alert">{error}</p> : null}
            <div className="cx-grid">
              {otherCourses.map((course) => (
                <CoursePreview key={course.id} course={course} onOpen={(id) => navigate(`/member/courses/${id}`)} />
              ))}
            </div>
          </section>
          </div>
          {showOnboarding ? <MemberOnboardingModal dismissible={onboardingMode === 'edit'} /> : null}
        </MemberShell>
      </MemberNotificationsProvider>
    );
  }

  return (
    <PublicSiteShell>
      <GuestHome courses={courses} loading={loading} error={error} onWishlist={gate.openAuth} />
      <AuthRequiredModal open={gate.authOpen} onClose={gate.closeAuth} />
    </PublicSiteShell>
  );
}
