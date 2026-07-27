import brandLogo from '../assets/brand/next-logo.png';

// Brand mark (:neXt). Shared: Logo — asset at src/assets/brand/next-logo.png
export default function Logo({ className = '' }) {
  return (
    <div className={`logo ${className}`.trim()}>
      <img
        className="logo-img"
        src={brandLogo}
        alt=":neXt — Your Path to What's neXt"
      />
    </div>
  );
}
