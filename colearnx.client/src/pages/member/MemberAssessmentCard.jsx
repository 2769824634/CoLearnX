import { useState } from 'react';
import { enrollmentsApi } from '../../api';
import { formatUtcDateTime, userFacingError } from '../businessPresentation';
import AssignmentFileRow from '../../components/AssignmentFileRow';
import {
  ASSIGNMENT_ACCEPT, ASSIGNMENT_MAX_FILES, assignmentSelectionError, deadlineState,
} from '../../utils/assignmentFiles';
import '../../styles/assessment-submission.css';

function badgeFor(item, deadline, submitted) {
  if (item.score != null) return item.status === 'Passed' ? ['passed', 'Passed'] : ['failed', 'Not passed'];
  if (submitted) return ['submitted', 'Submitted'];
  if (deadline.closed || !item.canSubmit) return ['closed', 'Not submitted'];
  return ['open', 'Open'];
}

// One assessment in the Member Learning Hub, with the assignment hand-in while the deadline is open.
export default function MemberAssessmentCard({ item, enrollmentId, token, onSubmitted, showToast }) {
  const [selected, setSelected] = useState([]);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const [dragging, setDragging] = useState(false);
  const files = item.files || [];
  const submitted = files.length > 0;
  const deadline = deadlineState(item.dueAt);
  const open = Boolean(item.canSubmit) && !deadline.closed;
  const [badgeTone, badgeLabel] = badgeFor(item, deadline, submitted);

  function addFiles(list) {
    const incoming = Array.from(list || []);
    if (!incoming.length) return;
    const next = [...selected];
    incoming.forEach((file) => {
      if (!next.some((existing) => existing.name === file.name && existing.size === file.size)) next.push(file);
    });
    setSelected(next);
    setError(next.length > ASSIGNMENT_MAX_FILES ? assignmentSelectionError(next) : '');
  }

  async function submit() {
    const problem = assignmentSelectionError(selected);
    if (problem) { setError(problem); return; }
    setBusy(true);
    setError('');
    try {
      await enrollmentsApi.submitAssessment(enrollmentId, item.id, selected, token);
      setSelected([]);
      showToast?.(submitted ? 'Submission replaced.' : 'Assignment submitted.');
      onSubmitted?.();
    } catch (failure) {
      setError(userFacingError(failure, 'Your documents could not be submitted. Please try again.'));
    } finally {
      setBusy(false);
    }
  }

  async function download(file) {
    try { await enrollmentsApi.downloadAssessmentSubmission(enrollmentId, item.id, file.id, file.fileName, token); }
    catch (failure) { showToast?.(userFacingError(failure, 'Could not download this document. Please try again.')); }
  }

  return <article className={`cx-assess-card${open ? ' is-open' : ''}`} aria-label={item.title}>
    <div className="cx-assess-head">
      <div>
        <h4>{item.title}</h4>
        <div className="cx-assess-meta">
          <span>Pass score {item.passScore}/{item.maxScore}</span>
          <span>{item.dueAt ? `Due ${formatUtcDateTime(item.dueAt)}` : 'No due date'}</span>
        </div>
      </div>
      <span className={`cx-assess-badge ${badgeTone}`}>{badgeLabel}</span>
    </div>
    {item.dueAt && item.score == null ? <div className={`cx-assess-due ${deadline.tone}`} role="status"><strong>{deadline.label}</strong></div> : null}
    <div className="cx-assess-body">
      {item.score != null ? <div className={`cx-assess-grade${item.status === 'Passed' ? '' : ' failed'}`}>
        <strong>{item.score}/{item.maxScore} · {item.status}</strong>
        {item.feedback ? <p>{item.feedback}</p> : null}
      </div> : null}

      {submitted ? <section aria-label={`${item.title} submitted documents`}>
        <p className="cx-assess-section-title">Your submission · {formatUtcDateTime(item.submittedAt)}</p>
        <ul className="cx-file-list">{files.map((file) => <AssignmentFileRow key={file.id} name={file.fileName} format={file.format} size={file.sizeBytes}
          action={<button type="button" className="cx-file-action" onClick={() => download(file)}>Download</button>} />)}</ul>
      </section> : null}

      {open ? <>
        <label className={`cx-dropzone${dragging ? ' dragging' : ''}${busy ? ' disabled' : ''}`}
          onDragOver={(event) => { event.preventDefault(); setDragging(true); }}
          onDragLeave={() => setDragging(false)}
          onDrop={(event) => { event.preventDefault(); setDragging(false); if (!busy) addFiles(event.dataTransfer.files); }}>
          <input type="file" multiple accept={ASSIGNMENT_ACCEPT} disabled={busy}
            aria-label={`Choose documents for ${item.title}`}
            onChange={(event) => { addFiles(event.target.files); event.target.value = ''; }} />
          <span className="cx-dropzone-icon" aria-hidden="true">+</span>
          <strong>{submitted ? 'Choose new documents to replace your submission' : 'Drop documents here or click to browse'}</strong>
          <small>PDF, DOCX, PPTX, PNG or JPG · up to {ASSIGNMENT_MAX_FILES} files · 10 MB each, 25 MB in total</small>
        </label>
        {selected.length ? <ul className="cx-file-list" aria-label="Documents ready to submit">{selected.map((file, index) => <AssignmentFileRow key={`${file.name}-${file.size}`}
          name={file.name} size={file.size} note="Ready"
          action={<button type="button" className="cx-file-action remove" disabled={busy} aria-label={`Remove ${file.name}`}
            onClick={() => { const next = selected.filter((_, position) => position !== index); setSelected(next); setError(next.length > ASSIGNMENT_MAX_FILES ? assignmentSelectionError(next) : ''); }}>Remove</button>} />)}</ul> : null}
        {error ? <p className="cx-assess-error" role="alert">{error}</p> : null}
        <div className="cx-assess-actions">
          <p>{submitted ? 'Submitting again replaces all of your current documents.' : 'You can resubmit until the deadline.'}</p>
          <button type="button" className="btn btn-primary btn-sm" disabled={busy || !selected.length} onClick={submit}>
            {busy ? 'Uploading…' : submitted ? 'Replace submission' : `Submit ${selected.length > 1 ? `${selected.length} documents` : 'assignment'}`}
          </button>
        </div>
      </> : item.score == null ? <p className="cx-assess-closed-note">
        {deadline.closed ? `The deadline passed on ${formatUtcDateTime(item.dueAt)}. Submissions are closed.` : 'Submissions are closed for this assessment.'}
      </p> : null}
    </div>
  </article>;
}
