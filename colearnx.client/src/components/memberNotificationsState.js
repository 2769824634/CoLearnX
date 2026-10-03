import { createContext, useContext } from 'react';

export const MemberNotificationsContext = createContext(null);
export const useMemberNotifications = () => useContext(MemberNotificationsContext);

const destinations = new Set(['/member/home', '/member/programs', '/member/badges', '/member/payment', '/member/disputes', '/member/account', '/member/courses', '/trainer/courses', '/trainer/learners', '/creator/courses/intake-applications']);
export function safeNotificationPath(path) {
  if (typeof path !== 'string' || path !== path.trim()) return null;
  const intakeDetail = /^\/(?:trainer\/courses\/intakes|creator\/courses\/intake-applications)\/[1-9]\d*$/.test(path);
  return destinations.has(path) || intakeDetail ? path : null;
}
