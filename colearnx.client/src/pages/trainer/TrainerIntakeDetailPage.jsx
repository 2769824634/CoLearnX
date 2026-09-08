import { useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { trainerIntakesApi } from '../../api/trainerIntakes';
import { trainerDeliveryApi } from '../../api/trainerDelivery';
import { ScheduleForm, SessionForm } from './IntakeForms';
import { formatDate, isEditable, localTimezone, safeMeetingLink } from './intakeForm';
import useTrainerQuery, { loadTrainerDetail } from './useTrainerQuery';
import { IntakeStatus, TrainerError, TrainerHeader, TrainerLoading } from './TrainerUi';
import ChangeRequestEditor from './ChangeRequestEditor';

function SessionCard({ session, editable, deliveryEditable, onEdit, onDelete, onDelivery }) {
  const meetingLink = safeMeetingLink(session.meetingLink);
  return <article className="trainer-session-card">
    <div className="trainer-section-heading"><div><span className="trainer-eyebrow">Session #{session.id}</span><h3>{session.label}</h3></div><div className="trainer-actions">{deliveryEditable ? <button className="btn btn-ghost" type="button" onClick={onDelivery}>Update delivery link</button> : null}{editable ? <><button className="btn btn-ghost" type="button" onClick={onEdit}>Edit Session</button><button className="btn btn-ghost" type="button" onClick={onDelete}>Delete Session</button></> : null}</div></div>
    <p className="trainer-session-time">{formatDate(session.startsAt)} → {formatDate(session.endsAt)}</p>
    <div className="trainer-session-locations">
      {session.meetingLink ? <div><strong>Online</strong>{meetingLink ? <a href={meetingLink} target="_blank" rel="noopener noreferrer">{meetingLink}</a> : <span>Invalid meeting link</span>}</div> : null}
      {session.physicalAddress ? <div><strong>Physical · {session.physicalCapacity} seats</strong><span>{session.physicalAddress}</span><small>Booking closes {formatDate(session.physicalBookingDeadline)}</small></div> : null}
    </div>
  </article>;
}

function DeliveryForm({ session, busy, blocked, error, onCancel, onSave }) {
  const [meetingLink, setMeetingLink] = useState(session.meetingLink || '');
  return <form className="trainer-form trainer-delivery-form" onSubmit={(event) => { event.preventDefault(); onSave(meetingLink.trim() || null); }}>
    <fieldset disabled={busy || blocked}><legend>Update “{session.label}” delivery link</legend><p>This routine action does not reopen the confirmed schedule.</p>
      <div className="form-group"><label htmlFor={`delivery-${session.id}`}>Online meeting link</label><input id={`delivery-${session.id}`} type="url" maxLength={2048} placeholder="https://…" value={meetingLink} onChange={(event) => setMeetingLink(event.target.value)} required={!session.physicalAddress} aria-invalid={error?.fieldErrors?.meetingLink ? true : undefined} /></div>
      {session.physicalAddress ? <p className="trainer-help">Leave blank to remove the online link; the physical location remains available.</p> : null}
    </fieldset><div className="trainer-actions"><button className="btn btn-primary" disabled={busy || blocked}>{busy ? 'Saving…' : 'Save delivery link'}</button><button className="btn btn-ghost" type="button" disabled={busy} onClick={onCancel}>Cancel</button></div>
  </form>;
}

function IntakeWorkspace({ query }) {
  const { intake, courses, catalogError } = query.data;
  const [notice, setNotice] = useState('');
  const [editor, setEditor] = useState(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);
  const [blocked, setBlocked] = useState(false);
  const course = courses.find((item) => item.id === intake.courseId);
  const rejectedChange = intake.status === 'Rejected' && Boolean(intake.latestChangeRequest);
  const editable = isEditable(intake.status) && !rejectedChange;
  const deliveryEditable = ['Published', 'InProgress'].includes(intake.status);
  const changeEditable = deliveryEditable || rejectedChange;
  function openEditor(next) { setEditor(next); if (!blocked) setError(null); setNotice(''); }

  async function mutate(command, message) {
    if (busy || blocked) return;
    setBusy(true);
    setError(null);
    setNotice('');
    try {
      const updated = await command();
      query.setData({ ...query.data, intake: updated });
      setEditor(null);
      setNotice(message);
    } catch (failure) {
      setError(failure);
      if (['INTAKE_VERSION_CONFLICT', 'INTAKE_NOT_EDITABLE', 'INTAKE_NOT_FOUND', 'UNAUTHENTICATED', 'TRAINER_REQUIRED'].includes(failure.code)) setBlocked(true);
    } finally { setBusy(false); }
  }

  const sessions = [...intake.sessions].sort((first, second) => new Date(first.startsAt) - new Date(second.startsAt));
  const editingSession = editor?.session;
  return <>
    <TrainerHeader eyebrow={`${course?.code || `Course #${intake.courseId}`} / Intake #${intake.id}`} title={course?.title || `Course #${intake.courseId}`} action={<IntakeStatus status={intake.status} />}>
      Your cohort schedule · All times in {localTimezone}
    </TrainerHeader>
    {notice ? <div className="trainer-notice" role="status">{notice}</div> : null}
    {catalogError ? <p className="trainer-help">Course names are unavailable ({catalogError.code || 'NETWORK_ERROR'}). Your Intake data is still available.</p> : null}
    <TrainerError error={error} onRetry={blocked ? query.refresh : undefined} retryLabel="Discard local edits and reload Intake" />
    {intake.status === 'PendingApproval' ? <div className="trainer-workflow-note"><strong>Submitted to the course Creator</strong><p>This Intake is waiting for confirmation. Schedule and Session editing are locked.</p><span>Submitted {formatDate(intake.submittedAt)}. Creator confirmation is not part of this workspace.</span></div> : null}
    {intake.status === 'Rejected' ? <div className="trainer-workflow-note"><strong>The Creator requested changes</strong><p>{intake.confirmationNote || 'No confirmation note was provided.'}</p><span>{rejectedChange ? 'Revise the retained proposal and resubmit it; the last confirmed schedule remains unchanged.' : 'Saving a change returns this Intake to Draft and clears the previous confirmation record.'}</span></div> : null}
    {!editable && !changeEditable && intake.status !== 'PendingApproval' ? <div className="trainer-workflow-note"><strong>This Intake is read-only here.</strong><p>No delivery or structural action is available in its current lifecycle state.</p></div> : null}
    {intake.confirmedAt ? <p className="trainer-help">Creator confirmation recorded {formatDate(intake.confirmedAt)}{intake.confirmationNote ? ` · ${intake.confirmationNote}` : ''}</p> : null}
    <div className="trainer-section-heading"><h2>Registration & delivery</h2>{editable && !editor ? <button type="button" className="btn btn-ghost" disabled={busy || blocked} onClick={() => openEditor({ type: 'schedule' })}>Edit schedule</button> : null}</div>
    <dl className="trainer-schedule-summary">{[['Registration opens', intake.registrationOpensAt], ['Registration closes', intake.registrationClosesAt], ['Delivery starts', intake.startsAt], ['Delivery ends', intake.endsAt]].map(([label, value]) => <div key={label}><dt>{label}</dt><dd>{formatDate(value)}</dd></div>)}</dl>
    {editor?.type === 'schedule' ? <ScheduleForm key={intake.version} intake={intake} error={error} onError={setError} busy={busy} blocked={blocked} onCancel={() => openEditor(null)} onSave={(body) => mutate(() => trainerIntakesApi.update(query.token, intake.id, body), 'Intake schedule saved.')} /> : null}
    <div className="trainer-section-heading"><div><p className="trainer-eyebrow">Teaching plan</p><h2>Sessions <span className="trainer-count">{sessions.length}</span></h2></div>{editable && !editor ? <button type="button" className="btn btn-primary" disabled={busy || blocked} onClick={() => openEditor({ type: 'session' })}>+ Add Session</button> : null}</div>
    {editor?.type === 'session' ? <SessionForm key={`${intake.version}:${editingSession?.id || 'new'}`} session={editingSession} intake={intake} error={error} onError={setError} busy={busy} blocked={blocked} onCancel={() => openEditor(null)} onSave={(body) => mutate(() => editingSession ? trainerIntakesApi.updateSession(query.token, intake.id, editingSession.id, body) : trainerIntakesApi.createSession(query.token, intake.id, body), editingSession ? 'Session updated.' : 'Session added.')} /> : null}
    {editor?.type === 'delivery' ? <DeliveryForm key={`${intake.version}:${editingSession.id}`} session={editingSession} error={error} busy={busy} blocked={blocked} onCancel={() => openEditor(null)} onSave={(meetingLink) => mutate(() => trainerDeliveryApi.updateMeetingLink(query.token, intake.id, editingSession.id, meetingLink, intake.version), 'Delivery link updated without reopening the schedule.')} /> : null}
    {sessions.length ? <div className="trainer-sessions">{sessions.map((session) => <SessionCard key={session.id} session={session} editable={editable && !editor && !busy && !blocked} deliveryEditable={deliveryEditable && !editor && !busy && !blocked} onDelivery={() => openEditor({ type: 'delivery', session })} onEdit={() => openEditor({ type: 'session', session })} onDelete={() => openEditor({ type: 'delete', session })} />)}</div> : <div className="trainer-empty"><strong>No Sessions yet</strong><p>Add at least one valid Session before submitting to the Creator.</p></div>}
    {editor?.type === 'delete' ? <div className="trainer-confirm" role="region" aria-label="Confirm Session deletion"><h2>Delete “{editingSession.label}”?</h2><p>This removes the Session from this Draft. Sessions with enrolment or attendance history cannot be deleted.</p><div className="trainer-actions"><button type="button" className="btn btn-primary" disabled={busy || blocked} onClick={() => mutate(() => trainerIntakesApi.deleteSession(query.token, intake.id, editingSession.id, intake.version), 'Session deleted.')}>{busy ? 'Deleting…' : 'Confirm deletion'}</button><button type="button" className="btn btn-ghost" disabled={busy} onClick={() => openEditor(null)}>Cancel</button></div></div> : null}
    {editable ? <div className="trainer-submit-panel"><div><p className="trainer-eyebrow">Next step</p><h2>Ready for Creator confirmation?</h2><p>Submit the saved schedule and Sessions. Your Intake will be locked while the Creator reviews it.</p></div>
      {editor?.type === 'submit' ? <div className="trainer-confirm" role="region" aria-label="Confirm Intake submission"><strong>Submit Intake #{intake.id}?</strong><p>This sends a request to the course Creator. It does not publish the Intake.</p><div className="trainer-actions"><button type="button" className="btn btn-primary" disabled={busy || blocked} onClick={() => mutate(() => trainerIntakesApi.submit(query.token, intake.id, intake.version), 'Intake submitted. Waiting for Creator confirmation.')}>{busy ? 'Submitting…' : 'Confirm submission'}</button><button type="button" className="btn btn-ghost" disabled={busy} onClick={() => openEditor(null)}>Cancel</button></div></div> : <div><button type="button" className="btn btn-primary" disabled={Boolean(editor) || busy || blocked || !sessions.length} onClick={() => openEditor({ type: 'submit' })}>Submit to Creator</button>{!sessions.length ? <p className="trainer-help">Add a Session to enable submission.</p> : editor ? <p className="trainer-help">Save or cancel your current edit first.</p> : null}</div>}
    </div> : null}
    {changeEditable && editor?.type !== 'change' ? <div className="trainer-submit-panel"><div><p className="trainer-eyebrow">Material changes</p><h2>{rejectedChange ? 'Revise the rejected proposal' : 'Need to change the confirmed schedule?'}</h2><p>Dates, labels, physical delivery and Session structure require Creator reconfirmation.</p></div><button type="button" className="btn btn-ghost" disabled={Boolean(editor) || busy || blocked} onClick={() => openEditor({ type: 'change' })}>{rejectedChange ? 'Revise change request' : 'Prepare change request'}</button></div> : null}
    {editor?.type === 'change' ? <ChangeRequestEditor key={`${intake.version}:${intake.latestChangeRequest?.applicationId || 'new'}`} intake={intake} busy={busy} blocked={blocked} error={error} onError={setError} onCancel={() => openEditor(null)} onSubmit={(body) => mutate(() => trainerIntakesApi.requestChange(query.token, intake.id, body), 'Change request submitted. The current confirmed schedule remains in place until approval.')} /> : null}
    <p className="trainer-detail-footer">Intake #{intake.id} · Course #{intake.courseId} · Trainer #{intake.trainerId}</p>
  </>;
}

export default function TrainerIntakeDetailPage() {
  const { courseIntakeId } = useParams();
  const query = useTrainerQuery(loadTrainerDetail, courseIntakeId);
  return <section className="trainer-page"><div className="trainer-detail-nav"><Link className="trainer-back" to="/trainer/courses">← All Intakes</Link></div>
    {query.loading ? <TrainerLoading /> : query.error ? <TrainerError error={query.error} onRetry={query.refresh} /> : <IntakeWorkspace key={`${query.token}:${courseIntakeId}`} query={query} />}
  </section>;
}
