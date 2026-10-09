import { useState } from 'react';
import { trainerIntakesApi } from '../../api/trainerIntakes';
import { trainerLaterPhaseApi } from '../../api/trainerLaterPhase';
import { TrainerError, TrainerHeader, TrainerLoading } from './TrainerUi';
import useTrainerQuery from './useTrainerQuery';
import TrainerReservationPanel from './TrainerReservationPanel';
import { formatCount } from '../businessPresentation';

async function loadLearnerWorkspace(token, _key, signal) {
  const summaries = await trainerIntakesApi.list(token, signal);
  const [cohorts, certificateRequests] = await Promise.all([
    Promise.all(summaries.map(async (summary) => {
      const [intake, learners, assessments] = await Promise.all([
        trainerIntakesApi.get(token, summary.id, signal),
        trainerLaterPhaseApi.learners(token, summary.id, signal),
        trainerLaterPhaseApi.assessments(token, summary.id, signal),
      ]);
      return { ...intake, learners, assessments };
    })),
    trainerLaterPhaseApi.certificateRequests(token, '', signal),
  ]);
  return { cohorts, certificateRequests };
}

const initialAssessment = { title: '', maxScore: '100', passScore: '60', dueAt: '' };
const initialGrade = { assessmentId: '', enrollmentId: '', score: '', feedback: '' };

function LearnerAttendance({ learner, sessions = [] }) {
  return <>{learner.attendanceRate}%{sessions.map((session) => {
    const status = learner.sessionAttendances?.find((item) => item.courseSessionId === session.id)?.status;
    return <small key={session.id}>{session.label}: {status || 'Not recorded'}</small>;
  })}</>;
}

export default function TrainerLearnersPage() {
  const query = useTrainerQuery(loadLearnerWorkspace);
  const [choice, setChoice] = useState('');
  const [assessment, setAssessment] = useState(initialAssessment);
  const [grade, setGrade] = useState(initialGrade);
  const [reviewReason, setReviewReason] = useState('');
  const [busy, setBusy] = useState(false);
  const [mutationError, setMutationError] = useState(null);
  const [notice, setNotice] = useState('');
  const cohort = query.data?.cohorts.find((item) => String(item.id) === choice) || query.data?.cohorts[0];
  const gradeableLearners = cohort?.learners.filter((item) => ['Active', 'Completed'].includes(item.enrollmentStatus)) || [];

  async function mutate(action, message, reset) {
    if (busy) return;
    setBusy(true);
    setMutationError(null);
    setNotice('');
    try {
      await action();
      reset?.();
      setNotice(message);
      query.refresh();
    } catch (error) {
      setMutationError(error);
    } finally {
      setBusy(false);
    }
  }

  function submitAssessment(event) {
    event.preventDefault();
    mutate(() => trainerLaterPhaseApi.createAssessment(query.token, cohort.id, {
      title: assessment.title,
      maxScore: Number(assessment.maxScore),
      passScore: Number(assessment.passScore),
      dueAt: assessment.dueAt ? new Date(assessment.dueAt).toISOString() : null,
    }), 'Assessment created.', () => setAssessment(initialAssessment));
  }

  function submitGrade(event) {
    event.preventDefault();
    mutate(() => trainerLaterPhaseApi.grade(query.token, grade.assessmentId, grade.enrollmentId, {
      score: Number(grade.score), feedback: grade.feedback || null,
    }), 'Grade saved.', () => setGrade(initialGrade));
  }

  const submittedRequests = query.data?.certificateRequests.filter((item) => item.status === 'Submitted') || [];
  return <section className="trainer-page later-page">
    <TrainerHeader title="Learner list">Monitor progress, attendance, assessment results and certificate readiness by Intake.</TrainerHeader>
    {query.loading ? <TrainerLoading /> : query.error ? <TrainerError error={query.error} onRetry={query.refresh} /> : <>
      <div className="trainer-toolbar"><div className="form-group"><label htmlFor="learner-intake">Intake</label><select id="learner-intake" value={cohort?.id || ''} onChange={(event) => setChoice(event.target.value)}>{query.data.cohorts.map((item) => <option key={item.id} value={item.id}>Intake #{item.id} · {item.status}</option>)}</select></div></div>
      {notice ? <p className="trainer-notice" role="status">{notice}</p> : null}<TrainerError error={mutationError} />
      {!cohort ? <div className="trainer-empty">No owned Intakes are available.</div> : <>
        <TrainerReservationPanel key={cohort.id} intake={cohort} />
        <div className="later-table-wrap"><table className="later-table"><thead><tr><th>Learner</th><th>Status</th><th>Progress</th><th>Attendance</th><th>Assessments</th><th>Certificate</th><th>Action</th></tr></thead><tbody>{cohort.learners.map((learner) => <tr key={learner.enrollmentId}><td><strong>{learner.fullName}</strong><small>{learner.email}</small></td><td>{learner.enrollmentStatus}</td><td>{learner.progressPercent}%</td><td><LearnerAttendance learner={learner} sessions={cohort.sessions} /></td><td>{learner.assessmentsPassed}/{learner.assessmentsTotal ?? cohort.assessments.length} passed<small>{learner.assessmentsGraded} graded</small></td><td>{learner.certificateRequestStatus || 'Not requested'}</td><td>{learner.enrollmentStatus === 'Active' ? <button type="button" className="btn btn-primary btn-sm" disabled={busy} onClick={() => mutate(() => trainerLaterPhaseApi.complete(query.token, learner.enrollmentId), 'Learner marked complete.')}>Mark complete</button> : null}</td></tr>)}</tbody></table></div>
        {!cohort.learners.length ? <div className="trainer-empty">No active or completed learners are listed for this Intake. Reserved places are shown separately above. This Intake needs at least {formatCount(cohort.minEnrollment ?? 10, 'learner')} to run.</div> : null}
        <div className="later-two-column">
          <form className="trainer-form" onSubmit={submitAssessment}><fieldset disabled={busy}><legend>Create assessment</legend><div className="form-group"><label htmlFor="assessment-title">Title</label><input id="assessment-title" required maxLength="160" value={assessment.title} onChange={(event) => setAssessment({ ...assessment, title: event.target.value })} /></div><div className="trainer-form-grid"><div className="form-group"><label htmlFor="assessment-max">Maximum</label><input id="assessment-max" type="number" min="1" required value={assessment.maxScore} onChange={(event) => setAssessment({ ...assessment, maxScore: event.target.value })} /></div><div className="form-group"><label htmlFor="assessment-pass">Pass score</label><input id="assessment-pass" type="number" min="0" required value={assessment.passScore} onChange={(event) => setAssessment({ ...assessment, passScore: event.target.value })} /></div></div><div className="form-group"><label htmlFor="assessment-due">Due at</label><input id="assessment-due" type="datetime-local" value={assessment.dueAt} onChange={(event) => setAssessment({ ...assessment, dueAt: event.target.value })} /></div></fieldset><button className="btn btn-primary" disabled={busy}>Create assessment</button></form>
          <form className="trainer-form" onSubmit={submitGrade}><fieldset disabled={busy || !cohort.assessments.length || !gradeableLearners.length}><legend>Grade learner</legend><div className="form-group"><label htmlFor="grade-assessment">Assessment</label><select id="grade-assessment" required value={grade.assessmentId} onChange={(event) => setGrade({ ...grade, assessmentId: event.target.value })}><option value="">Select…</option>{cohort.assessments.map((item) => <option key={item.id} value={item.id}>{item.title} / {item.maxScore}</option>)}</select></div><div className="form-group"><label htmlFor="grade-learner">Learner</label><select id="grade-learner" required value={grade.enrollmentId} onChange={(event) => setGrade({ ...grade, enrollmentId: event.target.value })}><option value="">Select…</option>{gradeableLearners.map((item) => <option key={item.enrollmentId} value={item.enrollmentId}>{item.fullName}</option>)}</select></div><div className="form-group"><label htmlFor="grade-score">Score</label><input id="grade-score" type="number" min="0" required value={grade.score} onChange={(event) => setGrade({ ...grade, score: event.target.value })} /></div><div className="form-group"><label htmlFor="grade-feedback">Feedback</label><textarea id="grade-feedback" maxLength="1000" value={grade.feedback} onChange={(event) => setGrade({ ...grade, feedback: event.target.value })} /></div></fieldset><button className="btn btn-primary" disabled={busy || !cohort.assessments.length || !gradeableLearners.length}>Save grade</button></form>
        </div>
      </>}
      <section className="later-panel"><div className="trainer-section-heading"><div><p className="trainer-eyebrow">Certificate gate</p><h2>Trainer review <span className="trainer-count">{submittedRequests.length}</span></h2></div></div>{submittedRequests.length ? <>{submittedRequests.map((item) => <article className="later-review-row" key={item.id}><div><strong>{item.learnerName}</strong><p>{item.courseCode} · {item.courseTitle}</p></div><div className="trainer-actions"><button className="btn btn-primary" type="button" disabled={busy} onClick={() => mutate(() => trainerLaterPhaseApi.reviewCertificate(query.token, item.id, 'Approve', reviewReason), 'Certificate request approved for Admin review.')}>Approve</button><button className="btn btn-ghost" type="button" disabled={busy || !reviewReason.trim()} onClick={() => mutate(() => trainerLaterPhaseApi.reviewCertificate(query.token, item.id, 'Reject', reviewReason), 'Certificate request rejected.')}>Reject</button></div></article>)}<div className="form-group"><label htmlFor="trainer-certificate-note">Review note / required rejection reason</label><textarea id="trainer-certificate-note" maxLength="512" value={reviewReason} onChange={(event) => setReviewReason(event.target.value)} /></div></> : <div className="trainer-empty">No certificate requests await Trainer review.</div>}</section>
    </>}
  </section>;
}
