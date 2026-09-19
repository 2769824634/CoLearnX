import { createContext, useContext } from 'react';

export const MemberNotificationsContext = createContext(null);
export const useMemberNotifications = () => useContext(MemberNotificationsContext);

const destinations = new Set(['/member/home', '/member/programs', '/member/badges', '/member/payment', '/member/disputes', '/member/account', '/member/courses']);
export function safeNotificationPath(path) {
  return typeof path === 'string' && destinations.has(path) ? path : null;
}
