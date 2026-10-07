import { useEffect, useMemo } from 'react';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import { SITE_TITLE } from '../../siteTitle';
import GuestCourseCard, { Arrow, CourseArt, GuestCardSkeleton } from './GuestCourseCard';
import Reveal from './Reveal';
import { GUEST_LEVELS, GUEST_STEPS, GUEST_SUBJECTS } from './guestContent';

function HeroPanel({ courses, loading }) {
  const preview = courses.slice(0, 3);
  return (
    <div className="g-hero-panel g-load" style={{ '--i': 2 }} aria-hidden="true">
      <div className="g-hero-panel-head">
        <span>Open for enrolment</span>
        <span className="g-hero-panel-dot">Live</span>
      </div>
      {loading || !preview.length ? (
        <ul className="g-hero-panel-list">
          {GUEST_STEPS.map((step, index) => (
            <li key={step.title}>
              <span className="g-hero-panel-num">{index + 1}</span>
              <span><strong>{step.title}</strong><small>{step.detail}</small></span>
            </li>
          ))}
        </ul>
      ) : (
        <ul className="g-hero-panel-list">
          {preview.map((course) => (
            <li key={course.id}>
              <CourseArt code={course.code} small />
              <span>
                <strong>{course.title}</strong>
                <small>{course.level || 'All levels'} · {course.credits ?? '—'} credits</small>
              </span>
            </li>
          ))}
        </ul>
      )}
      <p className="g-hero-panel-foot">Small classes · Scheduled sessions · Certificate on completion</p>
    </div>
  );
}

export default function GuestHome({ courses, loading, error, onWishlist }) {
  const navigate = useNavigate();
  const location = useLocation();
  const stats = useMemo(() => ({
    programs: courses.length,
    trainers: new Set(courses.flatMap((course) => course.trainerNames || [])).size,
    creators: new Set(courses.map((course) => course.creatorName).filter(Boolean)).size,
  }), [courses]);

  useEffect(() => {
    if (location.hash !== '#how') return;
    document.getElementById('how')?.scrollIntoView?.({ behavior: 'smooth', block: 'start' });
  }, [location.hash]);

  return (
    <>
      <section className="g-hero">
        <div className="g-hero-copy">
          <p className="g-badge g-load" style={{ '--i': 0 }}>Co-learning for real-world skills</p>
          <h1 className="g-hero-title g-load" style={{ '--i': 1 }}>
            CoLearn<span className="g-brand-x">X</span>
          </h1>
          <p className="g-tagline g-load" style={{ '--i': 1 }}>
            Your path to what&rsquo;s ne<span className="g-brand-x">X</span>t.
          </p>
          <p className="g-lede g-load" style={{ '--i': 2 }}>
            Theory and hands-on programs led by working trainers, from code and design to childcare,
            household management and home cooking. Pay with credits, learn in small classes, finish with a certificate.
          </p>
          <div className="g-hero-cta g-load" style={{ '--i': 3 }}>
            <Link className="g-btn g-btn-primary g-btn-lg" to="/register">Sign up free <Arrow /></Link>
            <Link className="g-btn g-btn-outline g-btn-lg" to="/courses">Browse courses</Link>
          </div>
        </div>
        <HeroPanel courses={courses} loading={loading} />
      </section>

      <dl className="g-stats g-load" style={{ '--i': 4 }}>
        <div><dt>Programs open</dt><dd>{loading ? '—' : stats.programs}</dd></div>
        <div><dt>Trainers teaching</dt><dd>{loading ? '—' : stats.trainers}</dd></div>
        <div><dt>Course creators</dt><dd>{loading ? '—' : stats.creators}</dd></div>
        <div><dt>Learning stages</dt><dd>{GUEST_LEVELS.length}</dd></div>
      </dl>

      <section className="g-section g-subjects" aria-label="Subjects on CoLearnX">
        <p className="g-subjects-label">Subjects</p>
        <ul>
          {GUEST_SUBJECTS.map((subject) => <li key={subject}>{subject}</li>)}
        </ul>
      </section>

      <section className="g-section" id="how" aria-labelledby="g-steps-title">
        <Reveal className="g-section-head g-section-head-center">
          <p className="g-kicker">How it works</p>
          <h2 id="g-steps-title" className="g-h2">How {SITE_TITLE} works</h2>
          <p className="g-section-lede">Three steps from curious to certified. Every class is scheduled, small and led by a person.</p>
        </Reveal>
        <ol className="g-steps">
          {GUEST_STEPS.map((step, index) => (
            <Reveal as="li" key={step.title} delay={index * 80}>
              <span className="g-step-num">{index + 1}</span>
              <strong>{step.title}</strong>
              <p>{step.detail}</p>
            </Reveal>
          ))}
        </ol>
      </section>

      <section className="g-section g-programs" aria-labelledby="g-programs-title">
        <Reveal className="g-section-head">
          <div>
            <p className="g-kicker">Open for enrolment</p>
            <h2 id="g-programs-title" className="g-h2">Training programs</h2>
          </div>
          <Link className="g-link-arrow" to="/courses">Browse all courses <Arrow /></Link>
        </Reveal>
        {loading ? <p className="g-sr" role="status">Loading programs…</p> : null}
        {error ? <p className="g-callout" role="alert">{error}</p> : null}
        <div className="g-grid">
          {loading ? <GuestCardSkeleton /> : courses.slice(0, 8).map((course, index) => (
            <GuestCourseCard
              key={course.id}
              course={course}
              index={index}
              onOpen={() => navigate(`/courses/${course.id}`)}
              onWishlist={onWishlist}
            />
          ))}
        </div>
      </section>

      <section className="g-section g-teach" aria-labelledby="g-teach-title">
        <Reveal className="g-section-head g-section-head-center">
          <p className="g-kicker">Share what you know</p>
          <h2 id="g-teach-title" className="g-h2">Know something worth teaching?</h2>
        </Reveal>
        <div className="g-teach-grid">
          <Reveal className="g-teach-card" delay={0}>
            <span className="g-chip g-chip-purple">Trainer</span>
            <h3>Run the class</h3>
            <p>Lead scheduled sessions, guide hands-on practice and sign off the skills your learners earn.</p>
            <Link className="g-link-arrow" to="/register">Apply to teach <Arrow /></Link>
          </Reveal>
          <Reveal className="g-teach-card" delay={80}>
            <span className="g-chip g-chip-teal">Creator</span>
            <h3>Shape the course</h3>
            <p>Write the outline and publish materials once. Every time a Trainer uses them, it is on your record.</p>
            <Link className="g-link-arrow" to="/register">Become a Creator <Arrow /></Link>
          </Reveal>
        </div>
      </section>
    </>
  );
}
