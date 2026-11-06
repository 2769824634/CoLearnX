import { courseArt, peopleLine, splitCourseCode } from './guestContent';

export function Arrow() {
  return (
    <svg className="g-arrow" viewBox="0 0 20 20" aria-hidden="true" focusable="false">
      <path d="M3 10h13M11 4.5 16.5 10 11 15.5" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

const ART_ICON_PATHS = {
  code: 'M7.5 6 3.5 10l4 4M12.5 6l4 4-4 4',
  pen: 'M12.5 4.5l3 3L7 16H4v-3zM11 6l3 3',
  briefcase: 'M3.5 7.5h13v8h-13zM7.5 7.5V5.5h5v2M3.5 11h13',
  book: 'M4 5.5A1.5 1.5 0 0 1 5.5 4H10v12H5.5A1.5 1.5 0 0 0 4 17.5zM16 5.5A1.5 1.5 0 0 0 14.5 4H10v12h4.5a1.5 1.5 0 0 1 1.5 1.5z',
};

function ArtIcon({ name }) {
  return (
    <svg className="g-art-icon" viewBox="0 0 20 20" focusable="false">
      <path d={ART_ICON_PATHS[name]} fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

export function CourseArt({ code, path, large = false, small = false }) {
  const art = courseArt(code, path);
  const [prefix, number] = splitCourseCode(code);
  return (
    <div className={`${art.className}${large ? ' g-art-large' : ''}${small ? ' g-art-small' : ''}`} style={art.style} aria-hidden="true">
      {small ? <ArtIcon name={art.icon} /> : (
        <>
          {path ? <em className="g-art-path">{path}</em> : null}
          <span className="g-art-code">
            {prefix ? <small>{prefix}</small> : null}
            <b>{number}</b>
          </span>
          <ArtIcon name={art.icon} />
        </>
      )}
    </div>
  );
}

export default function GuestCourseCard({ course, onOpen, onWishlist, index = 0 }) {
  return (
    <article className="g-card" style={{ '--i': index }}>
      <button type="button" className="g-card-art" tabIndex={-1} aria-hidden="true" onClick={onOpen}>
        <CourseArt code={course.code} path={course.learningPath} />
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
