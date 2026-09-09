import { useEffect, useState } from 'react';
import useAdminAuth from '../../auth/useAdminAuth';
import { adminCourseReviewsApi } from '../../api';
import Modal from '../../components/Modal';

const FILTERS = [
  { value: 'PendingApproval', label: 'Pending approval' },
  { value: 'Published', label: 'Published' },
  { value: 'Rejected', label: 'Rejected' },
  { value: 'All', label: 'All' },
];
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

function formatStatus(status) {
  return status === 'PendingApproval' ? 'Pending approval' : status;
}

export default function AdminCourseReviewsPage() {
  const { token } = useAdminAuth();
  const [filter, setFilter] = useState('PendingApproval');
  const [revision, setRevision] = useState(0);
  const [courses, setCourses] = useState([]);
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

    adminCourseReviewsApi.list(token, filter === 'All' ? null : filter, controller.signal)
      .then((items) => {
        if (!cancelled) {
          setCourses(items);
          setLoadError('');
        }
      })
      .catch((error) => {
        if (!cancelled && error.name !== 'AbortError') {
          setLoadError(error.message || 'Courses could not be loaded.');
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

  function openReview(course) {
    setSelected(course);
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
      setReviewError('Add a reason before rejecting this course.');
      return;
    }

    setReviewing(true);
    setReviewError('');
    try {
      const result = await adminCourseReviewsApi.review(
        token,
        selected.id,
        decision,
        reason.trim(),
      );
      const reviewed = result.course;
      setCourses((items) => {
        if (filter !== 'All' && reviewed.status !== filter)
          return items.filter((item) => item.id !== reviewed.id);
        return items.map((item) => item.id === reviewed.id ? reviewed : item);
      });
      setReviewNotice(`${reviewed.code} is now ${formatStatus(reviewed.status).toLowerCase()}.`);
      setSelected(null);
    } catch (error) {
      setReviewError(error.message || 'The course review could not be saved.');
    } finally {
      setReviewing(false);
    }
  }

  const filterLabel = FILTERS.find((item) => item.value === filter)?.label ?? filter;

  return (
    <div className="admin-review-page">
      <header className="admin-review-header">
        <div>
          <p className="admin-form-eyebrow">Catalogue review</p>
          <h1>Course reviews</h1>
          <p>Publish only submitted courses that are ready for the learner catalogue.</p>
        </div>
        <div className="admin-review-count" aria-live="polite">
          <strong>{courses.length}</strong>
          <span>{filterLabel.toLowerCase()} records</span>
        </div>
      </header>

      <section className="admin-review-workspace" aria-label="Course review queue">
        <div className="admin-review-toolbar">
          <div className="admin-review-filters" aria-label="Filter courses">
            {FILTERS.map((item) => (
              <button
                key={item.value}
                type="button"
                className={item.value === filter ? 'active' : ''}
                aria-pressed={item.value === filter}
                onClick={() => selectFilter(item.value)}
              >
                {item.label}
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
          <div className="admin-review-state" role="status">Loading courses…</div>
        ) : null}

        {!loadError && !loading && courses.length === 0 ? (
          <div className="admin-review-state">
            <strong>No {filterLabel.toLowerCase()} courses</strong>
            <span>The catalogue gate is clear for this view.</span>
          </div>
        ) : null}

        {!loadError && !loading && courses.length > 0 ? (
          <div className="admin-review-table-wrap">
            <table className="admin-review-table admin-course-table">
              <caption className="sr-only">{filterLabel} courses</caption>
              <thead>
                <tr>
                  <th scope="col">Course</th>
                  <th scope="col">Submitted by</th>
                  <th scope="col">Catalogue</th>
                  <th scope="col">Credits</th>
                  <th scope="col">Status</th>
                  <th scope="col"><span className="sr-only">Action</span></th>
                </tr>
              </thead>
              <tbody>
                {courses.map((course) => (
                  <tr key={course.id}>
                    <td>
                      <strong>{course.title}</strong>
                      <span>{course.code}</span>
                    </td>
                    <td>
                      <strong>{course.submittedByName}</strong>
                      <span>{course.submittedByEmail}</span>
                    </td>
                    <td>{course.category}<span>{course.level}</span></td>
                    <td><span className="admin-credit-value">{course.creditCost}</span></td>
                    <td>
                      <span className={`admin-status ${course.status.toLowerCase()}`}>
                        {formatStatus(course.status)}
                      </span>
                    </td>
                    <td className="admin-review-action">
                      {course.status === 'PendingApproval' ? (
                        <button
                          type="button"
                          className="btn btn-primary btn-sm"
                          onClick={() => openReview(course)}
                        >
                          Review
                        </button>
                      ) : (
                        <span className="admin-reviewed-at">
                          {course.reviewedAt ? formatDate(course.reviewedAt) : 'Existing course'}
                        </span>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : null}
      </section>

      <Modal open={Boolean(selected)} title="Review course" onClose={closeReview} width={600}>
        {selected ? (
          <form onSubmit={submitReview}>
            <div className="admin-review-summary admin-course-summary">
              <span>{selected.code}</span>
              <strong>{selected.title}</strong>
              <small>{selected.category} · {selected.level} · {selected.creditCost} credits</small>
              {selected.description ? <p>{selected.description}</p> : null}
            </div>

            <fieldset className="admin-decision-fieldset">
              <legend>Decision</legend>
              <div className="admin-decision-options">
                <button
                  type="button"
                  className={`${decision === 'Approve' ? 'active ' : ''}approve`}
                  aria-pressed={decision === 'Approve'}
                  onClick={() => {
                    setDecision('Approve');
                    setReviewError('');
                  }}
                >
                  Publish
                </button>
                <button
                  type="button"
                  className={`${decision === 'Reject' ? 'active ' : ''}reject`}
                  aria-pressed={decision === 'Reject'}
                  onClick={() => {
                    setDecision('Reject');
                    setReviewError('');
                  }}
                >
                  Reject
                </button>
              </div>
            </fieldset>

            <div className="form-group admin-review-reason">
              <label htmlFor="course-review-reason">
                Review note {decision === 'Reject' ? <span>Required</span> : <small>Optional</small>}
              </label>
              <textarea
                id="course-review-reason"
                maxLength={512}
                required={decision === 'Reject'}
                value={reason}
                onChange={(event) => setReason(event.target.value)}
                placeholder={decision === 'Reject'
                  ? 'Explain what must change before resubmission.'
                  : 'Add publication context for the audit trail.'}
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
                {reviewing ? 'Saving…' : decision === 'Approve' ? 'Publish course' : 'Reject course'}
              </button>
            </div>
          </form>
        ) : null}
      </Modal>
    </div>
  );
}
