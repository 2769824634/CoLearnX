export const MEMBER_NAV = [
  { id: 'home', label: 'Home' },
  { id: 'catalog', label: 'Courses' },
  { id: 'my-programs', label: 'My Programs' },
  { id: 'payment', label: 'Payment' },
  { id: 'badges', label: 'Badges' },
  { id: 'profile', label: 'My Account' },
];

export const COURSES = [
  {
    id: 'INFT2051',
    code: 'INFT 2051',
    title: 'UI/UX Design Fundamentals',
    trainer: 'Jane Smith',
    credits: 30,
    level: 'Beginner',
    topic: 'Design',
    sessions: [
      { id: 's1', label: 'Session 1', when: '20 May 2026 · 9:30am – 6:30pm', seats: 12 },
      { id: 's2', label: 'Session 2', when: '10 Jun 2026 · 9:30am – 6:30pm', seats: 8 },
      { id: 's3', label: 'Session 3', when: '5 Jul 2026 · 2:00pm – 8:00pm', seats: 0 },
    ],
  },
  {
    id: 'INFT3030',
    code: 'INFT 3030',
    title: 'Cybersecurity Essentials',
    trainer: 'Gu Yincheng',
    credits: 25,
    level: 'Intermediate',
    topic: 'Programming',
    sessions: [{ id: 's1', label: 'Session 1', when: '22 May 2026 · 2:00pm – 6:00pm', seats: 15 }],
  },
  {
    id: 'INFT2002',
    code: 'INFT 2002',
    title: 'Frontend React Bootcamp',
    trainer: 'Emily Wong',
    credits: 20,
    level: 'Beginner',
    topic: 'Programming',
    sessions: [{ id: 's1', label: 'Session 1', when: '15 Jun 2026 · 10:00am – 4:00pm', seats: 20 }],
  },
  {
    id: 'INFT4010',
    code: 'INFT 4010',
    title: 'UX Research Methods',
    trainer: 'Jane Smith',
    credits: 35,
    level: 'Intermediate',
    topic: 'Design',
    sessions: [{ id: 's1', label: 'Session 1', when: '1 Jul 2026 · 9:00am – 5:00pm', seats: 10 }],
  },
  {
    id: 'INFT3950',
    code: 'INFT 3950',
    title: 'Advanced Cloud Architecture',
    trainer: 'David Tan',
    credits: 40,
    level: 'Advanced',
    topic: 'Programming',
    sessions: [{ id: 's1', label: 'Session 1', when: '28 May 2026 · 9:30am – 5:30pm', seats: 6 }],
  },
  {
    id: 'INFT2100',
    code: 'INFT 2100',
    title: 'Data Analytics Intro',
    trainer: 'Tom Nguyen',
    credits: 28,
    level: 'Beginner',
    topic: 'Business',
    sessions: [{ id: 's1', label: 'Session 1', when: '8 Jul 2026 · 2:00pm – 6:00pm', seats: 18 }],
  },
];

export const CREDIT_PACKAGES = [
  { credits: 20, price: '$10', best: false },
  { credits: 50, price: '$25', best: false },
  { credits: 120, price: '$50', best: true },
  { credits: 300, price: '$100', best: false },
];

export const CERT_STAGES = [
  { n: 1, name: 'Foundation', date: '12 Mar 2026', done: true },
  { n: 2, name: 'Intermediate', date: '28 Apr 2026', done: true },
  { n: 3, name: 'Advanced', date: '22 May 2026', done: true },
  { n: 4, name: 'Graduation', date: '12 Jun 2026', done: true },
];

export function maskEmail(email) {
  const [local, domain] = email.split('@');
  if (!local || !domain) return email;
  const shown = local.length <= 2 ? `${local[0]}****` : `${local[0]}****${local[local.length - 1]}`;
  return `${shown}@${domain}`;
}

export function maskPhone(phone) {
  if (!phone || phone.length < 4) return '****';
  return `****${phone.slice(-4)}`;
}

export const initialMemberState = {
  credits: 120,
  user: {
    fullName: 'Huang Yousheng',
    displayName: 'Yousheng',
    email: 'huang.yousheng@colearnx.com',
    phone: '12345678',
    bio: 'The sole disciple of the Way of Mercilessness',
    learningGoals: 'Career switch · UI/UX & Cybersecurity',
    specialisations: 'UI/UX Design, Wireframing, Usability',
    trainerHeadline: 'Workshop facilitator · design thinking',
  },
  identityVisibility: { member: true, trainer: true, creator: false },
  enrolled: [
    { id: 'INFT2051', progress: 65, status: 'active' },
    { id: 'INFT3030', progress: 12, status: 'active' },
    { id: 'INFT2002', progress: 100, status: 'completed', cert: 'Foundation' },
  ],
  wishlist: ['INFT3030', 'INFT4010'],
  ledger: [
    { date: '12 Jun 2026', type: 'Top-Up', desc: 'PayPal package +120', delta: 120, balance: 120 },
    { date: '10 Jun 2026', type: 'Enrolment', desc: 'INFT 3030 — Cybersecurity Essentials', delta: -25, balance: 0 },
    { date: '08 Jun 2026', type: 'Enrolment', desc: 'INFT 2051 — UI/UX Design Fundamentals', delta: -30, balance: 25 },
    { date: '03 May 2026', type: 'Top-Up', desc: 'PayPal package +50', delta: 50, balance: 55 },
    { date: '01 May 2026', type: 'Refund', desc: 'INFT 1999 cancelled (Admin approved)', delta: 15, balance: 5 },
  ],
  prefs: { emailNotif: true, darkMode: false },
};
