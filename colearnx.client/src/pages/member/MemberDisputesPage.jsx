import { useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { disputesApi } from '../../api';
import MemberShell from '../../components/MemberShell';
import useTrainerQuery from '../trainer/useTrainerQuery';
import { useMemberData } from './memberDataState';

const loadDisputes = (token, _key, signal) => disputesApi.my(token, signal);

export default function MemberDisputesPage() {
  const { state } = useMemberData();
  const [params] = useSearchParams();
  const [chosenId, setChosenId] = useState(params.get('enrollmentId') || '');
  const [reason, setReason] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const query = useTrainerQuery(loadDisputes);
  const enrollment = state.enrolled.find((item) => String(item.enrollmentId) === chosenId);
  const open = query.data?.some((item) => String(item.enrollmentId) === chosenId && item.status === 'Open');
  const eligible = enrollment && !['cancelled', 'refunded'].includes(enrollment.status)
    && !open && !query.loading && !query.error;

  async function submit(event) {
    event.preventDefault();
    if (!eligible || !reason.trim() || reason.trim().length > 1000 || busy) return;
    setBusy(true);
    setError('');
    try {
      const created = await disputesApi.create(query.token, Number(chosenId), reason.trim());
      query.setData([created, ...(query.data || []).filter((item) => item.id !== created.id)]);
      setReason('');
    } catch (cause) {
      setError(cause.message || 'Could not submit dispute.');
    } finally {
      setBusy(false);
    }
  }

  return <MemberShell title="My Disputes" subtitle="Request help with one of your enrollments and track the Admin decision.">
    <p><Link to="/member/programs">← My Programs</Link></p>
    <div className="card"><div className="card-header">Submit a dispute</div><div className="card-body">
      <form onSubmit={submit}>
        <div className="form-group"><label htmlFor="dispute-enrollment">Enrollment</label>
          <select id="dispute-enrollment" value={chosenId} onChange={(event) => setChosenId(event.target.value)}>
            <option value="">Choose a program</option>
            {state.enrolled.map((item) => <option key={item.enrollmentId} value={item.enrollmentId}>{item.courseCode} — {item.courseTitle} ({item.status})</option>)}
          </select>
        </div>
        {enrollment && !eligible ? <p role="status">{open ? 'An open dispute already exists for this enrollment.' : 'Cancelled or refunded enrollments cannot be disputed.'}</p> : null}
        <div className="form-group"><label htmlFor="dispute-member-reason">Reason</label>
          <textarea id="dispute-member-reason" maxLength={1000} value={reason} onChange={(event) => setReason(event.target.value)} disabled={!eligible || busy} />
          <small>{reason.length}/1000 characters</small>
        </div>
        {error ? <p role="alert" className="trainer-error">{error}</p> : null}
        <button type="submit" className="btn btn-primary" disabled={!eligible || !reason.trim() || busy}>{busy ? 'Submitting…' : 'Submit dispute'}</button>
      </form>
    </div></div>
    <div className="card" style={{ marginTop: 16 }}><div className="card-header">Your disputes <button type="button" className="btn btn-ghost btn-sm" onClick={query.refresh}>Refresh</button></div><div className="card-body">
      {query.loading ? <p role="status">Loading disputes…</p> : query.error ? <p role="alert">{query.error.message}</p> : null}
      {query.data?.length === 0 ? <p>No disputes yet.</p> : null}
      {query.data?.map((item) => <article key={item.id} className="callout" style={{ marginBottom: 12 }}>
        <strong>{item.courseCode} — {item.courseTitle || `Dispute #${item.id}`}</strong>
        <p>Status: <span>{item.status}</span></p>
        <p>Reason: {item.reason}</p>
        {item.resolutionNote ? <p>Admin resolution: {item.resolutionNote}</p> : null}
        {item.status === 'ResolvedRefund' && item.refundCredits != null ? <p>Refunded: {item.refundCredits} credits</p> : null}
      </article>)}
    </div></div>
  </MemberShell>;
}
