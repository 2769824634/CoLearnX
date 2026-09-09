import { useState } from 'react';
import useAdminAuth from '../../auth/useAdminAuth';
import { adminLaterPhaseApi } from '../../api/adminLaterPhase';
import AdminDataState from './AdminDataState';
import useAdminQuery from './useAdminQuery';

async function loadLedger(token, key, signal) {
  return adminLaterPhaseApi.ledger(token, key ? JSON.parse(key) : {}, signal);
}

const initialAdjustment = { userId: '', delta: '', reason: '' };

export default function AdminCreditLedgerPage() {
  const { token } = useAdminAuth();
  const [filters, setFilters] = useState({ search: '', type: '' });
  const [applied, setApplied] = useState({ search: '', type: '' });
  const [adjustment, setAdjustment] = useState(initialAdjustment);
  const [operationKey, setOperationKey] = useState(() => crypto.randomUUID());
  const [mutationError, setMutationError] = useState(null);
  const [notice, setNotice] = useState('');
  const [busy, setBusy] = useState(false);
  const query = useAdminQuery(loadLedger, JSON.stringify(applied));
  const users = [...new Map((query.data || []).map((item) => [item.userId, item])).values()];

  async function submitAdjustment(event) {
    event.preventDefault();
    if (busy) return;
    setBusy(true);
    setMutationError(null);
    setNotice('');
    try {
      const result = await adminLaterPhaseApi.adjustCredits(token, {
        userId: Number(adjustment.userId),
        delta: Number(adjustment.delta),
        reason: adjustment.reason,
        idempotencyKey: operationKey,
      });
      setAdjustment(initialAdjustment);
      setOperationKey(crypto.randomUUID());
      setNotice(`${result.delta > 0 ? '+' : ''}${result.delta} credits applied. New balance: ${result.balanceAfter}.`);
      query.refresh();
    } catch (error) {
      setMutationError(error);
    } finally {
      setBusy(false);
    }
  }

  return <div className="admin-overview later-page">
    <header className="admin-review-header"><div><p className="admin-form-eyebrow">Finance controls</p><h1>Credit ledger</h1><p>Trace every balance change and apply reasoned, audited corrections.</p></div><button className="btn btn-ghost" type="button" onClick={query.refresh} disabled={query.loading}>Refresh</button></header>
    <form className="admin-audit-filters" onSubmit={(event) => { event.preventDefault(); setApplied(filters); }}><label>Search<input value={filters.search} placeholder="Name, email or description" onChange={(event) => setFilters({ ...filters, search: event.target.value })} /></label><label>Transaction type<select value={filters.type} onChange={(event) => setFilters({ ...filters, type: event.target.value })}><option value="">All types</option><option>TopUp</option><option>Enrolment</option><option>Refund</option><option>Royalty</option><option>AdminAdjustment</option></select></label><button className="btn btn-primary">Apply filters</button></form>
    <AdminDataState loading={query.loading} error={query.error || mutationError} onRetry={query.error ? query.refresh : () => setMutationError(null)} />
    {notice ? <p className="admin-review-notice" role="status">{notice}</p> : null}
    {query.data ? <div className="later-table-wrap admin-ledger-table"><table className="later-table"><thead><tr><th>Date</th><th>User</th><th>Type</th><th>Description</th><th>Change</th><th>Balance</th><th>Reference</th></tr></thead><tbody>{query.data.map((item) => <tr key={item.id}><td>{new Date(item.createdAt).toLocaleString()}</td><td><strong>{item.userName}</strong><small>{item.userEmail} · #{item.userId}</small></td><td>{item.type}</td><td>{item.description}</td><td className={item.delta < 0 ? 'later-negative' : 'later-positive'}>{item.delta > 0 ? '+' : ''}{item.delta}</td><td>{item.balanceAfter}</td><td>{item.relatedDisputeId ? `DSP-${item.relatedDisputeId}` : item.relatedEnrollmentId ? `ENR-${item.relatedEnrollmentId}` : '—'}</td></tr>)}</tbody></table>{!query.data.length ? <div className="admin-review-state">No ledger entries match these filters.</div> : null}</div> : null}
    <form className="later-panel admin-adjustment-form" onSubmit={submitAdjustment}><div className="admin-section-heading"><div><h2>Manual credit adjustment</h2><p>All changes require a user, non-zero delta and audit reason.</p></div></div><div className="later-form-grid"><label>User ID<input list="ledger-users" type="number" min="1" required value={adjustment.userId} onChange={(event) => setAdjustment({ ...adjustment, userId: event.target.value })} /><datalist id="ledger-users">{users.map((item) => <option key={item.userId} value={item.userId}>{item.userName} · {item.userEmail}</option>)}</datalist></label><label>Credit delta<input type="number" min="-10000" max="10000" required value={adjustment.delta} onChange={(event) => setAdjustment({ ...adjustment, delta: event.target.value })} /></label><label className="later-form-wide">Reason<textarea required maxLength="512" value={adjustment.reason} onChange={(event) => setAdjustment({ ...adjustment, reason: event.target.value })} /></label></div><div className="later-actions"><span>Negative adjustments cannot take a balance below zero.</span><button className="btn btn-primary" disabled={busy}>{busy ? 'Applying…' : 'Apply adjustment'}</button></div></form>
  </div>;
}
