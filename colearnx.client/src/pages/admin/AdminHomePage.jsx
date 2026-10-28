import { Link } from 'react-router-dom';
import { adminAuditApi, adminOverviewApi } from '../../api';
import useAdminQuery from './useAdminQuery';
import AdminDataState from './AdminDataState';
import { pendingTotal, useAdminPending } from './adminPending';
import { actionLabel, auditActor, formatAuditTime, resultClass, resultLabel } from './adminDisplay';

const QUEUE_CARDS = [
  { key: 'roleRequests', number: '01 / Access', title: 'Role requests', to: '/admin/approvals?queue=roles',
    detail: 'Trainer and Creator applications awaiting a decision.', link: 'Open role requests' },
  { key: 'courses', number: '02 / Catalogue', title: 'Courses to review', to: '/admin/approvals?queue=courses',
    detail: 'Submitted courses awaiting publication or feedback.', link: 'Open course reviews' },
  { key: 'materialVersions', number: '03 / Materials', title: 'Material versions', to: '/admin/approvals?queue=materials',
    detail: 'New material versions waiting before Trainers can use them.', link: 'Open material versions' },
  { key: 'certificates', number: '04 / Certificates', title: 'Certificates', to: '/admin/approvals?queue=certificates',
    detail: 'Trainer-approved certificates awaiting final issue.', link: 'Open certificates' },
  { key: 'disputes', number: '05 / Disputes', title: 'Disputes', to: '/admin/disputes',
    detail: 'Open disputes awaiting a resolution.', link: 'Open disputes' },
];

async function loadOverview(token, _queryKey, signal) {
  const [counts, activity] = await Promise.all([
    adminOverviewApi.pendingCounts(token, signal),
    adminAuditApi.list(token, 5, signal),
  ]);
  return { counts, activity, loadedAt: new Date().toISOString() };
}

export default function AdminHomePage() {
  const { data, loading, error, refresh: refreshOverview } = useAdminQuery(loadOverview);
  const pending = useAdminPending();
  const refresh = () => {
    refreshOverview();
    pending?.refresh();
  };
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
          {QUEUE_CARDS.map((card) => (
            <Link key={card.key} className="admin-queue-card" to={card.to}>
              <span className="admin-queue-number">{card.number}</span>
              <strong className="admin-queue-total">{data.counts[card.key]}</strong>
              <h2>{card.title}</h2>
              <p>{card.detail}</p>
              <span className="admin-queue-link">{card.link} <span aria-hidden="true">↗</span></span>
            </Link>
          ))}
        </section>
        {pendingTotal(data.counts) === 0 ? (
          <p className="admin-review-notice" role="status">All caught up. Nothing is waiting for review.</p>
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
                  <span className={`admin-status ${resultClass(log.result)}`}>{resultLabel(log.result)}</span>
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
