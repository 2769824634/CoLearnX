import { useEffect, useState } from 'react';
import useAdminAuth from '../../auth/useAdminAuth';
import { adminRoleRequestsApi } from '../../api';
import Modal from '../../components/Modal';

const FILTERS = ['Pending', 'Approved', 'Rejected', 'All'];
const DATE_FORMATTER = new Intl.DateTimeFormat('en-AU', {
  day: '2-digit',
  month: 'short',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
});

function formatDate(value) {
  return value ? DATE_FORMATTER.format(new Date(value)) : '—';
}

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
          setLoadError(error.message || 'Role requests could not be loaded.');
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
    if (nextFilter === filter) return;
    setFilter(nextFilter);
    setLoading(true);
    setLoadError('');
    setReviewNotice('');
  }

  function refresh() {
    setLoading(true);
    setLoadError('');
    setReviewNotice('');
    setRevision((value) => value + 1);
  }

  function openReview(roleRequest) {
    setSelected(roleRequest);
    setDecision('Approve');
    setReason('');
    setReviewError('');
    setReviewNotice('');
  }

  function closeReview() {
    if (reviewing) return;
    setSelected(null);
  }

  async function submitReview(event) {
    event.preventDefault();
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
      setReviewError(error.message || 'The review could not be saved.');
    } finally {
      setReviewing(false);
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
          <strong>{roleRequests.length}</strong>
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
                onClick={() => selectFilter(item)}
              >
                {item}
              </button>
            ))}
          </div>
          <button type="button" className="btn btn-ghost btn-sm" onClick={refresh} disabled={loading}>
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
                  <th scope="col">Submitted</th>
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
                    <td>{formatDate(roleRequest.createdAt)}</td>
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
                        <span className="admin-reviewed-at">{formatDate(roleRequest.reviewedAt)}</span>
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

            <fieldset className="admin-decision-fieldset">
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
                onChange={(event) => setReason(event.target.value)}
                placeholder={decision === 'Reject'
                  ? 'Explain what the applicant needs to address.'
                  : 'Add context for the audit trail.'}
              />
            </div>

            {reviewError ? <div className="admin-review-inline-error" role="alert">{reviewError}</div> : null}

            <div className="modal-actions">
              <button type="button" className="btn btn-ghost" onClick={closeReview} disabled={reviewing}>
                Cancel
              </button>
              <button
                type="submit"
                className={decision === 'Reject' ? 'btn admin-reject-button' : 'btn btn-primary'}
                disabled={reviewing}
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
