import { useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { disputesApi } from '../../api';
import MemberShell from '../../components/MemberShell';
import useTrainerQuery from '../trainer/useTrainerQuery';
import { useMemberSlices } from './memberDataState';
import { formatCount, userFacingError } from '../businessPresentation';

const loadDisputes = (token, _key, signal) => disputesApi.my(token, signal);

export default function MemberDisputesPage() {
  const { state } = useMemberSlices('enrollments');
  const [params] = useSearchParams();
  const [chosenId, setChosenId] = useState(params.get('enrollmentId') || '');
  const [reason, setReason] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const query = useTrainerQuery(loadDisputes);
  const enrollment = state.enrolled.find((item) => String(item.enrollmentId) === chosenId);
  const disputable = state.enrolled.filter((item) => ['active', 'completed'].includes(item.status));
  const open = query.data?.some((item) => String(item.enrollmentId) === chosenId && item.status === 'Open');
  const eligible = enrollment && ['active', 'completed'].includes(enrollment.status)
    && !open && !state.loading && !state.loadError && !query.loading && !query.error;

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
      setError(userFacingError(cause, 'Could not submit your dispute. Refresh your disputes before trying again.'));
    } finally {
      setBusy(false);
    }
  }

  return <MemberShell title="My Disputes" subtitle="Request help with one of your enrollments and track the Admin decision.">
    <p><Link to="/member/programs">← My Programs</Link></p>
    <div className="card"><div className="card-header">Submit a dispute</div><div className="card-body">
      {state.loading ? <p role="status">Loading your enrollments…</p> : state.loadError ? <p role="alert">{state.loadError}</p> : !disputable.length ? <p>Disputes require an active or completed enrollment. Reserved places can be cancelled in <Link to="/member/programs?tab=reserved">My Programs</Link> to release held credits. <Link to="/member/courses">Browse courses</Link></p> : <p>Select an active or completed program and explain the issue for Admin review.</p>}
      <form onSubmit={submit}>
        <div className="form-group"><label htmlFor="dispute-enrollment">Enrollment</label>
          <select id="dispute-enrollment" value={chosenId} onChange={(event) => setChosenId(event.target.value)}>
            <option value="">Choose a program</option>
            {disputable.map((item) => <option key={item.enrollmentId} value={item.enrollmentId}>{item.courseCode} — {item.courseTitle} ({item.status})</option>)}
          </select>
        </div>
        {enrollment && !eligible && !query.loading && !state.loading ? <p role="status">{open ? 'An open dispute already exists for this enrollment.' : ['active', 'completed'].includes(enrollment.status) ? 'Disputes are unavailable until enrollment and dispute data have loaded.' : 'Only active or completed enrollments can be disputed.'}</p> : null}
        <div className="form-group"><label htmlFor="dispute-member-reason">Reason</label>
          <textarea id="dispute-member-reason" maxLength={1000} value={reason} onChange={(event) => setReason(event.target.value)} disabled={!eligible || busy} />
          <small>{reason.length}/1000 characters</small>
        </div>
        {error ? <p role="alert" className="trainer-error">{error}</p> : null}
        <button type="submit" className="btn btn-primary" disabled={!eligible || !reason.trim() || busy}>{busy ? 'Submitting…' : 'Submit dispute'}</button>
      </form>
    </div></div>
    <div className="card" style={{ marginTop: 16 }}><div className="card-header">Your disputes <button type="button" className="btn btn-ghost btn-sm" onClick={query.refresh}>Refresh</button></div><div className="card-body">
      {query.loading ? <p role="status">Loading disputes…</p> : query.error ? <p role="alert">{userFacingError(query.error, 'Could not load your disputes. Please refresh to retry.')}</p> : null}
      {query.data?.length === 0 ? <p>No disputes yet.</p> : null}
      {query.data?.map((item) => <article key={item.id} className="callout" style={{ marginBottom: 12 }}>
        <strong>{item.courseCode} — {item.courseTitle || `Dispute #${item.id}`}</strong>
        <p>Status: <span>{item.status}</span></p>
        <p>Reason: {item.reason}</p>
        {item.resolutionNote ? <p>Admin resolution: {item.resolutionNote}</p> : null}
        {item.status === 'ResolvedRefund' && item.refundCredits != null ? <p>Refunded: {formatCount(item.refundCredits, 'credit')}</p> : null}
      </article>)}
    </div></div>
  </MemberShell>;
}
