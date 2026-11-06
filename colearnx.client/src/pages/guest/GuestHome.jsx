import { useEffect, useMemo } from 'react';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import { SITE_TITLE } from '../../siteTitle';
import GuestCourseCard, { Arrow, CourseArt, GuestCardSkeleton } from './GuestCourseCard';
import Reveal from './Reveal';
import { GUEST_LEVELS, GUEST_STEPS, GUEST_SUBJECTS } from './guestContent';

const STAT_ICON_PATHS = {
  book: 'M4 5.5A1.5 1.5 0 0 1 5.5 4H10v12H5.5A1.5 1.5 0 0 0 4 17.5zM16 5.5A1.5 1.5 0 0 0 14.5 4H10v12h4.5a1.5 1.5 0 0 1 1.5 1.5z',
  user: 'M10 10a3.25 3.25 0 1 0 0-6.5 3.25 3.25 0 0 0 0 6.5zM3.75 16.5c.6-2.9 3.1-4.5 6.25-4.5s5.65 1.6 6.25 4.5',
  pen: 'M12.5 4.5l3 3L7 16H4v-3zM11 6l3 3',
  layers: 'M10 3.5l6.5 3.5L10 10.5 3.5 7zM3.5 10.5L10 14l6.5-3.5M3.5 14L10 17.5l6.5-3.5',
};

function StatIcon({ name, tone }) {
  return (
    <span className={`g-stat-icon${tone ? ` is-${tone}` : ''}`} aria-hidden="true">
      <svg viewBox="0 0 20 20" focusable="false">
        <path d={STAT_ICON_PATHS[name]} fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round" />
      </svg>
    </span>
  );
}

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
        <div><dt><StatIcon name="book" />Programs open</dt><dd>{loading ? '—' : stats.programs}</dd></div>
        <div><dt><StatIcon name="user" tone="blue" />Trainers teaching</dt><dd>{loading ? '—' : stats.trainers}</dd></div>
        <div><dt><StatIcon name="pen" tone="teal" />Course creators</dt><dd>{loading ? '—' : stats.creators}</dd></div>
        <div><dt><StatIcon name="layers" tone="amber" />Learning stages</dt><dd>{GUEST_LEVELS.length}</dd></div>
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
