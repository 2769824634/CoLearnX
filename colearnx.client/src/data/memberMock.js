export const MEMBER_NAV = [
  { id: 'home', label: 'Home' },
  { id: 'catalog', label: 'Courses' },
  { id: 'my-programs', label: 'My Programs' },
  { id: 'payment', label: 'Payment' },
  { id: 'badges', label: 'Badges' },
  { id: 'profile', label: 'My Account' },
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
