import { Link, useNavigate } from 'react-router-dom';
import PublicSiteShell, { useGuestGate } from '../components/PublicSiteShell';
import MemberShell from '../components/MemberShell';
import { MemberNotificationsProvider } from '../components/MemberNotificationsProvider';
import AuthRequiredModal from '../components/AuthRequiredModal';
import { useAuth } from '../auth/AuthContext';
import { resolveAuthSession, SessionLoading } from '../auth/authSession';
import { SITE_TITLE } from '../siteTitle';
import MemberOnboardingModal, { needsMemberOnboarding } from './member/MemberOnboardingModal';
import { usePublicCourses } from './publicCourses';

function CoursePreview({ course, onOpen, onWishlist }) {
  return (
    <div className="course-card">
      <div className="thumb">{course.code}</div>
      <h4>{course.title}</h4>
      <div className="meta">{course.trainerNames?.length ? `Trainer: ${course.trainerNames.join(', ')}` : 'Trainer assigned when a class opens'}</div>
      {course.creatorName ? <div className="meta">Creator: {course.creatorName}</div> : null}
      <span className="pill">{course.credits} Credits</span>{' '}
      <span className="pill neutral">{course.level}</span>
      <div className="actions" style={{ marginTop: 10 }}>
        <button type="button" className="btn btn-primary btn-sm" style={{ flex: 1 }} onClick={() => onOpen(course.id)}>
          View Details
        </button>
        <button type="button" className="btn btn-ghost btn-sm" onClick={onWishlist}>
          + Wishlist
        </button>
      </div>
    </div>
  );
}

export default function PublicHomePage({ onboardingMode = 'auto' }) {
  const navigate = useNavigate();
  const auth = useAuth();
  const session = resolveAuthSession(auth);
  const { courses, loading, error } = usePublicCourses();
  const gate = useGuestGate();
  const showOnboarding = onboardingMode === 'edit' || (onboardingMode === 'auto' && needsMemberOnboarding(auth.user));
  const isMember = session === 'member';
  const detailPath = isMember ? '/member/courses' : '/courses';

  if (session === 'booting') return <SessionLoading />;

  const body = (
    <>
      <div className="public-home-hero">
        <h1>{SITE_TITLE}</h1>
        <p>Your Path to What's neXt.</p>
      </div>
      <h2 className="section-title">Training programs</h2>
      {loading ? <p role="status">Loading programs…</p> : null}
      {error ? <p className="callout warn" role="alert">{error}</p> : null}
      <div className="grid-4">
        {courses.map((course) => (
          <CoursePreview
            key={course.id}
            course={course}
            onOpen={(id) => navigate(`${detailPath}/${id}`)}
            onWishlist={isMember ? () => navigate(`/member/courses/${course.id}`) : gate.openAuth}
          />
        ))}
      </div>
      {courses.length ? (
        <p className="public-home-more">
          <Link to={detailPath}>Browse all courses</Link>
        </p>
      ) : null}
      {showOnboarding ? <MemberOnboardingModal dismissible={onboardingMode === 'edit'} /> : null}
    </>
  );

  if (isMember) {
    return (
      <MemberNotificationsProvider>
        <MemberShell onSearch={(query) => navigate(`/member/courses?q=${encodeURIComponent(query)}`)}>
          {body}
        </MemberShell>
      </MemberNotificationsProvider>
    );
  }

  return (
    <PublicSiteShell>
      {body}
      <AuthRequiredModal open={gate.authOpen} onClose={gate.closeAuth} />
    </PublicSiteShell>
  );
}
