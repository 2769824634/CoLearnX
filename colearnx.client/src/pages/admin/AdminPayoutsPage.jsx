import { useState } from 'react';
import { adminPayoutsApi } from '../../api';
import useAdminAuth from '../../auth/useAdminAuth';
import { localDateTime } from '../creator/openClass';
import AdminDataState from './AdminDataState';
import { useAdminPending } from './adminPending';
import useAdminQuery from './useAdminQuery';

const loadPayouts = (token, status, signal) => adminPayoutsApi.list(token, status, signal);

export default function AdminPayoutsPage() {
  const { token } = useAdminAuth();
  const pending = useAdminPending();
  const [status, setStatus] = useState('Requested');
  const [reference, setReference] = useState('');
  const [selectedId, setSelectedId] = useState(null);
  const [busy, setBusy] = useState(false);
  const [operationError, setOperationError] = useState(null);
  const query = useAdminQuery(loadPayouts, status);
  const selected = query.data?.find((item) => item.id === selectedId);
  const review = async (decision) => {
    setBusy(true); setOperationError(null);
    try {
      await adminPayoutsApi.review(token, selected.id, decision, reference.trim());
      setSelectedId(null); setReference(''); query.refresh(); pending?.refresh();
    } catch (error) { setOperationError(error); }
    finally { setBusy(false); }
  };
  return <div className="admin-review-workspace">
    <header className="admin-review-header"><div><p className="admin-form-eyebrow">Offline finance</p><h1>Payout requests</h1>
      <p>Record an external payment reference or reject the request and release its earnings.</p></div>
      <label>Status<select value={status} onChange={(event) => { setStatus(event.target.value); setSelectedId(null); setReference(''); }}>
        <option>Requested</option><option>Paid</option><option>Rejected</option></select></label></header>
    <AdminDataState loading={query.loading} error={query.error || operationError} onRetry={query.refresh} operation={operationError ? 'Payout review' : null} />
    {query.data ? <div className="admin-review-layout"><section className="admin-review-list">
      {!query.data.length ? <div className="admin-review-state">No payout requests in this status.</div> : query.data.map((item) =>
        <button type="button" key={item.id} className={selectedId === item.id ? 'active' : ''} onClick={() => { setSelectedId(item.id); setReference(''); }}>
          <strong>{item.userName}</strong><span>{item.userEmail} · {item.role}</span><small>{item.credits} credits · {localDateTime(item.requestedAt)}</small></button>)}
    </section>{selected ? <section className="later-panel later-case-detail"><span className="admin-status pending">{selected.status}</span>
      <h2>Payout #{selected.id}</h2><dl><div><dt>User</dt><dd>{selected.userName} · {selected.userEmail}</dd></div><div><dt>Role</dt><dd>{selected.role}</dd></div><div><dt>Amount</dt><dd>{selected.credits} credits · ≈ ${(selected.credits).toFixed(2)}</dd></div>
        <div><dt>Method</dt><dd>{selected.payoutMethod}</dd></div></dl>{selected.status === 'Requested' ? <>
        <label>Transfer reference / rejection reason<input aria-label="Transfer reference / rejection reason" value={reference} maxLength="256" onChange={(event) => setReference(event.target.value)} /></label>
        <div className="trainer-actions"><button className="btn btn-teal" disabled={busy || !reference.trim()} onClick={() => review('Paid')}>Mark as paid</button>
          <button className="btn btn-danger" disabled={busy || !reference.trim()} onClick={() => review('Reject')}>Reject</button></div></> :
        <p>{selected.reference || 'No reference recorded.'}</p>}</section> : <section className="later-panel admin-review-state">Select a payout request.</section>}</div> : null}
  </div>;
}
