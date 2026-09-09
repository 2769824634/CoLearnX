import { useState } from 'react';
import useAdminAuth from '../../auth/useAdminAuth';
import { adminLaterPhaseApi } from '../../api/adminLaterPhase';
import AdminDataState from './AdminDataState';
import useAdminQuery from './useAdminQuery';

async function loadLaterApprovals(token, type, signal) {
  return type === 'materials'
    ? adminLaterPhaseApi.materialVersions(token, 'PendingApproval', signal)
    : adminLaterPhaseApi.certificateRequests(token, 'TrainerApproved', signal);
}

export default function AdminLaterApprovalsPage({ type }) {
  const { token } = useAdminAuth();
  const query = useAdminQuery(loadLaterApprovals, type);
  const [notes, setNotes] = useState({});
  const [busyId, setBusyId] = useState(null);
  const [mutationError, setMutationError] = useState(null);
  const [notice, setNotice] = useState('');

  async function review(item, decision) {
    if (busyId) return;
    setBusyId(item.id || item.versionId);
    setMutationError(null);
    setNotice('');
    const id = item.id || item.versionId;
    try {
      if (type === 'materials') await adminLaterPhaseApi.reviewMaterial(token, item.versionId, decision, notes[id]);
      else await adminLaterPhaseApi.reviewCertificate(token, item.id, decision, notes[id]);
      setNotice(type === 'materials'
        ? `Material version ${decision === 'Approve' ? 'approved for Trainer use' : 'rejected'}.`
        : `Certificate request ${decision === 'Approve' ? 'approved and issued' : 'rejected'}.`);
      query.refresh();
    } catch (error) {
      setMutationError(error);
    } finally {
      setBusyId(null);
    }
  }

  const heading = type === 'materials' ? 'Material version approvals' : 'Certificate final review';
  const subtitle = type === 'materials'
    ? 'Approve immutable versions before Trainers can attach them to an Intake.'
    : 'Only Trainer-approved requests reach this final issuance gate.';
  return <section className="admin-review-page later-page"><header className="admin-review-header"><div><p className="admin-form-eyebrow">Governed queue</p><h1>{heading}</h1><p>{subtitle}</p></div><button className="btn btn-ghost" type="button" disabled={query.loading} onClick={query.refresh}>Refresh</button></header>
    <AdminDataState loading={query.loading} error={query.error || mutationError} onRetry={query.error ? query.refresh : () => setMutationError(null)} />
    {notice ? <p className="admin-review-notice" role="status">{notice}</p> : null}
    {query.data?.length ? <div className="later-review-list">{query.data.map((item) => {
      const id = item.id || item.versionId;
      return <article className="later-review-card" key={id}><div className="later-review-card-main"><span className="admin-status">{item.status}</span><h2>{type === 'materials' ? item.title : item.learnerName}</h2><p>{type === 'materials' ? `${item.creatorName} · ${item.format} · version ${item.versionNumber}` : `${item.courseCode} · ${item.courseTitle}`}</p>{type === 'materials' ? <a href={item.filePath}>Review submitted file</a> : <small>Trainer note: {item.trainerReviewReason || 'Approved without a note'}</small>}</div><div className="later-review-card-action"><label htmlFor={`review-note-${type}-${id}`}>Review note / required rejection reason</label><textarea id={`review-note-${type}-${id}`} maxLength="512" value={notes[id] || ''} onChange={(event) => setNotes({ ...notes, [id]: event.target.value })} /><div className="trainer-actions"><button className="btn btn-primary" type="button" disabled={Boolean(busyId)} onClick={() => review(item, 'Approve')}>{busyId === id ? 'Saving…' : 'Approve'}</button><button className="btn btn-danger" type="button" disabled={Boolean(busyId) || !notes[id]?.trim()} onClick={() => review(item, 'Reject')}>Reject</button></div></div></article>;
    })}</div> : !query.loading && !query.error ? <div className="admin-review-state"><strong>No pending {type === 'materials' ? 'material versions' : 'certificate requests'}</strong><span>The queue is currently clear.</span></div> : null}
  </section>;
}
