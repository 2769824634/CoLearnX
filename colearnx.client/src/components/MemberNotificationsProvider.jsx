import { useRef, useState } from 'react';
import { notificationsApi } from '../api';
import useTrainerQuery from '../pages/trainer/useTrainerQuery';
import { MemberNotificationsContext } from './memberNotificationsState';

const loadNotifications = (token, _key, signal) => notificationsApi.my(token, signal);

export function MemberNotificationsProvider({ children }) {
  const query = useTrainerQuery(loadNotifications);
  const [action, setAction] = useState(null);
  const inFlight = useRef(false);
  const currentAction = action?.token === query.token ? action : null;
  async function markRead(id) {
    if (inFlight.current) return false;
    inFlight.current = true;
    setAction({ token: query.token, busy: true });
    try {
      if (id == null) await notificationsApi.readAll(query.token);
      else await notificationsApi.read(id, query.token);
      const items = (query.data?.items || []).map((item) => id == null || item.id === id ? { ...item, isRead: true } : item);
      query.setData({ items, unreadCount: items.filter((item) => !item.isRead).length });
      setAction(null);
      return true;
    } catch (error) {
      setAction({ token: query.token, error: error.message || 'Could not mark notification as read. Try again.' });
      return false;
    } finally { inFlight.current = false; }
  }
  return <MemberNotificationsContext.Provider value={{ ...query, markRead, busy: Boolean(currentAction?.busy), actionError: currentAction?.error }}>{children}</MemberNotificationsContext.Provider>;
}
