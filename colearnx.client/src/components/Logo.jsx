import brandLogo from '../assets/brand/next-logo.png';
import brandLogoOnDark from '../assets/brand/next-logo-on-dark.png';

// Brand mark (:neXt). tone="dark" swaps the charcoal ink for white on dark surfaces.
export default function Logo({ className = '', tone = 'light' }) {
  const onDark = tone === 'dark';
  return (
    <div className={`logo${onDark ? ' logo-on-dark' : ''} ${className}`.trim()}>
      <img
        className="logo-img"
        src={onDark ? brandLogoOnDark : brandLogo}
        alt=":neXt — Your Path to What's neXt"
      />
    </div>
  );
}
