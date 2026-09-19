import { useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import MemberShell from '../../components/MemberShell';
import { useMemberNotifications } from '../../components/memberNotificationsState';
import { certificatesApi } from '../../api';
import useTrainerQuery from '../trainer/useTrainerQuery';
import { utcDate } from '../../utils/utcDates';

async function loadCertificates(token, _key, signal) {
  const [certificates, eligibility, requests] = await Promise.all([
    certificatesApi.my(token, signal), certificatesApi.eligibility(token, signal), certificatesApi.requests(token, signal),
  ]);
  return { certificates, eligibility, requests };
}
const states = { Submitted: 'Awaiting Trainer review', TrainerApproved: 'Awaiting Admin approval', TrainerRejected: 'Rejected by Trainer', AdminRejected: 'Rejected by Admin', Issued: 'Certificate issued' };
function DateLabel({ value }) { return value ? <time dateTime={value}>{utcDate(value).toLocaleString()}</time> : null; }

export default function MemberBadgesPage() {
  const query = useTrainerQuery(loadCertificates);
  const notifications = useMemberNotifications();
  const [action, setAction] = useState(null);
  const inFlight = useRef(false);
  const currentAction = action?.token === query.token ? action : null;
  const busy = Boolean(currentAction?.busy);
  async function submit(enrollmentId) {
    if (inFlight.current) return;
    inFlight.current = true; setAction({ token: query.token, busy: true });
    try {
      const request = await certificatesApi.request(enrollmentId, query.token);
      query.setData({ ...query.data, requests: [request, ...query.data.requests.filter((item) => item.id !== request.id)],
        eligibility: query.data.eligibility.map((item) => item.enrollmentId === enrollmentId ? { ...item, existingRequestId: request.id, existingRequestStatus: request.status } : item) });
      setAction(null); notifications?.refresh();
    } catch (error) { setAction({ token: query.token, error: error.message || 'Unable to request a certificate. Please try again.' }); }
    finally { inFlight.current = false; }
  }
  return <MemberShell title="Badges & Certificates" subtitle="Your achievements and certificate applications, in one place.">
    <div className="certificate-intro"><p>Stages shown here come from your issued certificates. Separate badge awards are not currently configured.</p><button className="btn btn-ghost" onClick={query.refresh} disabled={query.loading || busy}>Refresh</button></div>
    {query.loading ? <p role="status">Loading certificates…</p> : query.error ? <div className="callout warn"><p role="alert">{query.error.message}</p><button className="btn btn-primary" onClick={query.refresh}>Retry</button></div> : <>
      <section aria-labelledby="issued-heading"><h2 id="issued-heading">Issued certificates</h2>
        {!query.data.certificates.length ? <div className="card card-body"><p>No certificates yet. Complete a program and meet its attendance and assessment requirements to apply.</p><Link to="/member/programs">View my programs</Link></div> : <div className="certificate-grid">{query.data.certificates.map((certificate) => <article key={certificate.id} className="card certificate-card">
          <span className="pill">Stage {certificate.stageNumber} · {certificate.stageName}</span><h3>{certificate.title}</h3>
          {certificate.courseTitle ? <p>{certificate.courseCode} · {certificate.courseTitle}</p> : <p>Stage certificate</p>}
          <p>Issued <DateLabel value={certificate.awardedAt} /></p><p>Verification number</p><code>{certificate.verificationCode}</code>
        </article>)}</div>}
      </section>
      <section className="certificate-section" aria-labelledby="apply-heading"><h2 id="apply-heading">Request a certificate</h2><p>Eligibility is checked by the server using completion, attendance and assessment results.</p>
        {!query.data.eligibility.length ? <p>No enrolled programs available. <Link to="/member/courses">Explore courses</Link></p> : <div className="certificate-grid">{query.data.eligibility.map((item) => <article key={item.enrollmentId} className="card certificate-card">
          <h3>{item.courseTitle}</h3><p>{item.courseCode} · Enrollment #{item.enrollmentId}</p>
          {item.attendanceRate != null ? <p>Attendance {item.attendanceRate}% · Assessments passed {item.passedAssessmentCount}/{item.assessmentCount}</p> : null}
          {item.reasons?.length ? <ul>{item.reasons.map((reason) => <li key={reason}>{reason}</li>)}</ul> : <p>{item.existingRequestId ? 'Your application is listed below.' : 'Eligible to apply'}</p>}
          <button className="btn btn-primary" disabled={busy || !item.isEligible || Boolean(item.existingRequestId)} onClick={() => submit(item.enrollmentId)}>{item.existingRequestId ? 'Already requested' : busy ? 'Submitting…' : 'Request certificate'}</button>
        </article>)}</div>}
        {currentAction?.error ? <p className="callout warn" role="alert">{currentAction.error}</p> : null}
      </section>
      <section className="certificate-section" aria-labelledby="progress-heading"><h2 id="progress-heading">Application progress</h2>
        {!query.data.requests.length ? <p>No certificate applications yet.</p> : <div className="certificate-grid">{query.data.requests.map((request) => <article className="card certificate-card" key={request.id}>
          <div className="certificate-heading"><h3>{request.courseTitle || `Enrollment #${request.enrollmentId}`}</h3><span className="pill">{states[request.status] || request.status}</span></div>
          <p>Submitted <DateLabel value={request.submittedAt} /></p>
          {request.trainerReviewedAt ? <p>Trainer review <DateLabel value={request.trainerReviewedAt} /></p> : null}
          {request.trainerReviewReason ? <p>{request.trainerReviewReason}</p> : null}
          {request.adminReviewedAt ? <p>Admin review <DateLabel value={request.adminReviewedAt} /></p> : null}
          {request.adminReviewReason ? <p>{request.adminReviewReason}</p> : null}
          {request.status.endsWith('Rejected') ? <p>A rejected request cannot be resubmitted. Contact your Trainer about the review outcome.</p> : null}
          {request.status === 'Submitted' ? <p>Trainer review → Admin approval → Certificate issued</p> : null}
        </article>)}</div>}
      </section>
    </>}
  </MemberShell>;
}
