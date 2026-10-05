import { useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import useAdminAuth from '../../auth/useAdminAuth';
import { adminLaterPhaseApi } from '../../api/adminLaterPhase';
import AdminDataState from './AdminDataState';
import useAdminQuery from './useAdminQuery';
import { loadAdminUsers } from './adminUserLookup';
import { formatCount, formatUtcDateTime } from '../businessPresentation';

async function loadLedger(token, key, signal) {
  return adminLaterPhaseApi.ledger(token, key ? JSON.parse(key) : {}, signal);
}

const initialAdjustment = { userId: '', delta: '', reason: '' };

export default function AdminCreditLedgerPage() {
  const { token } = useAdminAuth();
  const [params] = useSearchParams();
  const initialUserId = params.get('userId') || '';
  const [filters, setFilters] = useState({ search: '', type: '' });
  const [applied, setApplied] = useState({ search: '', type: '' });
  const [adjustment, setAdjustment] = useState({ ...initialAdjustment, userId: initialUserId });
  const [userSearch, setUserSearch] = useState(initialUserId);
  const [appliedUserSearch, setAppliedUserSearch] = useState(initialUserId);
  const [operationKey, setOperationKey] = useState(() => crypto.randomUUID());
  const [mutationError, setMutationError] = useState(null);
  const [notice, setNotice] = useState('');
  const [busy, setBusy] = useState(false);
  const query = useAdminQuery(loadLedger, JSON.stringify(applied));
  const userQuery = useAdminQuery(loadAdminUsers, appliedUserSearch);
  const selectedUser = userQuery.data?.find((user) => user.id === Number(adjustment.userId));
  const delta = Number(adjustment.delta);
  const canAdjust = selectedUser && !userQuery.loading && !userQuery.error && adjustment.reason.trim()
    && adjustment.delta !== '' && delta !== 0
    && Number.isInteger(delta) && Math.abs(delta) <= 10000 && selectedUser.creditBalance + delta >= 0;

  function checkCurrentRecords() {
    query.refresh();
    userQuery.refresh();
    setMutationError(null);
  }

  async function submitAdjustment(event) {
    event.preventDefault();
    if (busy || !canAdjust || mutationError) return;
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
      setNotice(`${result.delta > 0 ? '+' : result.delta < 0 ? '−' : ''}${formatCount(Math.abs(result.delta), 'credit')} applied. New balance: ${formatCount(result.balanceAfter, 'credit')}.`);
      query.refresh();
      userQuery.refresh();
    } catch (error) {
      setMutationError(error);
    } finally {
      setBusy(false);
    }
  }

  return <div className="admin-overview later-page">
    <header className="admin-review-header"><div><p className="admin-form-eyebrow">Finance controls</p><h1>Credit ledger</h1><p>Trace every balance change and apply reasoned, audited corrections.</p></div><button className="btn btn-ghost" type="button" onClick={query.refresh} disabled={query.loading}>Refresh</button></header>
    <form className="admin-audit-filters" onSubmit={(event) => { event.preventDefault(); setApplied(filters); }}><label>Search<input value={filters.search} placeholder="Name, email or description" onChange={(event) => setFilters({ ...filters, search: event.target.value })} /></label><label>Transaction type<select value={filters.type} onChange={(event) => setFilters({ ...filters, type: event.target.value })}><option value="">All types</option><option>TopUp</option><option>Enrolment</option><option>Hold</option><option>Capture</option><option>Release</option><option>Forfeit</option><option>Refund</option><option>Royalty</option><option>AdminAdjustment</option></select></label><button className="btn btn-primary">Apply filters</button></form>
    <AdminDataState loading={query.loading} error={query.error} onRetry={query.refresh} />
    <AdminDataState error={mutationError} operation="Credit adjustment" onRetry={checkCurrentRecords} />
    {notice ? <p className="admin-review-notice" role="status">{notice}</p> : null}
    {query.data ? <div className="later-table-wrap admin-ledger-table"><table className="later-table"><thead><tr><th>Date (UTC)</th><th>User</th><th>Type</th><th>Description</th><th>Change</th><th>Available</th><th>On hold</th><th>Reference</th></tr></thead><tbody>{query.data.map((item) => <tr key={item.id}><td>{formatUtcDateTime(item.createdAt)}</td><td><strong>{item.userName}</strong><small>{item.userEmail} · #{item.userId}</small></td><td>{item.type}</td><td>{item.description}</td><td className={item.delta < 0 ? 'later-negative' : 'later-positive'}>{item.delta > 0 ? '+' : item.delta < 0 ? '−' : ''}{formatCount(Math.abs(item.delta), 'credit')}</td><td>{formatCount(item.balanceAfter, 'credit')}</td><td>{item.heldAfter == null ? '—' : formatCount(item.heldAfter, 'credit')}</td><td>{item.relatedDisputeId ? `DSP-${item.relatedDisputeId}` : item.relatedEnrollmentId ? `ENR-${item.relatedEnrollmentId}` : '—'}</td></tr>)}</tbody></table>{!query.data.length ? <div className="admin-review-state">No ledger entries match these filters.</div> : null}</div> : null}
    <section className="later-panel"><h2>Find the adjustment account</h2><form className="admin-audit-filters" onSubmit={(event) => { event.preventDefault(); setAppliedUserSearch(userSearch.trim()); }}><label>Account search<input value={userSearch} maxLength={254} placeholder="Name, email or user ID" onChange={(event) => setUserSearch(event.target.value)} /></label><button className="btn btn-ghost">Find account</button></form><AdminDataState loading={userQuery.loading} error={userQuery.error} onRetry={userQuery.refresh} /></section>
    <form className="later-panel admin-adjustment-form" onSubmit={submitAdjustment}><div className="admin-section-heading"><div><h2>Manual credit adjustment</h2><p>Select an identified account and review the balance before applying a correction.</p></div></div><div className="later-form-grid"><label>Account<select required value={adjustment.userId} onChange={(event) => setAdjustment({ ...adjustment, userId: event.target.value })} disabled={busy || Boolean(mutationError)}><option value="">Choose an account</option>{(userQuery.data || []).map((user) => <option key={user.id} value={user.id}>{user.fullName} · {user.email} · #{user.id}</option>)}</select></label><label>Credit delta<input type="number" min="-10000" max="10000" required value={adjustment.delta} disabled={busy || Boolean(mutationError)} onChange={(event) => setAdjustment({ ...adjustment, delta: event.target.value })} /></label><label className="later-form-wide">Reason<textarea required maxLength="512" value={adjustment.reason} disabled={busy || Boolean(mutationError)} onChange={(event) => setAdjustment({ ...adjustment, reason: event.target.value })} /></label></div>
      {selectedUser ? <div className="admin-review-summary" aria-live="polite"><strong>{selectedUser.fullName} · {selectedUser.email} · #{selectedUser.id}</strong><p>Available: {formatCount(selectedUser.creditBalance, 'credit')} · On hold: {formatCount(selectedUser.heldCredits, 'credit')}</p><p>After adjustment: {formatCount(selectedUser.creditBalance, 'credit')} {delta >= 0 ? '+' : '−'} {formatCount(Math.abs(delta), 'credit')} = {formatCount(selectedUser.creditBalance + delta, 'credit')} available</p></div> : <p>Select an account from the search results to preview this adjustment.</p>}
      <div className="later-actions"><span>Negative adjustments cannot take a balance below zero.</span><button className="btn btn-primary" disabled={busy || !canAdjust || Boolean(mutationError)}>{busy ? 'Applying…' : 'Apply adjustment'}</button></div></form>
  </div>;
}
