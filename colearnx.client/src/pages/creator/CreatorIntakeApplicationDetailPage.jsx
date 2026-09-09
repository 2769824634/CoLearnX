import { useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { creatorIntakeApplicationsApi } from '../../api/creatorIntakeApplications';
import { reviewPayload } from '../trainer/b4Workflow';
import { formatDate } from '../trainer/intakeForm';
import useTrainerQuery from '../trainer/useTrainerQuery';
import { CreatorError, CreatorHeader } from './CreatorUi';

const loadCreatorApplication = (token, id, signal) => creatorIntakeApplicationsApi.get(token, id, signal);

function Schedule({ title, intake, accent = false }) {
  return <section className={`creator-schedule${accent ? ' proposed' : ''}`}><div className="creator-section-heading"><div><p className="creator-eyebrow">{accent ? 'Awaiting your decision' : 'Currently active'}</p><h2>{title}</h2></div></div>
    <dl>{[['Registration opens', intake.registrationOpensAt], ['Registration closes', intake.registrationClosesAt], ['Delivery starts', intake.startsAt], ['Delivery ends', intake.endsAt]].map(([label, value]) => <div key={label}><dt>{label}</dt><dd>{formatDate(value)}</dd></div>)}</dl>
    <div className="creator-session-list">{intake.sessions.map((session, index) => <article key={session.id || `${session.label}-${index}`}><span>{String(index + 1).padStart(2, '0')}</span><div><strong>{session.label}</strong><p>{formatDate(session.startsAt)} → {formatDate(session.endsAt)}</p><small>{session.meetingLink ? 'Online' : ''}{session.meetingLink && session.physicalAddress ? ' + ' : ''}{session.physicalAddress ? `${session.physicalAddress} · ${session.physicalCapacity} seats` : ''}</small></div></article>)}</div>
  </section>;
}

function ReviewDesk({ application, token, intakeId, onReviewed }) {
  const [note, setNote] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);
  async function review(decision) {
    if (busy) return;
    setBusy(true); setError(null);
    try { await creatorIntakeApplicationsApi.review(token, intakeId, reviewPayload(decision, note, application.version)); onReviewed(); }
    catch (failure) { setError(failure); }
    finally { setBusy(false); }
  }
  return <aside className="creator-review-desk"><p className="creator-eyebrow">Decision</p><h2>Creator confirmation</h2><p>Confirm publishes a new Intake or applies the proposed change. Reject keeps the current confirmed schedule and requires a clear note.</p><label htmlFor="creator-confirmation-note">Confirmation note</label><textarea id="creator-confirmation-note" maxLength={512} rows={5} value={note} onChange={(event) => setNote(event.target.value)} placeholder="Required when returning changes" />
    <CreatorError error={error} /><div className="creator-review-actions"><button type="button" className="btn btn-primary" disabled={busy} onClick={() => review('Confirm')}>{busy ? 'Saving…' : 'Confirm application'}</button><button type="button" className="btn btn-ghost" disabled={busy || !note.trim()} onClick={() => review('Reject')}>Return with note</button></div>
  </aside>;
}

function ApplicationWorkspace({ query }) {
  const detail = query.data;
  const { application, currentIntake, proposedChange } = detail;
  return <><CreatorHeader eyebrow={`${application.courseCode} / Intake #${application.courseIntakeId}`} title={application.courseTitle} action={<span className={`creator-status ${application.status.toLowerCase()}`}>{application.status}</span>}>Trainer: {application.trainerName} · {application.kind === 'Change' ? 'Structural change request' : 'New Intake application'}</CreatorHeader>
    <div className="creator-review-layout"><main><Schedule title={proposedChange ? 'Confirmed schedule' : 'Submitted schedule'} intake={currentIntake} />{proposedChange ? <Schedule title="Proposed replacement" intake={proposedChange} accent /> : null}</main>{application.status === 'Pending' ? <ReviewDesk application={application} token={query.token} intakeId={application.courseIntakeId} onReviewed={query.refresh} /> : <aside className="creator-review-desk closed"><p className="creator-eyebrow">Decision recorded</p><h2>{application.status}</h2><p>{currentIntake.confirmationNote || 'No note was recorded.'}</p></aside>}</div>
  </>;
}

export default function CreatorIntakeApplicationDetailPage() {
  const { courseIntakeId } = useParams();
  const query = useTrainerQuery(loadCreatorApplication, courseIntakeId);
  return <section className="creator-page"><Link className="creator-back" to="/creator/courses">← Intake applications</Link>{query.loading ? <div className="creator-empty">Loading application…</div> : query.error ? <CreatorError error={query.error} onRetry={query.refresh} /> : <ApplicationWorkspace key={`${courseIntakeId}:${query.data.application.version}:${query.data.application.status}`} query={query} />}</section>;
}
