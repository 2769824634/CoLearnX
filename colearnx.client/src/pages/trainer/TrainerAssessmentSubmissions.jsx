import { useState } from 'react';
import { formatUtcDateTime } from '../businessPresentation';
import { deadlineState } from '../../utils/assignmentFiles';
import AssignmentFileRow from '../../components/AssignmentFileRow';
import { TrainerError } from './TrainerUi';
import '../../styles/assessment-submission.css';

// Documents learners handed in for each assessment of one Intake, ready to download while grading.
export default function TrainerAssessmentSubmissions({ assessments = [], learners = [], token, api }) {
  const [error, setError] = useState(null);
  if (!assessments.length) return null;

  async function download(assessmentId, file) {
    setError(null);
    try { await api.downloadSubmission(token, assessmentId, file.id, file.fileName); }
    catch (failure) { setError(failure); }
  }

  return <section className="later-panel" aria-label="Assignment submissions">
    <div className="trainer-section-heading"><div><p className="trainer-eyebrow">Assignments</p><h2>Submitted documents</h2></div></div>
    <TrainerError error={error} />
    <div className="trainer-submissions">{assessments.map((assessment) => {
      const submissions = assessment.submissions || [];
      const submittedIds = new Set(submissions.map((item) => item.enrollmentId));
      const missing = learners.filter((learner) => !submittedIds.has(learner.enrollmentId));
      const total = learners.length;
      const percent = total ? Math.round((submissions.length / total) * 100) : 0;
      const deadline = deadlineState(assessment.dueAt);
      return <article key={assessment.id} className="trainer-submission-card">
        <header>
          <div>
            <h3>{assessment.title}</h3>
            <p>{assessment.dueAt ? `Due ${formatUtcDateTime(assessment.dueAt)} · ${deadline.label}` : 'No submission deadline'}</p>
          </div>
          <span className="trainer-submission-progress">
            <span className="progress-bar"><span className="fill" style={{ width: `${percent}%` }} /></span>
            {submissions.length}/{total} submitted
          </span>
        </header>
        {submissions.length ? <ul className="trainer-submission-rows">{submissions.map((submission) => <li key={submission.enrollmentId}>
          <div><strong>{submission.learnerName}</strong><small>Submitted {formatUtcDateTime(submission.submittedAt)}</small></div>
          <ul className="cx-file-list">{submission.files.map((file) => <AssignmentFileRow key={file.id} name={file.fileName} format={file.format} size={file.sizeBytes}
            action={<button type="button" className="cx-file-action" aria-label={`Download ${file.fileName} from ${submission.learnerName}`} onClick={() => download(assessment.id, file)}>Download</button>} />)}</ul>
        </li>)}</ul> : <p className="trainer-submission-missing">No documents submitted yet.</p>}
        {submissions.length && missing.length ? <p className="trainer-submission-missing">Not submitted: {missing.map((learner) => learner.fullName).join(', ')}</p> : null}
      </article>;
    })}</div>
  </section>;
}
