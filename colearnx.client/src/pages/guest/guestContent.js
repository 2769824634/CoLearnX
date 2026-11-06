export const GUEST_SUBJECTS = [
  'Business & Management',
  'IT & Data',
  'Design & Media',
  'Health & Care',
  'Household & Hospitality',
  'Lifestyle & Crafts',
  'Languages & Arts',
  'Education & Career',
];

export const GUEST_STEPS = [
  { title: 'Find a program', detail: 'Browse theory and hands-on courses by subject and level, from first steps to mastery.' },
  { title: 'Enrol with credits', detail: 'Top up once, then reserve a place in a scheduled class. No subscription to cancel.' },
  { title: 'Learn and get certified', detail: 'Attend the sessions, pass the assessment and request your completion certificate.' },
];

export const GUEST_LEVELS = ['Beginner', 'Intermediate', 'Advanced'];

const PALETTES = [
  ['#f0ebff', '#7241ff'],
  ['#e6faf6', '#0b8f77'],
  ['#eaf0ff', '#3b5bdb'],
  ['#fff4e6', '#c46a0a'],
  ['#e7f5ff', '#1c7ed6'],
  ['#fdeef4', '#c2255c'],
];

function hash(text) {
  let value = 2166136261;
  for (const char of String(text || '')) {
    value ^= char.codePointAt(0);
    value = Math.imul(value, 16777619);
  }
  return value >>> 0;
}

// Learning paths get a fixed colour and icon, so a cover says what kind of course it is.
const PATH_ART = {
  Technology: { base: '#2f6fd6', icon: 'code' },
  Design: { base: '#0f8f78', icon: 'pen' },
  'Professional Skills': { base: '#6a3df0', icon: 'briefcase' },
};

export function courseArt(seed, path) {
  const known = PATH_ART[path];
  const [, ink] = PALETTES[hash(seed) % PALETTES.length];
  return {
    className: 'g-art',
    style: { '--art-base': known?.base || ink },
    icon: known?.icon || 'book',
  };
}

// "INFT 2002" -> ["INFT", "2002"]; codes without a letter prefix stay whole.
export function splitCourseCode(code) {
  const match = /^([A-Za-z]+)[\s-]*(\S.*)$/.exec(String(code || '').trim());
  return match ? [match[1].toUpperCase(), match[2]] : ['', String(code || '')];
}

export function peopleLine(course) {
  if (course.trainerNames?.length) return `With ${course.trainerNames.join(', ')}`;
  if (course.creatorName) return `Created by ${course.creatorName}`;
  return 'Trainer assigned when a class opens';
}
