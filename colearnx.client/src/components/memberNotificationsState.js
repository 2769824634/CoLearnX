import { createContext, useContext } from 'react';

export const MemberNotificationsContext = createContext(null);
export const useMemberNotifications = () => useContext(MemberNotificationsContext);

const destinations = new Set(['/member/home', '/member/programs', '/member/badges', '/member/payment', '/member/disputes', '/member/account', '/member/courses', '/trainer/courses', '/trainer/learners', '/creator/courses/intake-applications']);
const programTabs = new Set(['active', 'reserved', 'completed', 'history']);

export function safeNotificationPath(path) {
  if (typeof path !== 'string' || path !== path.trim() || path.includes('\\') || path.includes('..') || path.includes('%') || path.includes('//')) return null;
  let parsed;
  try {
    parsed = new URL(path, 'https://colearnx.local');
  } catch {
    return null;
  }
  if (parsed.origin !== 'https://colearnx.local' || parsed.username || parsed.password || parsed.hash) return null;
  const pathname = parsed.pathname;
  const intakeDetail = /^\/(?:trainer\/courses\/intakes|creator\/courses\/intake-applications)\/[1-9]\d*$/.test(pathname);
  if (!destinations.has(pathname) && !intakeDetail) return null;
  if (!parsed.search) return pathname;
  if (pathname !== '/member/programs') return null;
  const params = parsed.searchParams;
  for (const key of params.keys()) {
    if (key !== 'tab' && key !== 'enrollmentId') return null;
  }
  const tab = params.get('tab');
  if (tab != null && !programTabs.has(tab)) return null;
  const enrollmentId = params.get('enrollmentId');
  if (enrollmentId != null && !/^[1-9]\d*$/.test(enrollmentId)) return null;
  const next = new URLSearchParams();
  if (tab) next.set('tab', tab);
  if (enrollmentId) next.set('enrollmentId', enrollmentId);
  const query = next.toString();
  return query ? `${pathname}?${query}` : pathname;
}
