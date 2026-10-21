import { useState } from 'react';
import { Link } from 'react-router-dom';
import Modal from './Modal';
import { importantNotificationTypes, safeNotificationPath, useMemberNotifications } from './memberNotificationsState';
import { formatUtcDateTime } from '../pages/businessPresentation';

function ImportantNotice({ item, onDismiss }) {
  const path = item ? safeNotificationPath(item.targetPath) : null;
  return <Modal open={Boolean(item)} title={item?.title} onClose={onDismiss} width={460}>
    {item ? <div className="notification-popup">
      <p className="notification-popup-eyebrow">Important update</p>
      <p>{item.message}</p>
      <time dateTime={item.createdAt}>{formatUtcDateTime(item.createdAt)}</time>
      <div className="modal-actions">
        {path ? <Link className="btn btn-ghost" to={path} onClick={onDismiss}>Open related page</Link> : null}
        <button type="button" className="btn btn-primary" onClick={onDismiss} data-modal-autofocus>Got it</button>
      </div>
    </div> : null}
  </Modal>;
}

export default function MemberNotifications() {
  const notifications = useMemberNotifications();
  const [open, setOpen] = useState(false);
  const [dismissed, setDismissed] = useState(() => new Set());
  if (!notifications) return null;
  const { data, loading, error, refresh, markRead, busy, actionError } = notifications;
  const unread = data?.unreadCount ?? 0;
  const important = open ? null : data?.items?.find((item) => !item.isRead && importantNotificationTypes.has(item.type) && !dismissed.has(item.id));
  function dismissImportant() {
    setDismissed((ids) => new Set(ids).add(important.id));
    void markRead(important.id);
  }
  return <div className="member-notifications">
    <button type="button" className="notif-badge" aria-label={`Notifications, ${unread} unread`} aria-haspopup="dialog" onClick={() => setOpen(true)}>
      <svg aria-hidden="true" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.7"><path d="M18 8a6 6 0 0 0-12 0c0 7-3 7-3 9h18c0-2-3-2-3-9Z" /><path d="M10 21h4" /></svg>
      {unread > 0 ? <span className="notification-count">{unread > 99 ? '99+' : unread}</span> : null}
    </button>
    <ImportantNotice item={important} onDismiss={dismissImportant} />
    <Modal open={open} title="Notifications" onClose={() => setOpen(false)} width={560}>
      <div className="notifications-dialog">
        <div className="notification-tools"><span>{unread} unread</span><span><button className="btn btn-ghost btn-sm" onClick={refresh} disabled={loading || busy}>Refresh</button><button className="btn btn-ghost btn-sm" onClick={() => markRead()} disabled={!unread || busy || loading}>Mark all as read</button></span></div>
        {loading ? <p role="status" className="card-body">Loading notifications…</p> : error ? <div className="card-body"><p role="alert">{error.message}</p><button className="btn btn-primary" onClick={refresh}>Retry</button></div> : <div className="notification-list">
          {!data?.items?.length ? <p className="card-body">No notifications yet.</p> : data.items.map((item) => <article key={item.id} className={`notification-item${item.isRead ? '' : ' unread'}`}>
            <div className="certificate-heading"><strong>{item.title}</strong><span className="pill">{item.isRead ? 'Read' : 'Unread'}</span></div>
            <p>{item.message}</p><time dateTime={item.createdAt}>{formatUtcDateTime(item.createdAt)}</time>
            <div className="notification-item-actions">
              {safeNotificationPath(item.targetPath) ? <Link to={safeNotificationPath(item.targetPath)} onClick={() => { if (!item.isRead) void markRead(item.id); setOpen(false); }}>Open related page</Link> : null}
              {!item.isRead ? <button className="btn btn-ghost btn-sm" type="button" disabled={busy} onClick={() => markRead(item.id)}>Mark as read</button> : null}
            </div>
          </article>)}
        </div>}
        {actionError ? <p className="card-body" role="alert">{actionError}</p> : null}
      </div>
    </Modal>
  </div>;
}
