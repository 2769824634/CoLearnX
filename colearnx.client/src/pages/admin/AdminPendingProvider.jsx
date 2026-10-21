import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { adminOverviewApi } from '../../api';
import { AdminPendingContext, PENDING_REFRESH_MS, approvalTotal, pendingTotal } from './adminPending';

function newItemsMessage(added) {
  return added === 1 ? '1 new item is waiting for review' : `${added} new items are waiting for review`;
}

export function AdminPendingProvider({ token, children }) {
  const [counts, setCounts] = useState(null);
  const [notice, setNotice] = useState(null);
  const latestRequest = useRef(0);
  const previous = useRef(null);

  const refresh = useCallback(() => {
    if (!token) return;
    const requestId = ++latestRequest.current;
    const apply = (next) => {
      if (requestId !== latestRequest.current || !next) return;
      const before = previous.current;
      previous.current = next;
      setCounts(next);
      const added = before ? pendingTotal(next) - pendingTotal(before) : 0;
      if (added > 0) {
        const approvalsGrew = approvalTotal(next) > approvalTotal(before);
        setNotice({ added, to: approvalsGrew ? '/admin/approvals' : '/admin/disputes' });
      }
    };
    // Counts are a hint; a failed poll keeps the last known numbers instead of interrupting the page.
    adminOverviewApi.pendingCounts(token).then(apply, () => {});
  }, [token]);

  useEffect(() => {
    refresh();
    const refreshWhenVisible = () => { if (!document.hidden) refresh(); };
    const timer = setInterval(refreshWhenVisible, PENDING_REFRESH_MS);
    document.addEventListener('visibilitychange', refreshWhenVisible);
    window.addEventListener('focus', refreshWhenVisible);
    return () => {
      clearInterval(timer);
      document.removeEventListener('visibilitychange', refreshWhenVisible);
      window.removeEventListener('focus', refreshWhenVisible);
      latestRequest.current += 1;
    };
  }, [refresh]);

  const value = useMemo(() => ({ counts, refresh }), [counts, refresh]);
  const dismiss = () => setNotice(null);

  return (
    <AdminPendingContext.Provider value={value}>
      {children}
      {notice ? (
        <div className="toast show admin-pending-toast" role="status">
          <Link to={notice.to} onClick={dismiss}>{newItemsMessage(notice.added)}</Link>
          <button type="button" aria-label="Dismiss new items notice" onClick={dismiss}>×</button>
        </div>
      ) : null}
    </AdminPendingContext.Provider>
  );
}
