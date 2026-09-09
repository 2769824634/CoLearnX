import { useState } from 'react';
import { adminAuditApi } from '../../api';
import useAdminQuery from './useAdminQuery';
import AdminDataState from './AdminDataState';
import { actionLabel, auditActor, formatAuditTime, resultClass } from './adminDisplay';

const PAGE_SIZE = 25;
const EMPTY_FILTERS = { entityType: '', result: '', actorType: '' };

function loadAudit(token, queryKey, signal) {
  return adminAuditApi.query(token, JSON.parse(queryKey), signal);
}

export default function AdminAuditLogsPage() {
  const [filters, setFilters] = useState(EMPTY_FILTERS);
  const [cursors, setCursors] = useState([null]);
  const beforeId = cursors[cursors.length - 1];
  const queryKey = JSON.stringify({ ...filters, beforeId, limit: PAGE_SIZE + 1 });
  const { data, loading, error, refresh } = useAdminQuery(loadAudit, queryKey);
  const logs = data?.slice(0, PAGE_SIZE) ?? [];
  const hasNext = data?.length > PAGE_SIZE;

  function updateFilter(key, value) {
    setFilters((previous) => ({ ...previous, [key]: value }));
    setCursors([null]);
  }

  function refreshLatest() {
    setCursors([null]);
    refresh();
  }

  return (
    <div className="admin-audit-page">
      <header className="admin-review-header">
        <div>
          <p className="admin-form-eyebrow">Activity record</p>
          <h1>Audit log</h1>
          <p>Read-only records of who acted, what changed and the recorded outcome.</p>
        </div>
        <button type="button" className="btn btn-ghost" onClick={refreshLatest} disabled={loading}>Refresh latest</button>
      </header>
      <section className="admin-review-workspace" aria-label="Audit records">
        <div className="admin-audit-filters">
          <label>Resource
            <select value={filters.entityType} onChange={(event) => updateFilter('entityType', event.target.value)}>
              <option value="">All resources</option><option value="RoleRequest">Role requests</option>
              <option value="Course">Courses</option><option value="AdminAccount">Admin accounts</option>
            </select>
          </label>
          <label>Outcome
            <select value={filters.result} onChange={(event) => updateFilter('result', event.target.value)}>
              <option value="">All outcomes</option>
              {['Approved', 'Published', 'Rejected', 'Succeeded', 'Failed'].map((value) => <option key={value}>{value}</option>)}
            </select>
          </label>
          <label>Actor
            <select value={filters.actorType} onChange={(event) => updateFilter('actorType', event.target.value)}>
              <option value="">All actors</option><option value="Admin">Administrators</option><option value="User">Users</option>
            </select>
          </label>
          <button type="button" className="btn btn-ghost" onClick={() => { setFilters(EMPTY_FILTERS); setCursors([null]); }}>
            Clear filters
          </button>
        </div>
        <AdminDataState loading={loading} error={error} onRetry={refresh} />
        {data && logs.length === 0 ? <div className="admin-review-state" role="status">
          <strong>No matching activity</strong><span>Try another filter or refresh for new records.</span>
        </div> : null}
        {logs.length ? <div className="admin-review-table-wrap" tabIndex={0} role="region" aria-label="Scrollable audit table">
          <table className="admin-review-table admin-audit-table">
            <caption className="sr-only">Audit events, newest recorded first. All times are UTC.</caption>
            <thead><tr><th scope="col">Event / Time</th><th scope="col">Actor</th><th scope="col">Action / Resource</th>
              <th scope="col">Outcome</th><th scope="col">Reason</th></tr></thead>
            <tbody>{logs.map((log) => <tr key={log.id}>
              <td><strong>#{log.id}</strong><span>{formatAuditTime(log.createdAt)}</span></td>
              <td>{auditActor(log)}</td>
              <td><strong>{actionLabel(log.action)}</strong><span>{log.entityType} {log.entityId ? `#${log.entityId}` : ''}</span></td>
              <td><span className={`admin-status ${resultClass(log.result)}`}>{log.result}</span></td>
              <td className="admin-audit-reason">{log.reason || '—'}</td>
            </tr>)}</tbody>
          </table>
        </div> : null}
        <div className="admin-audit-pagination">
          <span role="status">Page {cursors.length}{data ? ` · ${logs.length} records` : ''}</span>
          <div>
            <button type="button" className="btn btn-ghost" disabled={loading || cursors.length === 1}
              onClick={() => setCursors((previous) => previous.slice(0, -1))}>Newer</button>
            <button type="button" className="btn btn-ghost" disabled={loading || !hasNext}
              onClick={() => setCursors((previous) => [...previous, logs[logs.length - 1].id])}>Older</button>
          </div>
        </div>
      </section>
    </div>
  );
}
