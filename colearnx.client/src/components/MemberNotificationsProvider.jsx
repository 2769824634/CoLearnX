import { useEffect, useRef, useState } from 'react';
import { notificationsApi } from '../api';
import useTrainerQuery from '../pages/trainer/useTrainerQuery';
import { MemberNotificationsContext } from './memberNotificationsState';
import { connectNotificationRealtime } from './notificationRealtime';

const loadNotifications = (token, _key, signal) => notificationsApi.my(token, signal);
const POLL_MS = 60_000;

export function MemberNotificationsProvider({ children }) {
  const query = useTrainerQuery(loadNotifications);
  const [action, setAction] = useState(null);
  const inFlight = useRef(false);
  const latest = useRef({ setData: query.setData, data: query.data });
  const pushed = useRef([]);
  const currentAction = action?.token === query.token ? action : null;
  function merge() {
    const current = latest.current.data;
    if (!current || !pushed.current.length) return;
    const known = new Set((current.items || []).map((item) => item.id));
    const extra = pushed.current.filter((item) => !known.has(item.id));
    pushed.current = extra;
    if (!extra.length) return;
    const items = [...extra, ...(current.items || [])];
    latest.current.setData({ items, unreadCount: items.filter((item) => !item.isRead).length });
  }
  useEffect(() => {
    latest.current = { setData: query.setData, data: query.data };
    merge();
  });
  useEffect(() => {
    if (!query.token) return undefined;
    const controller = new AbortController();
    const realtime = connectNotificationRealtime(query.token, (item) => {
      if (item?.id == null || pushed.current.some((existing) => existing.id === item.id)) return;
      pushed.current = [item, ...pushed.current];
      merge();
    });
    const timer = setInterval(() => {
      if (realtime.connected || document.visibilityState === 'hidden' || inFlight.current) return;
      notificationsApi.my(query.token, controller.signal).then((data) => {
        if (!controller.signal.aborted && !inFlight.current) latest.current.setData(data);
      }, () => {});
    }, POLL_MS);
    return () => { clearInterval(timer); controller.abort(); void realtime.stop(); };
  }, [query.token]);
  async function markRead(id) {
    if (inFlight.current) return false;
    inFlight.current = true;
    setAction({ token: query.token, busy: true });
    try {
      if (id == null) await notificationsApi.readAll(query.token);
      else await notificationsApi.read(id, query.token);
      const items = (latest.current.data?.items || []).map((item) => id == null || item.id === id ? { ...item, isRead: true } : item);
      latest.current.setData({ items, unreadCount: items.filter((item) => !item.isRead).length });
      setAction(null);
      return true;
    } catch (error) {
      setAction({ token: query.token, error: error.message || 'Could not mark notification as read. Try again.' });
      return false;
    } finally { inFlight.current = false; }
  }
  return <MemberNotificationsContext.Provider value={{ ...query, markRead, busy: Boolean(currentAction?.busy), actionError: currentAction?.error }}>{children}</MemberNotificationsContext.Provider>;
}
