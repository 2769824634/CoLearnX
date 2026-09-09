import { useState } from 'react';
import useAdminAuth from '../../auth/useAdminAuth';
import { adminLaterPhaseApi } from '../../api/adminLaterPhase';
import AdminDataState from './AdminDataState';
import useAdminQuery from './useAdminQuery';

async function loadDisputes(token, status, signal) {
  return adminLaterPhaseApi.disputes(token, status, signal);
}

export default function AdminDisputesPage() {
  const { token } = useAdminAuth();
  const [status, setStatus] = useState('Open');
  const [selectedId, setSelectedId] = useState(null);
  const [refundCredits, setRefundCredits] = useState('');
  const [reason, setReason] = useState('');
  const [operationKey, setOperationKey] = useState(() => crypto.randomUUID());
  const [busy, setBusy] = useState(false);
  const [mutationError, setMutationError] = useState(null);
  const [notice, setNotice] = useState('');
  const query = useAdminQuery(loadDisputes, status);
  const selected = query.data?.find((item) => item.id === selectedId) || query.data?.[0];

  function choose(item) {
    setSelectedId(item.id);
    setRefundCredits(String(item.creditsSpent));
    setReason('');
    setOperationKey(crypto.randomUUID());
  }

  async function review(decision) {
    if (!selected || busy) return;
    setBusy(true);
    setMutationError(null);
    setNotice('');
    try {
      const result = await adminLaterPhaseApi.reviewDispute(token, selected.id, {
        decision,
        refundCredits: decision === 'Refund' ? Number(refundCredits || selected.creditsSpent) : null,
        reason,
        idempotencyKey: operationKey,
      });
      setNotice(decision === 'Refund' ? `${result.refundCredits} credits refunded. Balance is now ${result.balanceAfter}.` : 'Dispute rejected with an audit record.');
      setSelectedId(null);
      setReason('');
      setOperationKey(crypto.randomUUID());
      query.refresh();
    } catch (error) {
      setMutationError(error);
    } finally {
      setBusy(false);
    }
  }

  return <div className="admin-overview later-page">
    <header className="admin-review-header"><div><p className="admin-form-eyebrow">Case management</p><h1>Disputes & refunds</h1><p>Resolve each case once; refunds update enrollment, balance, ledger, notification and audit together.</p></div><label className="admin-status-filter">Status<select value={status} onChange={(event) => { setStatus(event.target.value); setSelectedId(null); }}><option value="">All</option><option>Open</option><option>ResolvedRefund</option><option>Rejected</option></select></label></header>
    <AdminDataState loading={query.loading} error={query.error || mutationError} onRetry={query.error ? query.refresh : () => setMutationError(null)} />
    {notice ? <p className="admin-review-notice" role="status">{notice}</p> : null}
    {query.data ? <div className="later-split"><div className="later-case-list">{query.data.map((item) => <button type="button" className={selected?.id === item.id ? 'active' : ''} key={item.id} onClick={() => choose(item)}><span>DSP-{item.id}</span><strong>{item.userName}</strong><small>{item.courseCode} · {item.status}</small></button>)}{!query.data.length ? <div className="admin-review-state">No disputes match this status.</div> : null}</div>
      {selected ? <section className="later-panel later-case-detail"><span className="admin-status">{selected.status}</span><h2>DSP-{selected.id} · {selected.userName}</h2><p>{selected.userEmail}</p><dl><div><dt>Course</dt><dd>{selected.courseCode} · {selected.courseTitle}</dd></div><div><dt>Enrollment cost</dt><dd>{selected.creditsSpent} credits</dd></div><div><dt>Member reason</dt><dd>{selected.reason}</dd></div></dl>{selected.status === 'Open' ? <><div className="form-group"><label htmlFor="refund-credits">Credits to restore</label><input id="refund-credits" type="number" min="1" max={selected.creditsSpent} value={refundCredits || selected.creditsSpent} onChange={(event) => setRefundCredits(event.target.value)} /></div><div className="form-group"><label htmlFor="dispute-reason">Resolution note</label><textarea id="dispute-reason" required maxLength="512" value={reason} onChange={(event) => setReason(event.target.value)} /></div><div className="trainer-actions"><button className="btn btn-teal" type="button" disabled={busy || !reason.trim()} onClick={() => review('Refund')}>{busy ? 'Processing…' : 'Process refund'}</button><button className="btn btn-danger" type="button" disabled={busy || !reason.trim()} onClick={() => review('Reject')}>Reject case</button></div></> : <p className="admin-review-notice">{selected.resolutionNote || 'Resolved without a note.'}</p>}</section> : <section className="later-panel admin-review-state">Select a dispute to inspect its evidence.</section>}</div> : null}
  </div>;
}
