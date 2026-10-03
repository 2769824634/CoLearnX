import { useEffect, useState } from 'react';
import useAdminAuth from '../../auth/useAdminAuth';
import { adminRoleRequestsApi } from '../../api';
import Modal from '../../components/Modal';
import { formatUtcDateTime, userFacingError } from '../businessPresentation';

const FILTERS = ['Pending', 'Approved', 'Rejected', 'All'];
function hasCredentials(roleRequest) {
  return Boolean(roleRequest.degreeOrResumePath || roleRequest.idDocumentPath);
}

export default function AdminRoleRequestsPage() {
  const { token } = useAdminAuth();
  const [filter, setFilter] = useState('Pending');
  const [revision, setRevision] = useState(0);
  const [roleRequests, setRoleRequests] = useState([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState('');
  const [reviewNotice, setReviewNotice] = useState('');
  const [selected, setSelected] = useState(null);
  const [decision, setDecision] = useState('Approve');
  const [reason, setReason] = useState('');
  const [reviewError, setReviewError] = useState('');
  const [reviewing, setReviewing] = useState(false);
  const [download, setDownload] = useState(null);
  const downloadBusy = Boolean(download?.busy);

  useEffect(() => {
    const controller = new AbortController();
    let cancelled = false;

    adminRoleRequestsApi.list(token, filter === 'All' ? null : filter, controller.signal)
      .then((items) => {
        if (!cancelled) {
          setRoleRequests(items);
          setLoadError('');
        }
      })
      .catch((error) => {
        if (!cancelled && error.name !== 'AbortError') {
          setLoadError(userFacingError(error, 'Role requests could not be loaded. Please retry.'));
        }
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
      controller.abort();
    };
  }, [filter, revision, token]);

  function selectFilter(nextFilter) {
    if (nextFilter === filter || reviewing || downloadBusy) return;
    setFilter(nextFilter);
    setLoading(true);
    setLoadError('');
    setReviewNotice('');
  }

  function refresh() {
    if (reviewing || downloadBusy) return;
    setLoading(true);
    setLoadError('');
    setReviewNotice('');
    setRevision((value) => value + 1);
  }

  function openReview(roleRequest) {
    if (reviewing || downloadBusy) return;
    setSelected(roleRequest);
    setDecision('Approve');
    setReason('');
    setReviewError('');
    setReviewNotice('');
    setDownload(null);
  }

  function closeReview() {
    if (reviewing || downloadBusy) return;
    setSelected(null);
  }

  async function submitReview(event) {
    event.preventDefault();
    if (!selected || reviewing || downloadBusy) return;
    if (decision === 'Reject' && !reason.trim()) {
      setReviewError('Add a reason before rejecting this request.');
      return;
    }

    setReviewing(true);
    setReviewError('');
    try {
      const result = await adminRoleRequestsApi.review(
        token,
        selected.id,
        decision,
        reason.trim(),
      );
      const reviewed = result.roleRequest;
      setRoleRequests((items) => {
        if (filter !== 'All' && reviewed.status !== filter)
          return items.filter((item) => item.id !== reviewed.id);
        return items.map((item) => item.id === reviewed.id ? reviewed : item);
      });
      setReviewNotice(`${reviewed.requestedRole} request ${reviewed.status.toLowerCase()}.`);
      setSelected(null);
    } catch (error) {
      setReviewError(userFacingError(error, 'The review could not be confirmed. Refresh the queue before trying again.'));
    } finally {
      setReviewing(false);
    }
  }

  async function downloadEvidence(kind) {
    if (!selected || reviewing || downloadBusy) return;
    const isResume = kind === 'resume';
    if (!(isResume ? selected.degreeOrResumePath : selected.idDocumentPath)) return;
    const label = isResume ? 'Resume' : 'ID document';
    setDownload({ kind, busy: true });
    try {
      await (isResume ? adminRoleRequestsApi.downloadResume(token, selected.id) : adminRoleRequestsApi.downloadIdDocument(token, selected.id));
      setDownload({ kind, message: `${label} download started. Check your browser downloads.` });
    } catch (error) {
      setDownload({ kind, error: userFacingError(error, `Could not download the ${label.toLowerCase()}. Retry or contact the applicant about this attachment.`) });
    }
  }

  return (
    <div className="admin-review-page">
      <header className="admin-review-header">
        <div>
          <p className="admin-form-eyebrow">Role governance</p>
          <h1>Role requests</h1>
          <p>Review Trainer and Creator access with a traceable decision.</p>
        </div>
        <div className="admin-review-count" aria-live="polite">
          <strong>{loading || loadError ? '—' : roleRequests.length}</strong>
          <span>{filter.toLowerCase()} records</span>
        </div>
      </header>

      <section className="admin-review-workspace" aria-label="Role request review queue">
        <div className="admin-review-toolbar">
          <div className="admin-review-filters" aria-label="Filter role requests">
            {FILTERS.map((item) => (
              <button
                key={item}
                type="button"
                className={item === filter ? 'active' : ''}
                aria-pressed={item === filter}
                disabled={reviewing || downloadBusy}
                onClick={() => selectFilter(item)}
              >
                {item}
              </button>
            ))}
          </div>
          <button type="button" className="btn btn-ghost btn-sm" onClick={refresh} disabled={loading || reviewing || downloadBusy}>
            Refresh
          </button>
        </div>

        {reviewNotice ? (
          <div className="admin-review-notice" role="status">{reviewNotice}</div>
        ) : null}

        {loadError ? (
          <div className="admin-review-state error" role="alert">
            <strong>Queue unavailable</strong>
            <span>{loadError}</span>
            <button type="button" className="btn btn-ghost btn-sm" onClick={refresh}>Try again</button>
          </div>
        ) : null}

        {!loadError && loading ? (
          <div className="admin-review-state" role="status">Loading role requests…</div>
        ) : null}

        {!loadError && !loading && roleRequests.length === 0 ? (
          <div className="admin-review-state">
            <strong>No {filter.toLowerCase()} requests</strong>
            <span>The queue is clear for this view.</span>
          </div>
        ) : null}

        {!loadError && !loading && roleRequests.length > 0 ? (
          <div className="admin-review-table-wrap">
            <table className="admin-review-table">
              <caption className="sr-only">{filter} Trainer and Creator role requests</caption>
              <thead>
                <tr>
                  <th scope="col">Applicant</th>
                  <th scope="col">Requested role</th>
                  <th scope="col">Evidence</th>
                  <th scope="col">Submitted (UTC)</th>
                  <th scope="col">Status</th>
                  <th scope="col"><span className="sr-only">Action</span></th>
                </tr>
              </thead>
              <tbody>
                {roleRequests.map((roleRequest) => (
                  <tr key={roleRequest.id}>
                    <td>
                      <strong>{roleRequest.userFullName}</strong>
                      <span>{roleRequest.userEmail}</span>
                    </td>
                    <td><span className="admin-role-label">{roleRequest.requestedRole}</span></td>
                    <td>{hasCredentials(roleRequest) ? 'Attached' : 'Not supplied'}</td>
                    <td>{formatUtcDateTime(roleRequest.createdAt)}</td>
                    <td>
                      <span className={`admin-status ${roleRequest.status.toLowerCase()}`}>
                        {roleRequest.status}
                      </span>
                    </td>
                    <td className="admin-review-action">
                      {roleRequest.status === 'Pending' ? (
                        <button
                          type="button"
                          className="btn btn-primary btn-sm"
                          onClick={() => openReview(roleRequest)}
                        >
                          Review
                        </button>
                      ) : (
                        <span className="admin-reviewed-at">{formatUtcDateTime(roleRequest.reviewedAt)}</span>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : null}
      </section>

      <Modal open={Boolean(selected)} title="Review role request" onClose={closeReview} width={560}>
        {selected ? (
          <form onSubmit={submitReview}>
            <div className="admin-review-summary">
              <span>Applicant</span>
              <strong>{selected.userFullName}</strong>
              <small>{selected.userEmail} · requesting {selected.requestedRole}</small>
            </div>
            {hasCredentials(selected) ? (
              <div className="trainer-actions" style={{ margin: '0 0 16px' }}>
                {selected.degreeOrResumePath ? <button type="button" className="btn btn-ghost btn-sm" disabled={reviewing || downloadBusy} onClick={() => downloadEvidence('resume')}>
                  {downloadBusy && download.kind === 'resume' ? 'Downloading…' : 'Download resume'}
                </button> : null}
                {selected.idDocumentPath ? <button type="button" className="btn btn-ghost btn-sm" disabled={reviewing || downloadBusy} onClick={() => downloadEvidence('id')}>
                  {downloadBusy && download.kind === 'id' ? 'Downloading…' : 'Download ID document'}
                </button> : null}
              </div>
            ) : (
              <p className="page-sub">No files were uploaded with this request.</p>
            )}
            {downloadBusy ? <p role="status">Downloading {download.kind === 'resume' ? 'resume' : 'ID document'}…</p> : download?.message ? <p role="status">{download.message}</p> : null}
            {download?.error ? <p role="alert" className="admin-review-inline-error">{download.error}</p> : null}
            {selected.applicantStatement ? (
              <div className="form-group">
                <label>Applicant statement</label>
                <p style={{ whiteSpace: 'pre-wrap', margin: '6px 0 16px', fontSize: 13 }}>{selected.applicantStatement}</p>
              </div>
            ) : null}

            <fieldset className="admin-decision-fieldset" disabled={reviewing || downloadBusy}>
              <legend>Decision</legend>
              <div className="admin-decision-options">
                {['Approve', 'Reject'].map((item) => (
                  <button
                    key={item}
                    type="button"
                    className={`${item === decision ? 'active ' : ''}${item.toLowerCase()}`}
                    aria-pressed={item === decision}
                    onClick={() => {
                      setDecision(item);
                      setReviewError('');
                    }}
                  >
                    {item}
                  </button>
                ))}
              </div>
            </fieldset>

            <div className="form-group admin-review-reason">
              <label htmlFor="role-review-reason">
                Review note {decision === 'Reject' ? <span>Required</span> : <small>Optional</small>}
              </label>
              <textarea
                id="role-review-reason"
                maxLength={512}
                required={decision === 'Reject'}
                value={reason}
                disabled={reviewing || downloadBusy}
                onChange={(event) => setReason(event.target.value)}
                placeholder={decision === 'Reject'
                  ? 'Explain what the applicant needs to address.'
                  : 'Add context for the audit trail.'}
              />
            </div>

            {reviewError ? <div className="admin-review-inline-error" role="alert">{reviewError}</div> : null}

            <div className="modal-actions">
              <button type="button" className="btn btn-ghost" onClick={closeReview} disabled={reviewing || downloadBusy}>
                Cancel
              </button>
              <button
                type="submit"
                className={decision === 'Reject' ? 'btn admin-reject-button' : 'btn btn-primary'}
                disabled={reviewing || downloadBusy}
              >
                {reviewing ? 'Saving…' : `${decision} request`}
              </button>
            </div>
          </form>
        ) : null}
      </Modal>
    </div>
  );
}
