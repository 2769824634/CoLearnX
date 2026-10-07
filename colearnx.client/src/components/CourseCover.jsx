const PATTERNS = ['grid', 'rings', 'stripes', 'dots'];
const TONES = ['purple', 'teal', 'blend'];

function seedFor(code) {
  return [...code].reduce((hash, char) => (hash * 31 + char.charCodeAt(0)) >>> 0, 7);
}

// Generated stand-in until Courses carry real cover images.
export default function CourseCover({ code, className = '' }) {
  const label = String(code || '');
  const seed = seedFor(label);
  const [prefix, ...rest] = label.split(/\s+/);
  return (
    <div
      className={`course-cover ${className}`.trim()}
      data-pattern={PATTERNS[seed % PATTERNS.length]}
      data-tone={TONES[Math.floor(seed / PATTERNS.length) % TONES.length]}
      aria-hidden="true"
    >
      <span className="course-cover-code">
        {rest.length ? <><small>{prefix}</small>{rest.join(' ')}</> : label}
      </span>
    </div>
  );
}
