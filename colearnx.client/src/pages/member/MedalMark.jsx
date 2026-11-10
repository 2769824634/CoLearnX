import { useId } from 'react';

const metals = {
  Locked: { rim: ['#8d95a1', '#e6ebf0', '#626b76'], face: ['#f4f6f8', '#b7c0ca'], icon: '#4e5966' },
  Brass: { rim: ['#7a4e22', '#f3d7a8', '#4e3012'], face: ['#f0d0a0', '#c48a42'], icon: '#3f2910' },
  Silver: { rim: ['#66717f', '#f7f9fb', '#3a424d'], face: ['#eef2f6', '#8f9bab'], icon: '#24303c' },
  Gold: { rim: ['#8a6410', '#fff3bf', '#5a4208'], face: ['#ffe7a3', '#c9961c'], icon: '#3f2c06' },
  Diamond: { rim: ['#5e9fc0', '#f5fdff', '#215f7c'], face: ['#f3fcff', '#7ec8e3'], icon: '#123848' },
};

const outer = '80,8 146,46 146,118 80,156 14,118 14,46';
const inner = '80,26 132,54 132,110 80,138 28,110 28,54';

function MedalIcon({ area }) {
  if (area === 'BusinessManagement') {
    return (
      <g fill="currentColor">
        <rect x="58" y="92" width="14" height="28" rx="2" />
        <rect x="76" y="78" width="14" height="42" rx="2" />
        <rect x="94" y="64" width="14" height="56" rx="2" />
      </g>
    );
  }
  if (area === 'HumanitiesHealthCreative') {
    return (
      <path
        fill="currentColor"
        d="M80 68v48c-7-5-16-7-26-7V74c10 0 19 2 26 6zm0 0c7-4 16-6 26-6v35c-10 0-19 2-26 7z"
      />
    );
  }
  return (
    <g fill="none" stroke="currentColor" strokeWidth="4" strokeLinejoin="round">
      <rect x="60" y="68" width="40" height="40" rx="6" />
      <path d="M70 88h20M80 78v20" />
    </g>
  );
}

export default function MedalMark({ area, material, title }) {
  const id = useId().replace(/:/g, '');
  const metal = metals[material] || metals.Locked;
  const label = material === 'Locked' ? 'locked' : material.toLowerCase();
  return (
    <svg className={`medal-mark medal-mark-${label}`} viewBox="0 0 160 164" role="img" aria-label={`${title} ${label} medal`}>
      <defs>
        <linearGradient id={`${id}-rim`} x1="20" y1="12" x2="140" y2="150" gradientUnits="userSpaceOnUse">
          <stop offset="0" stopColor={metal.rim[1]} />
          <stop offset="0.45" stopColor={metal.rim[0]} />
          <stop offset="1" stopColor={metal.rim[2]} />
        </linearGradient>
        <linearGradient id={`${id}-face`} x1="40" y1="30" x2="120" y2="140" gradientUnits="userSpaceOnUse">
          <stop offset="0" stopColor={metal.face[0]} />
          <stop offset="1" stopColor={metal.face[1]} />
        </linearGradient>
        <radialGradient id={`${id}-gloss`} cx="50%" cy="28%" r="55%">
          <stop offset="0" stopColor="#ffffff" stopOpacity="0.85" />
          <stop offset="1" stopColor="#ffffff" stopOpacity="0" />
        </radialGradient>
        <filter id={`${id}-shadow`} x="-20%" y="-10%" width="140%" height="140%">
          <feDropShadow dx="0" dy="3" stdDeviation="2.2" floodColor="#1c2430" floodOpacity="0.28" />
        </filter>
      </defs>
      <polygon points={outer} fill={`url(#${id}-rim)`} filter={`url(#${id}-shadow)`} />
      <polygon points={inner} fill={`url(#${id}-face)`} />
      <polygon points={inner} fill={`url(#${id}-gloss)`} />
      {material === 'Diamond' ? (
        <g stroke="#ffffff" strokeWidth="1.4" opacity="0.75">
          <path d="M80 34 L104 82 L80 130 L56 82 Z" fill="none" />
          <path d="M48 70 L112 102" />
        </g>
      ) : null}
      <g style={{ color: metal.icon }}>
        <MedalIcon area={area} />
      </g>
    </svg>
  );
}
