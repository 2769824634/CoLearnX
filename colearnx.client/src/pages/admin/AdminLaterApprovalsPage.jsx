import { useState } from 'react';
import useAdminAuth from '../../auth/useAdminAuth';
import { adminLaterPhaseApi } from '../../api/adminLaterPhase';
import AdminDataState from './AdminDataState';
import useAdminQuery from './useAdminQuery';
import { formatUtcDateTime, userFacingError } from '../businessPresentation';

async function loadLaterApprovals(token, type, signal) {
  return type === 'materials'
    ? adminLaterPhaseApi.materialVersions(token, 'PendingApproval', signal)
    : adminLaterPhaseApi.certificateRequests(token, 'TrainerApproved', signal);
}

function formatFileSize(bytes) {
  if (bytes == null || !Number.isFinite(Number(bytes)) || Number(bytes) < 0) return 'File size unavailable';
  const size = Number(bytes);
  return size >= 1024 * 1024 ? `${Number((size / (1024 * 1024)).toFixed(1))} MB`
    : size >= 1024 ? `${Number((size / 1024).toFixed(1))} KB` : `${size} bytes`;
}

function MaterialMetadata({ item }) {
  return <dl style={{ overflowWrap: 'anywhere' }}>
    <div><dt>Course title</dt><dd>{item.courseTitle || 'Course title unavailable'}</dd></div>
    <div><dt>Course code</dt><dd>{item.courseCode || 'Course code unavailable'}</dd></div>
    <div><dt>Course status</dt><dd>{item.courseStatus || 'Course status unavailable'}</dd></div>
    <div><dt>File name</dt><dd>{item.fileName || 'File name unavailable'}</dd></div>
    <div><dt>File size</dt><dd>{formatFileSize(item.fileSizeBytes)}</dd></div>
    <div><dt>Submitted (UTC)</dt><dd>{item.submittedAt ? formatUtcDateTime(item.submittedAt) : 'Submission date unavailable'}</dd></div>
  </dl>;
}

export default function AdminLaterApprovalsPage({ type }) {
  const { token } = useAdminAuth();
  const query = useAdminQuery(loadLaterApprovals, type);
  const [notes, setNotes] = useState({});
  const [busyId, setBusyId] = useState(null);
  const [mutationError, setMutationError] = useState(null);
  const [notice, setNotice] = useState('');
  const [download, setDownload] = useState(null);
  const downloadBusy = Boolean(download?.busy);

  async function review(item, decision) {
    if (busyId || downloadBusy) return;
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

  async function downloadMaterial(item) {
    if (busyId || downloadBusy || !item.filePath) return;
    const id = item.versionId;
    setDownload({ id, busy: true });
    try {
      await adminLaterPhaseApi.downloadMaterial(token, id, item.fileName || `${item.title}.${String(item.format || 'bin').toLowerCase()}`);
      setDownload({ id, message: 'File download started. Check your browser downloads.' });
    } catch (error) {
      setDownload({ id, error: userFacingError(error, 'Could not download this submitted file. Retry or contact the Creator about this attachment.') });
    }
  }

  function refreshRecords() {
    if (busyId || downloadBusy) return;
    setMutationError(null); setNotice(''); setDownload(null); query.refresh();
  }

  const heading = type === 'materials' ? 'Material version approvals' : 'Certificate final review';
  const subtitle = type === 'materials'
    ? 'Approve immutable versions before Trainers can attach them to an Intake.'
    : 'Only Trainer-approved requests reach this final issuance gate.';
  return <section className="admin-review-page later-page"><header className="admin-review-header"><div><p className="admin-form-eyebrow">Governed queue</p><h1>{heading}</h1><p>{subtitle}</p></div><button className="btn btn-ghost" type="button" disabled={query.loading || Boolean(busyId) || downloadBusy} onClick={refreshRecords}>Refresh</button></header>
    <AdminDataState loading={query.loading} error={query.error} onRetry={refreshRecords} />
    {mutationError ? <div className="admin-review-state error" role="alert"><strong>Review result needs checking</strong><span>{userFacingError(mutationError, 'The review could not be confirmed. Refresh the queue to check its current status before trying again.')}</span><button type="button" className="btn btn-ghost" onClick={refreshRecords}>Check current queue</button></div> : null}
    {notice ? <p className="admin-review-notice" role="status">{notice}</p> : null}
    {query.data?.length ? <div className="later-review-list">{query.data.map((item) => {
      const id = item.id || item.versionId;
      return <article className="later-review-card" key={id}>
        <div className="later-review-card-main"><span className="admin-status">{item.status}</span><h2>{type === 'materials' ? item.title : item.learnerName}</h2>
          <p>{type === 'materials' ? `${item.creatorName || 'Creator unavailable'} · ${item.format || 'Format unavailable'} · version ${item.versionNumber}` : `${item.courseCode} · ${item.courseTitle}`}</p>
          {type === 'materials' ? <>
            <MaterialMetadata item={item} />
            {item.filePath ? <button type="button" className="btn btn-ghost btn-sm" disabled={Boolean(busyId) || downloadBusy} onClick={() => downloadMaterial(item)}>{downloadBusy && download.id === item.versionId ? 'Downloading…' : 'Download submitted file'}</button> : <p>Submitted file unavailable</p>}
            {download?.id === item.versionId && download.busy ? <p role="status">Downloading submitted file…</p> : download?.id === item.versionId && download.message ? <p role="status">{download.message}</p> : null}
            {download?.id === item.versionId && download.error ? <p role="alert">{download.error}</p> : null}
          </> : <><small>Trainer note: {item.trainerReviewReason || 'Approved without a note'}</small><p>Submitted (UTC): {formatUtcDateTime(item.submittedAt)}</p>{item.trainerReviewedAt ? <p>Trainer reviewed (UTC): {formatUtcDateTime(item.trainerReviewedAt)}</p> : null}</>}
        </div>
        <div className="later-review-card-action"><label htmlFor={`review-note-${type}-${id}`}>Review note / required rejection reason</label><textarea id={`review-note-${type}-${id}`} maxLength="512" disabled={Boolean(busyId) || downloadBusy} value={notes[id] || ''} onChange={(event) => setNotes({ ...notes, [id]: event.target.value })} /><div className="trainer-actions"><button className="btn btn-primary" type="button" disabled={Boolean(busyId) || downloadBusy} onClick={() => review(item, 'Approve')}>{busyId === id ? 'Saving…' : 'Approve'}</button><button className="btn btn-danger" type="button" disabled={Boolean(busyId) || downloadBusy || !notes[id]?.trim()} onClick={() => review(item, 'Reject')}>Reject</button></div></div>
      </article>;
    })}</div> : !query.loading && !query.error ? <div className="admin-review-state"><strong>No pending {type === 'materials' ? 'material versions' : 'certificate requests'}</strong><span>The queue is currently clear.</span></div> : null}
  </section>;
}
