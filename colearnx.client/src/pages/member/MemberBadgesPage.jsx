import { useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import MemberShell from '../../components/MemberShell';
import { useMemberNotifications } from '../../components/memberNotificationsState';
import { certificatesApi, medalsApi } from '../../api';
import { downloadFile } from '../../api/client';
import MedalMark from './MedalMark';
import useTrainerQuery from '../trainer/useTrainerQuery';
import { utcDate } from '../../utils/utcDates';
import { userFacingError } from '../businessPresentation';

async function loadCertificates(token, _key, signal) {
  const [certificates, eligibility, requests, medals] = await Promise.all([
    certificatesApi.my(token, signal), certificatesApi.eligibility(token, signal), certificatesApi.requests(token, signal), medalsApi.my(token, signal),
  ]);
  return { certificates, eligibility, requests, medals };
}
const materials = { Locked: 'Not earned', Brass: 'Brass', Silver: 'Silver', Gold: 'Gold', Diamond: 'Diamond' };
const medalPill = { Locked: 'neutral', Brass: 'warn', Silver: 'info', Gold: 'warn', Diamond: 'purple' };
const states = { Submitted: 'Awaiting Trainer review', TrainerApproved: 'Awaiting Admin approval', TrainerRejected: 'Rejected by Trainer', AdminRejected: 'Rejected by Admin', Issued: 'Certificate issued' };
function AwardIcon() {
  return <svg className="certificate-award" viewBox="0 0 24 24" aria-hidden="true" focusable="false"><circle cx="12" cy="9" r="6" fill="none" stroke="currentColor" strokeWidth="1.7" /><path d="M8.5 13.8 7 21l5-2.6 5 2.6-1.5-7.2" fill="none" stroke="currentColor" strokeWidth="1.7" strokeLinejoin="round" /></svg>;
}
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
    } catch (error) { setAction({ token: query.token, error: userFacingError(error, 'Unable to request a certificate. Please try again.') }); }
    finally { inFlight.current = false; }
  }
  return <MemberShell title="Badges & Certificates" subtitle="Your achievements and certificate applications, in one place.">
    <div className="certificate-intro"><p>Stages shown here come from your issued certificates. Each subject area also has one medal, from brass to diamond.</p><button className="btn btn-ghost" onClick={query.refresh} disabled={query.loading || busy}>Refresh</button></div>
    {query.loading ? <p role="status">Loading certificates…</p> : query.error ? <div className="callout warn"><p role="alert">{userFacingError(query.error, 'Could not load certificates. Please retry.')}</p><button className="btn btn-primary" onClick={query.refresh}>Retry</button></div> : <>
      <section className="certificate-section" aria-labelledby="medals-heading"><h2 id="medals-heading">Medals</h2>
        <p>Any finished course earns brass. Three or more courses at Intermediate or Advanced earn silver. Five or more courses, including an Advanced course, earn gold. Five or more courses across Beginner, Intermediate, and Advanced earn diamond.</p>
        {!query.data.medals?.length ? <div className="card card-body"><p>No subject medals are available.</p></div> : <div className="certificate-grid">{query.data.medals.map((medal) => <article className={`card certificate-card${medal.material && medal.material !== 'Locked' ? ' is-issued' : ''}`} key={medal.area}>
          <MedalMark area={medal.area} material={medal.material} title={medal.name} />
          <span className={`pill ${medalPill[medal.material] || 'neutral'}`}>{materials[medal.material] || medal.material}</span>
          <h3>{medal.name}</h3>
          {medal.progress ? <p>{medal.progress}</p> : null}
          <p>{medal.requirement}</p>
        </article>)}</div>}
      </section>
      <section className="certificate-section" aria-labelledby="issued-heading"><h2 id="issued-heading">Issued certificates</h2>
        {!query.data.certificates.length ? <div className="card card-body"><p>No certificates yet. Complete a program and meet its attendance and assessment requirements to apply.</p><Link to="/member/programs">View my programs</Link></div> : <div className="certificate-grid">{query.data.certificates.map((certificate) => <article key={certificate.id} className="card certificate-card is-issued">
          <AwardIcon /><span className="pill purple">{certificate.learningPathId ? certificate.stageName : 'Course'}</span><h3>{certificate.title}</h3>
          {certificate.courseTitle ? <p>{certificate.courseCode} · {certificate.courseTitle}</p> : <p>{certificate.learningPathName || 'Stage certificate'}</p>}
          <p>Issued <DateLabel value={certificate.awardedAt} /></p><p className="certificate-verify-label">Verification number</p><code>{certificate.verificationCode}</code>
          <button type="button" className="btn btn-ghost" onClick={() => downloadFile(`/api/certificates/${certificate.id}/file`, `${certificate.courseCode || 'certificate'}.pdf`, query.token)}>Download PDF</button>
        </article>)}</div>}
      </section>
      <section className="certificate-section" aria-labelledby="apply-heading"><h2 id="apply-heading">Request a certificate</h2><p>Eligibility is checked by the server using completion, attendance and assessment results.</p>
        {!query.data.eligibility.length ? <p>No enrolled programs available. <Link to="/member/courses">Explore courses</Link></p> : <div className="certificate-grid">{query.data.eligibility.map((item) => <article key={item.enrollmentId} className={`card certificate-card${item.isEligible && !item.existingRequestId ? ' is-ready' : ''}`}>
          <h3>{item.courseTitle}</h3><p>{item.courseCode} · Enrollment #{item.enrollmentId}</p>
          <p>{item.progressPercent != null ? `Progress ${item.progressPercent}% · Required 100%` : 'Progress unavailable · Required 100%'}</p>
          {item.attendanceRate != null ? <p>Attendance {item.attendanceRate}% · Assessments passed {item.passedAssessmentCount}/{item.assessmentCount}</p> : null}
          <p>Attendance required: 80% · All assessments must pass.</p>
          {item.assessments?.length ? <ul className="certificate-assessments">{item.assessments.map((assessment) => <li key={assessment.id}><strong>{assessment.title}</strong><span>{assessment.status || (assessment.score == null ? 'Not graded' : assessment.score >= assessment.passScore ? 'Passed' : 'Not passed')}</span><small>Score: {assessment.score ?? '—'} · Pass score: {assessment.passScore}</small></li>)}</ul> : <p>No assessment details available.</p>}
          {item.reasons?.length ? <ul>{item.reasons.map((reason) => <li key={reason}>{reason}</li>)}</ul> : <p>{item.existingRequestId ? 'Your application is listed below.' : 'Eligible to apply'}</p>}
          <button className="btn btn-primary" disabled={busy || !item.isEligible || Boolean(item.existingRequestId)} onClick={() => submit(item.enrollmentId)}>{item.existingRequestId ? 'Already requested' : busy ? 'Submitting…' : 'Request certificate'}</button>
        </article>)}</div>}
        {currentAction?.error ? <p className="callout warn" role="alert">{currentAction.error}</p> : null}
      </section>
      <section className="certificate-section" aria-labelledby="progress-heading"><h2 id="progress-heading">Application progress</h2>
        {!query.data.requests.length ? <p>No certificate applications yet.</p> : <div className="certificate-grid">{query.data.requests.map((request) => <article className="card certificate-card" key={request.id}>
          <div className="certificate-heading"><h3>{request.courseTitle || `Enrollment #${request.enrollmentId}`}</h3><span className={`pill ${request.status === 'Issued' ? 'success' : request.status.endsWith('Rejected') ? 'danger' : 'purple'}`}>{states[request.status] || request.status}</span></div>
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
