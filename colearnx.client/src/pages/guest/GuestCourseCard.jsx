import { courseArt, peopleLine } from './guestContent';

export function Arrow() {
  return (
    <svg className="g-arrow" viewBox="0 0 20 20" aria-hidden="true" focusable="false">
      <path d="M3 10h13M11 4.5 16.5 10 11 15.5" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

export function CourseArt({ code, large = false, small = false }) {
  const art = courseArt(code);
  return (
    <div className={`${art.className}${large ? ' g-art-large' : ''}${small ? ' g-art-small' : ''}`} style={art.style} aria-hidden="true">
      <span>{code}</span>
    </div>
  );
}

export default function GuestCourseCard({ course, onOpen, onWishlist, index = 0 }) {
  return (
    <article className="g-card" style={{ '--i': index }}>
      <button type="button" className="g-card-art" tabIndex={-1} aria-hidden="true" onClick={onOpen}>
        <CourseArt code={course.code} />
      </button>
      <div className="g-card-body">
        <div className="g-card-meta">
          <span className="g-chip">{course.level || 'All levels'}</span>
          <span className="g-card-credits"><strong>{course.credits ?? '—'}</strong> credits</span>
        </div>
        <h3 className="g-card-title">{course.title}</h3>
        <p className="g-card-people">{peopleLine(course)}</p>
        <div className="g-card-actions">
          <button type="button" className="g-btn g-btn-ink g-btn-sm" onClick={onOpen}>
            View Details <Arrow />
          </button>
          <button type="button" className="g-wish" onClick={onWishlist}>
            <span className="g-wish-plus">+</span> Wishlist
          </button>
        </div>
      </div>
    </article>
  );
}

export function GuestCardSkeleton({ count = 4 }) {
  return Array.from({ length: count }, (_, index) => (
    <div key={index} className="g-card g-card-skeleton" aria-hidden="true">
      <div className="g-card-art" />
      <div className="g-card-body"><span /><span /><span /></div>
    </div>
  ));
}
