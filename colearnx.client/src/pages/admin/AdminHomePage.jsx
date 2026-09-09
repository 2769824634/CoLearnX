import { Link } from 'react-router-dom';
import { adminAuditApi, adminCourseReviewsApi, adminRoleRequestsApi } from '../../api';
import useAdminQuery from './useAdminQuery';
import AdminDataState from './AdminDataState';
import { actionLabel, auditActor, formatAuditTime, resultClass } from './adminDisplay';

async function loadOverview(token, _queryKey, signal) {
  const [roles, courses, activity] = await Promise.all([
    adminRoleRequestsApi.list(token, 'Pending', signal),
    adminCourseReviewsApi.list(token, 'PendingApproval', signal),
    adminAuditApi.list(token, 5, signal),
  ]);
  return { roles, courses, activity, loadedAt: new Date().toISOString() };
}

export default function AdminHomePage() {
  const { data, loading, error, refresh } = useAdminQuery(loadOverview);
  return (
    <div className="admin-overview">
      <header className="admin-review-header">
        <div>
          <p className="admin-form-eyebrow">Administration</p>
          <h1>Operations overview</h1>
          <p>Review access, publish courses and follow the record of every decision.</p>
        </div>
        <button className="btn btn-ghost" type="button" onClick={refresh} disabled={loading}>Refresh</button>
      </header>
      <AdminDataState loading={loading} error={error} onRetry={refresh} />
      {data ? <>
        <section className="admin-overview-queues" aria-label="Pending review queues">
          <Link className="admin-queue-card" to="/admin/approvals?queue=roles">
            <span className="admin-queue-number">01 / Access</span>
            <strong className="admin-queue-total">{data.roles.length}</strong>
            <h2>Role requests</h2>
            <p>Trainer and Creator applications awaiting a decision.</p>
            <span className="admin-queue-link">Open role requests <span aria-hidden="true">↗</span></span>
          </Link>
          <Link className="admin-queue-card" to="/admin/approvals?queue=courses">
            <span className="admin-queue-number">02 / Catalogue</span>
            <strong className="admin-queue-total">{data.courses.length}</strong>
            <h2>Courses to review</h2>
            <p>Submitted courses awaiting publication or feedback.</p>
            <span className="admin-queue-link">Open course reviews <span aria-hidden="true">↗</span></span>
          </Link>
        </section>
        {data.roles.length + data.courses.length === 0 ? (
          <p className="admin-review-notice" role="status">All caught up. No role requests or courses are awaiting review.</p>
        ) : null}
        <section className="admin-activity-panel" aria-labelledby="recent-activity-title">
          <div className="admin-section-heading">
            <div><h2 id="recent-activity-title">Recent activity</h2><p>Latest 5 recorded events</p></div>
            <Link to="/admin/audit">View audit log <span aria-hidden="true">↗</span></Link>
          </div>
          {data.activity.length ? (
            <ol className="admin-activity-list">
              {data.activity.map((log) => <li key={log.id}>
                <span className="admin-activity-id">#{log.id}</span>
                <div className="admin-activity-description">
                  <strong>{actionLabel(log.action)}</strong>
                  <span>{auditActor(log)} · {log.entityType} {log.entityId ? `#${log.entityId}` : ''}</span>
                  {log.reason ? <p>{log.reason}</p> : null}
                </div>
                <div className="admin-activity-meta">
                  <span className={`admin-status ${resultClass(log.result)}`}>{log.result}</span>
                  <time>{formatAuditTime(log.createdAt)}</time>
                </div>
              </li>)}
            </ol>
          ) : <div className="admin-review-state"><strong>No activity yet</strong><span>Recorded actions will appear here.</span></div>}
        </section>
        <p className="admin-updated-at">Updated {formatAuditTime(data.loadedAt)}</p>
      </> : null}
    </div>
  );
}
