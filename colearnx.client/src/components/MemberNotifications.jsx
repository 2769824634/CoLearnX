import { useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { safeNotificationPath, useMemberNotifications } from './memberNotificationsState';
import { utcDate } from '../utils/utcDates';

export default function MemberNotifications() {
  const notifications = useMemberNotifications();
  const [open, setOpen] = useState(false);
  const trigger = useRef(null);
  if (!notifications) return null;
  const { data, loading, error, refresh, markRead, busy, actionError } = notifications;
  const unread = data?.unreadCount ?? 0;
  function close() { setOpen(false); trigger.current?.focus(); }
  return <div className="member-notifications" onKeyDown={(event) => { if (event.key === 'Escape') close(); }}>
    <button ref={trigger} type="button" className="notif-badge" aria-label={`Notifications, ${unread} unread`} aria-expanded={open} aria-controls="member-notifications-panel" onClick={() => setOpen((value) => !value)}>
      <svg aria-hidden="true" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.7"><path d="M18 8a6 6 0 0 0-12 0c0 7-3 7-3 9h18c0-2-3-2-3-9Z" /><path d="M10 21h4" /></svg>
      {unread > 0 ? <span className="notification-count">{unread > 99 ? '99+' : unread}</span> : null}
    </button>
    {open ? <section id="member-notifications-panel" className="notifications-panel" aria-label="Your notifications">
      <div className="card-header"><h2>Notifications</h2><button className="btn btn-ghost btn-sm" type="button" onClick={close}>Close</button></div>
      <div className="notification-tools"><span>{unread} unread</span><button className="btn btn-ghost btn-sm" onClick={refresh} disabled={loading || busy}>Refresh</button><button className="btn btn-ghost btn-sm" onClick={() => markRead()} disabled={!unread || busy || loading}>Mark all as read</button></div>
      {loading ? <p role="status" className="card-body">Loading notifications…</p> : error ? <div className="card-body"><p role="alert">{error.message}</p><button className="btn btn-primary" onClick={refresh}>Retry</button></div> : <div className="notification-list">
        {!data?.items?.length ? <p className="card-body">No notifications yet.</p> : data.items.map((item) => <article key={item.id} className={`notification-item${item.isRead ? '' : ' unread'}`}>
          <div className="certificate-heading"><strong>{item.title}</strong><span className="pill">{item.isRead ? 'Read' : 'Unread'}</span></div>
          <p>{item.message}</p><time dateTime={item.createdAt}>{utcDate(item.createdAt).toLocaleString()}</time>
          <div className="notification-item-actions">
            {safeNotificationPath(item.targetPath) ? <Link to={safeNotificationPath(item.targetPath)} onClick={() => { if (!item.isRead) void markRead(item.id); setOpen(false); }}>Open related page</Link> : null}
            {!item.isRead ? <button className="btn btn-ghost btn-sm" type="button" disabled={busy} onClick={() => markRead(item.id)}>Mark as read</button> : null}
          </div>
        </article>)}
      </div>}
      {actionError ? <p className="card-body" role="alert">{actionError}</p> : null}
    </section> : null}
  </div>;
}
