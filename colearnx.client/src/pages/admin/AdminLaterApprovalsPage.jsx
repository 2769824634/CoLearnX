import { useState } from 'react';
import useAdminAuth from '../../auth/useAdminAuth';
import { adminLaterPhaseApi } from '../../api/adminLaterPhase';
import AdminDataState from './AdminDataState';
import useAdminQuery from './useAdminQuery';
import { useAdminPending } from './adminPending';
import { formatUtcDateTime, userFacingError } from '../businessPresentation';

const loadCertificateRequests = (token, _key, signal) => adminLaterPhaseApi.certificateRequests(token, 'TrainerApproved', signal);

// Final certificate review. Course files are reviewed with their course in Course reviews.
export default function AdminLaterApprovalsPage() {
  const { token } = useAdminAuth();
  const query = useAdminQuery(loadCertificateRequests);
  const pending = useAdminPending();
  const [notes, setNotes] = useState({});
  const [files, setFiles] = useState({});
  const [busyId, setBusyId] = useState(null);
  const [mutationError, setMutationError] = useState(null);
  const [notice, setNotice] = useState('');

  async function review(item, decision) {
    if (busyId) return;
    const id = item.id;
    setBusyId(id);
    setMutationError(null);
    setNotice('');
    try {
      if (decision === 'Approve') await adminLaterPhaseApi.issueCertificate(token, id, files[id], notes[id]);
      else await adminLaterPhaseApi.reviewCertificate(token, id, decision, notes[id]);
      setNotice(`Certificate request ${decision === 'Approve' ? 'issued' : 'rejected'}.`);
      query.refresh();
      pending?.refresh();
    } catch (error) {
      setMutationError(error);
    } finally {
      setBusyId(null);
    }
  }

  function refreshRecords() {
    if (busyId) return;
    setMutationError(null); setNotice(''); query.refresh();
  }

  return <section className="admin-review-page later-page"><header className="admin-review-header"><div><p className="admin-form-eyebrow">Governed queue</p><h1>Certificate final review</h1><p>Upload the certificate PDF to issue a Trainer-approved request, or reject it with a reason.</p></div><button className="btn btn-ghost" type="button" disabled={query.loading || Boolean(busyId)} onClick={refreshRecords}>Refresh</button></header>
    <AdminDataState loading={query.loading} error={query.error} onRetry={refreshRecords} />
    {mutationError ? <div className="admin-review-state error" role="alert"><strong>Review result needs checking</strong><span>{userFacingError(mutationError, 'The review could not be confirmed. Refresh the queue to check its current status before trying again.')}</span><button type="button" className="btn btn-ghost" onClick={refreshRecords}>Check current queue</button></div> : null}
    {notice ? <p className="admin-review-notice" role="status">{notice}</p> : null}
    {query.data?.length ? <div className="later-review-list">{query.data.map((item) => {
      const id = item.id;
      return <article className="later-review-card" key={id}>
        <div className="later-review-card-main"><span className="admin-status pending">{item.status}</span><h2>{item.learnerName}</h2>
          <p>{item.courseCode} · {item.courseTitle}</p>
          <small>Trainer note: {item.trainerReviewReason || 'Approved without a note'}</small><p>Submitted (UTC): {formatUtcDateTime(item.submittedAt)}</p>{item.trainerReviewedAt ? <p>Trainer reviewed (UTC): {formatUtcDateTime(item.trainerReviewedAt)}</p> : null}
        </div>
        <div className="later-review-card-action">
          <label htmlFor={`certificate-pdf-${id}`}>Certificate PDF</label><input id={`certificate-pdf-${id}`} type="file" accept="application/pdf,.pdf" disabled={Boolean(busyId)} onChange={(event) => setFiles({ ...files, [id]: event.target.files?.[0] || null })} />{files[id] ? <p>{files[id].name}</p> : <p>Choose the PDF that the learner will download.</p>}
          <label htmlFor={`review-note-certificates-${id}`}>Optional note / required rejection reason</label><textarea id={`review-note-certificates-${id}`} maxLength="512" disabled={Boolean(busyId)} value={notes[id] || ''} onChange={(event) => setNotes({ ...notes, [id]: event.target.value })} /><div className="trainer-actions"><button className="btn btn-teal" type="button" disabled={Boolean(busyId) || !files[id]?.name?.toLowerCase().endsWith('.pdf')} onClick={() => review(item, 'Approve')}>{busyId === id ? 'Saving…' : 'Issue certificate'}</button><button className="btn btn-danger" type="button" disabled={Boolean(busyId) || !notes[id]?.trim()} onClick={() => review(item, 'Reject')}>Reject</button></div></div>
      </article>;
    })}</div> : !query.loading && !query.error ? <div className="admin-review-state"><strong>No pending certificate requests</strong><span>The queue is currently clear.</span></div> : null}
  </section>;
}
