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

export function courseArt(seed) {
  const value = hash(seed);
  const [base, ink] = PALETTES[value % PALETTES.length];
  return {
    className: 'g-art',
    style: { '--art-base': base, '--art-ink': ink },
  };
}

export function peopleLine(course) {
  if (course.trainerNames?.length) return `With ${course.trainerNames.join(', ')}`;
  if (course.creatorName) return `Created by ${course.creatorName}`;
  return 'Trainer assigned when a class opens';
}
