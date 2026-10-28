import { useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { trainerDeliveryApi } from '../../api/trainerDelivery';
import { formatDate, safeMeetingLink } from './intakeForm';
import useTrainerQuery, { loadTrainerDetail } from './useTrainerQuery';
import { IntakeStatus, TrainerError, TrainerHeader, TrainerLoading } from './TrainerUi';
import TrainerResourcesPanel from './TrainerResourcesPanel';
import TrainerSessionMaterialsPanel from './TrainerSessionMaterialsPanel';
import TrainerReservationPanel from './TrainerReservationPanel';
import { formatCount, formatUtcDate } from '../businessPresentation';

function SessionCard({ session, deliveryEditable, onDelivery }) {
  const meetingLink = safeMeetingLink(session.meetingLink);
  return <article className="trainer-session-card">
    <div className="trainer-section-heading"><div><span className="trainer-eyebrow">Session #{session.id}</span><h3>{session.label}</h3></div><div className="trainer-actions">{deliveryEditable ? <button className="btn btn-ghost" type="button" onClick={onDelivery}>Update delivery link</button> : null}</div></div>
    <p className="trainer-session-time">{formatDate(session.startsAt)} → {formatDate(session.endsAt)}</p>
    <div className="trainer-session-locations">
      {session.meetingLink ? <div><strong>Online</strong>{meetingLink ? <a href={meetingLink} target="_blank" rel="noopener noreferrer">{meetingLink}</a> : <span>Invalid meeting link</span>}</div> : null}
      {session.physicalAddress ? <div><strong>Physical · {formatCount(session.physicalCapacity, 'seat')}</strong><span>{session.physicalAddress}</span><small>Booking closes {formatDate(session.physicalBookingDeadline)}</small></div> : null}
    </div>
  </article>;
}

function DeliveryForm({ session, busy, error, onCancel, onSave }) {
  const [meetingLink, setMeetingLink] = useState(session.meetingLink || '');
  return <form className="trainer-form trainer-delivery-form" onSubmit={(event) => { event.preventDefault(); onSave(meetingLink.trim() || null); }}>
    <fieldset disabled={busy}><legend>Update “{session.label}” delivery link</legend>
      <div className="form-group"><label htmlFor={`delivery-${session.id}`}>Online meeting link</label><input id={`delivery-${session.id}`} type="url" maxLength={2048} placeholder="https://…" value={meetingLink} onChange={(event) => setMeetingLink(event.target.value)} required={!session.physicalAddress} aria-invalid={error?.fieldErrors?.meetingLink ? true : undefined} /></div>
    </fieldset>
    <div className="trainer-actions"><button className="btn btn-primary" disabled={busy}>{busy ? 'Saving…' : 'Save delivery link'}</button><button className="btn btn-ghost" type="button" disabled={busy} onClick={onCancel}>Cancel</button></div>
  </form>;
}

function IntakeWorkspace({ query }) {
  const { intake, courses, catalogError } = query.data;
  const [notice, setNotice] = useState('');
  const [editor, setEditor] = useState(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);
  const course = courses.find((item) => item.id === intake.courseId);
  const deliveryEditable = ['Published', 'InProgress'].includes(intake.status);
  const sessions = [...intake.sessions].sort((first, second) => new Date(first.startsAt) - new Date(second.startsAt));

  async function saveDelivery(meetingLink) {
    if (busy) return;
    setBusy(true);
    setError(null);
    setNotice('');
    try {
      const updated = await trainerDeliveryApi.updateMeetingLink(query.token, intake.id, editor.session.id, meetingLink, intake.version);
      query.setData({ ...query.data, intake: updated });
      setEditor(null);
      setNotice('Delivery link updated.');
    } catch (failure) { setError(failure); }
    finally { setBusy(false); }
  }

  return <>
    <TrainerHeader eyebrow={`${course?.code || `Course #${intake.courseId}`} / Intake #${intake.id}`} title={course?.title || `Course #${intake.courseId}`} action={<IntakeStatus status={intake.status} />}>
      This class was assigned to you. The course Creator owns the schedule.
    </TrainerHeader>
    {notice ? <div className="trainer-notice" role="status">{notice}</div> : null}
    {catalogError ? <p className="trainer-help">Course names are unavailable ({catalogError.code || 'NETWORK_ERROR'}). Your Intake data is still available.</p> : null}
    <TrainerError error={error} />
    {intake.confirmedToRunAt ? <p className="trainer-notice">Class confirmed to run {formatDate(intake.confirmedToRunAt)}. Reserved credits captured.</p> : null}
    {intake.cancelledAt ? <p className="trainer-help">Cancelled {formatDate(intake.cancelledAt)}{intake.cancellationReason === 'MinimumEnrollmentNotMet' ? ': minimum enrollment was not met' : '.'}</p> : null}
    {intake.replacementForIntakeId ? <p>Replacement for <Link to={`/trainer/courses/intakes/${intake.replacementForIntakeId}`}>Intake #{intake.replacementForIntakeId}</Link>.</p> : null}
    {intake.replacementIntakeId ? <p>Postponed to <Link to={`/trainer/courses/intakes/${intake.replacementIntakeId}`}>Intake #{intake.replacementIntakeId}</Link>.</p> : null}
    <div className="trainer-section-heading"><h2>Registration & delivery</h2></div>
    <dl className="trainer-schedule-summary">{[['Registration opens', intake.registrationOpensAt, formatUtcDate], ['Registration closes', intake.registrationClosesAt, formatUtcDate], ['Delivery starts', intake.startsAt, formatDate], ['Delivery ends', intake.endsAt, formatDate]].map(([label, value, format]) => <div key={label}><dt>{label}</dt><dd>{format(value)}</dd></div>)}<div><dt>Minimum enrollment</dt><dd>{formatCount(intake.minEnrollment ?? 10, 'learner')}</dd></div></dl>
    <TrainerReservationPanel key={intake.id} intake={intake} />
    <div className="trainer-section-heading"><div><p className="trainer-eyebrow">Teaching plan</p><h2>Sessions <span className="trainer-count">{sessions.length}</span></h2></div></div>
    {editor?.type === 'delivery' ? <DeliveryForm key={`${intake.version}:${editor.session.id}`} session={editor.session} error={error} busy={busy} onCancel={() => setEditor(null)} onSave={saveDelivery} /> : null}
    {sessions.length ? <div className="trainer-sessions">{sessions.map((session) => <SessionCard key={session.id} session={session} deliveryEditable={deliveryEditable && !editor && !busy} onDelivery={() => setEditor({ type: 'delivery', session })} />)}</div> : <div className="trainer-empty"><strong>No sessions yet</strong><p>The course Creator adds sessions before this class is published.</p></div>}
    {deliveryEditable ? <TrainerResourcesPanel token={query.token} intake={intake} /> : null}
    {deliveryEditable ? <TrainerSessionMaterialsPanel token={query.token} intake={intake} /> : null}
    <p className="trainer-detail-footer">Intake #{intake.id} · Course #{intake.courseId}</p>
  </>;
}

export default function TrainerIntakeDetailPage() {
  const { courseIntakeId } = useParams();
  const query = useTrainerQuery(loadTrainerDetail, courseIntakeId);
  return <section className="trainer-page"><div className="trainer-detail-nav"><Link className="trainer-back" to="/trainer/courses">← All Intakes</Link></div>
    {query.loading ? <TrainerLoading /> : query.error ? <TrainerError error={query.error} onRetry={query.refresh} /> : <IntakeWorkspace key={`${query.token}:${courseIntakeId}`} query={query} />}
  </section>;
}
