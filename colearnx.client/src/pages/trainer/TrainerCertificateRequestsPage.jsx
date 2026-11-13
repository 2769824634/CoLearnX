import { useState } from 'react';
import { Link } from 'react-router-dom';
import { trainerIntakesApi } from '../../api/trainerIntakes';
import { trainerLaterPhaseApi } from '../../api/trainerLaterPhase';
import useTrainerQuery from './useTrainerQuery';
import { TrainerError, TrainerHeader, TrainerLoading } from './TrainerUi';
import { formatDate, intakeLink } from './intakeForm';

const GROUPS = [
  { title: 'Waiting for your review', statuses: ['Submitted'], empty: 'No certificate requests are waiting for you.' },
  { title: 'Reviewed', statuses: ['TrainerApproved', 'TrainerRejected', 'AdminRejected', 'Issued'], empty: 'Requests you have reviewed appear here.' },
];

const STATUS = {
  Submitted: { label: 'Waiting', tone: 'pendingapproval' },
  TrainerApproved: { label: 'With Admin', tone: 'inprogress' },
  TrainerRejected: { label: 'Rejected', tone: 'rejected' },
  AdminRejected: { label: 'Rejected by Admin', tone: 'rejected' },
  Issued: { label: 'Issued', tone: 'published' },
};

async function loadCertificateRequests(token, _, signal) {
  const [requests, intakes] = await Promise.all([
    trainerLaterPhaseApi.certificateRequests(token, '', signal),
    trainerIntakesApi.list(token, signal),
  ]);
  return { requests, intakes };
}

function RequestRow({ request, intake, busy, error, onReview }) {
  const [reason, setReason] = useState('');
  const { label, tone } = STATUS[request.status] || { label: request.status, tone: '' };
  const waiting = request.status === 'Submitted';
  const noteId = `certificate-note-${request.id}`;

  return <article className="trainer-intake-row trainer-approval-row">
    <div className="trainer-intake-title">
      <small><span>{request.courseCode}</span> · <span>{request.courseTitle}</span></small>
      <h3>{request.learnerName}</h3>
      <p>
        <Link to={intakeLink(request.courseIntakeId)}>{intake ? `Intake ${formatDate(intake.startsAt)} → ${formatDate(intake.endsAt)}` : 'Open Intake'}</Link>
        {' · '}Requested {formatDate(request.submittedAt)}
        {request.trainerReviewedAt ? ` · Reviewed ${formatDate(request.trainerReviewedAt)}` : ''}
      </p>
      {waiting ? <div className="form-group">
        <label htmlFor={noteId}>Review note (required to reject)</label>
        <input id={noteId} maxLength="512" value={reason} disabled={busy} onChange={(event) => setReason(event.target.value)} />
      </div> : request.trainerReviewReason ? <p>Your note: {request.trainerReviewReason}</p> : null}
      <TrainerError error={error} />
    </div>
    <span className={`trainer-status ${tone}`}>{label}</span>
    {waiting ? <div className="trainer-actions">
      <button type="button" className="btn btn-ghost btn-sm" disabled={busy || !reason.trim()} title={reason.trim() ? undefined : 'Enter a review note to reject'} onClick={() => onReview(request, 'Reject', reason)}>Reject</button>
      <button type="button" className="btn btn-primary btn-sm" disabled={busy} onClick={() => onReview(request, 'Approve', reason)}>Approve</button>
    </div> : null}
  </article>;
}

export default function TrainerCertificateRequestsPage() {
  const query = useTrainerQuery(loadCertificateRequests);
  const [busyId, setBusyId] = useState(null);
  const [errors, setErrors] = useState({});
  const requests = query.data?.requests || [];
  const intakes = new Map((query.data?.intakes || []).map((intake) => [intake.id, intake]));

  async function review(request, decision, reason) {
    setBusyId(request.id);
    setErrors((current) => ({ ...current, [request.id]: null }));
    try {
      const updated = await trainerLaterPhaseApi.reviewCertificate(query.token, request.id, decision, reason.trim());
      query.setData({ ...query.data, requests: requests.map((item) => item.id === updated.id ? updated : item) });
    } catch (requestError) {
      setErrors((current) => ({ ...current, [request.id]: requestError }));
    } finally {
      setBusyId(null);
    }
  }

  return <section className="trainer-page">
    <TrainerHeader eyebrow="Learners / Certificate requests" title="Certificate requests" action={<button type="button" className="btn btn-ghost" onClick={query.refresh} disabled={query.loading}>Refresh</button>}>
      Members who finished one of your Intakes ask for their certificate here. Approving sends the request to Admin, who issues the certificate.
    </TrainerHeader>
    {query.loading ? <TrainerLoading /> : query.error ? <TrainerError error={query.error} onRetry={query.refresh} />
      : GROUPS.map((group) => {
        const items = requests.filter((item) => group.statuses.includes(item.status));
        const headingId = `trainer-certificates-${group.statuses[0].toLowerCase()}`;
        return <section key={group.title} aria-labelledby={headingId}>
          <div className="trainer-section-heading"><h2 id={headingId}>{group.title}</h2><span className="trainer-group-count">{items.length}</span></div>
          {!items.length ? <p className="trainer-group-empty">{group.empty}</p>
            : <div className="trainer-intake-list">{items.map((item) => <RequestRow key={item.id} request={item} intake={intakes.get(item.courseIntakeId)}
              busy={busyId !== null} error={errors[item.id]} onReview={review} />)}</div>}
        </section>;
      })}
  </section>;
}
