import { useState } from 'react';
import { clearFieldErrors, initialSchedule, initialSession, localTimezone, schedulePayload, sessionPayload } from './intakeForm';
import { TrainerField } from './TrainerUi';

function FormActions({ busy, blocked, onCancel, label }) {
  return <div className="trainer-actions"><button type="submit" className="btn btn-primary" disabled={busy || blocked}>{busy ? 'Saving…' : label}</button>{onCancel ? <button type="button" className="btn btn-ghost" disabled={busy} onClick={onCancel}>Cancel</button> : null}</div>;
}

export function ScheduleForm({ intake, courses, onSave, onCancel, busy, blocked, error, onError, allowMinEnrollment = true, submitLabel }) {
  const [values, setValues] = useState(() => initialSchedule(intake));
  const [courseId, setCourseId] = useState('');
  function clearField(name) {
    const nextError = clearFieldErrors(error, name);
    if (nextError !== error) onError(nextError);
  }
  const change = (event) => {
    setValues((current) => ({ ...current, [event.target.name]: event.target.value }));
    clearField(event.target.name);
  };
  function submit(event) {
    event.preventDefault();
    try { onSave(schedulePayload(values, intake?.version, intake), courseId); } catch (failure) { onError(failure); }
  }
  return <form onSubmit={submit} className="trainer-form">
    <fieldset disabled={busy || blocked}><legend>{intake ? 'Edit Intake schedule' : 'Create a Draft'}</legend>
      <p>Use local time ({localTimezone}). Dates are saved in UTC.</p>
      {!intake ? <TrainerField name="courseId" label="Published course" value={courseId} onChange={(event) => setCourseId(event.target.value)} required error={error}><option value="">Choose a published course</option>{courses.map((course) => <option key={course.id} value={course.id}>{course.code} — {course.title}</option>)}</TrainerField> : null}
      <div className="trainer-form-grid">{[['registrationOpensAt', 'Registration opens'], ['registrationClosesAt', 'Registration closes'], ['startsAt', 'Delivery starts'], ['endsAt', 'Delivery ends']].map(([name, label]) => <TrainerField key={name} name={name} label={label} type="datetime-local" step="60" value={values[name]} onChange={change} required error={error} />)}</div>
      {allowMinEnrollment ? <TrainerField name="minEnrollment" label="Minimum enrollment" type="number" min="2" max="200" step="1" value={values.minEnrollment} onChange={change} required error={error} /> : null}
      <p className="trainer-help">Registration must close at least 10 days before delivery starts. All Sessions must fit inside the delivery period.</p>
    </fieldset>
    <FormActions busy={busy} blocked={blocked} onCancel={onCancel} label={submitLabel || (intake ? 'Save schedule' : 'Create Draft')} />
  </form>;
}

export function SessionForm({ session, intake, onSave, onCancel, busy, blocked, error, onError }) {
  const [values, setValues] = useState(() => initialSession(session, intake));
  function clearField(name) {
    const nextError = clearFieldErrors(error, name);
    if (nextError !== error) onError(nextError);
  }
  const change = (event) => {
    setValues((current) => ({ ...current, [event.target.name]: event.target.type === 'checkbox' ? event.target.checked : event.target.value }));
    clearField(event.target.name);
  };
  function submit(event) {
    event.preventDefault();
    try { onSave(sessionPayload(values, intake, session)); } catch (failure) { onError(failure); }
  }
  return <form onSubmit={submit} className="trainer-form">
    <fieldset disabled={busy || blocked}><legend>{session ? 'Edit Session' : 'Add a Session'}</legend>
      <p>Use local time ({localTimezone}). Provide an online link, a physical location, or both.</p>
      <TrainerField name="label" label="Session label" maxLength={128} value={values.label} onChange={change} required error={error} />
      <div className="trainer-form-grid">{[['startsAt', 'Session starts'], ['endsAt', 'Session ends']].map(([name, label]) => <TrainerField key={name} name={name} label={label} type="datetime-local" step="60" value={values[name]} min={initialSchedule(intake).startsAt} max={initialSchedule(intake).endsAt} onChange={change} required error={error} />)}</div>
      <TrainerField name="meetingLink" label="Online meeting link (optional with a physical location)" type="url" maxLength={2048} placeholder="https://…" value={values.meetingLink} onChange={change} error={error} />
      <label className="trainer-checkbox"><input name="physical" type="checkbox" checked={values.physical} onChange={change} /> Include a physical location</label>
      {values.physical ? <div className="trainer-physical-fields">
        <TrainerField name="physicalAddress" label="Physical address" maxLength={512} value={values.physicalAddress} onChange={change} required error={error} />
        <div className="trainer-form-grid"><TrainerField name="physicalCapacity" label="Physical capacity" type="number" min="1" max="2147483647" step="1" value={values.physicalCapacity} onChange={change} required error={error} />
          <TrainerField name="physicalBookingDeadline" label="Physical booking deadline" type="datetime-local" step="60" max={values.startsAt} value={values.physicalBookingDeadline} onChange={change} required error={error} /></div>
        <p className="trainer-help">Physical and online attendance share this Session's capacity. The physical booking deadline applies, and Members do not choose a mode yet.</p>
      </div> : null}
    </fieldset>
    <FormActions busy={busy} blocked={blocked} onCancel={onCancel} label={session ? 'Save Session' : 'Add Session'} />
  </form>;
}
