import { useState } from 'react';
import { clearFieldErrors, fieldMessages, initialSchedule, initialSession, localTimezone, schedulePayload, sessionEndLocal, sessionPayload, todayInput } from './intakeForm';
import { TrainerField } from './TrainerUi';

function WhenField({ name, label, kind, value, onChange, required, error, min, max }) {
  const messages = fieldMessages(error, name);
  const describedBy = messages.length ? `trainer-${name}-error` : undefined;
  const invalid = messages.length ? true : undefined;
  const date = String(value || '').slice(0, 10);
  const time = String(value || '').slice(11, 16);
  function emit(nextDate, nextTime) {
    const clock = String(nextTime || '').slice(0, 5);
    onChange({ target: { name, value: nextDate || clock ? `${nextDate}T${clock}` : '' } });
  }
  return <div className="form-group">
    {kind === 'date'
      ? <><label htmlFor={`trainer-${name}`}>{label}</label><input id={`trainer-${name}`} name={name} type="date" required={required} value={value || ''} min={min} aria-invalid={invalid} aria-describedby={describedBy} onChange={onChange} /></>
      : <><span id={`trainer-${name}-label`}>{label}</span><div className="trainer-datetime" role="group" aria-labelledby={`trainer-${name}-label`}>
        <input type="date" aria-label={`${label} date`} required={required} value={date} min={min ? String(min).slice(0, 10) : undefined} max={max ? String(max).slice(0, 10) : undefined} aria-invalid={invalid} aria-describedby={describedBy} onChange={(event) => emit(event.target.value, time)} />
        <input type="time" aria-label={`${label} time`} required={required} step="60" value={time} aria-invalid={invalid} aria-describedby={describedBy} onChange={(event) => emit(date, event.target.value)} />
      </div></>}
    {messages.length ? <span className="trainer-field-error" id={`trainer-${name}-error`}>{messages.join(' ')}</span> : null}
  </div>;
}

function FormActions({ busy, blocked, onCancel, label }) {
  return <div className="trainer-actions"><button type="submit" className="btn btn-primary" disabled={busy || blocked}>{busy ? 'Saving…' : label}</button>{onCancel ? <button type="button" className="btn btn-ghost" disabled={busy} onClick={onCancel}>Cancel</button> : null}</div>;
}

export function ScheduleForm({ intake, courses, trainers, onCourseChange, onSave, onCancel, busy, blocked, error, onError, allowMinEnrollment = true, submitLabel }) {
  const [values, setValues] = useState(() => initialSchedule(intake));
  const [courseId, setCourseId] = useState('');
  const [trainerId, setTrainerId] = useState('');
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
    try { onSave(schedulePayload(values, intake?.version, intake, trainerId), courseId); } catch (failure) { onError(failure); }
  }
  function setSchedule(key, errorName) {
    return (event) => {
      setValues((current) => ({ ...current, [key]: event.target.value }));
      clearField(errorName);
    };
  }
  return <form onSubmit={submit} className="trainer-form">
    <fieldset disabled={busy || blocked}><legend>{intake ? 'Edit Intake schedule' : 'Create a Draft'}</legend>
      <p>Registration opens on a date at a time in {localTimezone} and stays open for the number of days you enter. Teaching dates have no clock time. Times are saved in UTC.</p>
      {!intake && courses ? <TrainerField name="courseId" label="Published course" value={courseId} onChange={(event) => { setCourseId(event.target.value); setTrainerId(''); onCourseChange?.(event.target.value); }} required error={error}><option value="">Choose a published course</option>{courses.map((course) => <option key={course.id} value={course.id}>{course.code} — {course.title}</option>)}</TrainerField> : null}
      {!intake && trainers ? <TrainerField name="trainerId" label="Accepted Trainer" value={trainerId} onChange={(event) => setTrainerId(event.target.value)} required error={error}><option value="">Choose a Trainer who accepted this course</option>{trainers.map((trainer) => <option key={trainer.trainerId} value={trainer.trainerId}>{trainer.fullName}</option>)}</TrainerField> : null}
      <div className="trainer-form-grid">
        <WhenField name="registrationOpensAt" label="Registration starts" kind="date" value={values.registrationStartDate} onChange={setSchedule('registrationStartDate', 'registrationOpensAt')} required error={error} min={todayInput()} />
        <div className="form-group">
          <label htmlFor="trainer-registrationStartTime">Registration time</label>
          <input id="trainer-registrationStartTime" name="registrationStartTime" type="time" step="60" required value={values.registrationStartTime} onChange={setSchedule('registrationStartTime', 'registrationOpensAt')} />
        </div>
        <TrainerField name="registrationClosesAt" label="Registration length (days)" type="number" min="1" step="1" value={values.registrationDays} onChange={setSchedule('registrationDays', 'registrationClosesAt')} required error={error} />
        <TrainerField name="daysBeforeTeaching" label="Days before teaching" type="number" min="0" step="1" value={values.daysBeforeTeaching} onChange={setSchedule('daysBeforeTeaching', 'daysBeforeTeaching')} required error={error} />
        <WhenField name="startsAt" label="First teaching date" kind="date" value={values.deliveryStartDate} onChange={setSchedule('deliveryStartDate', 'startsAt')} required error={error} />
        <WhenField name="endsAt" label="Last teaching date" kind="date" value={values.deliveryEndDate} onChange={setSchedule('deliveryEndDate', 'endsAt')} required error={error} />
      </div>
      {allowMinEnrollment ? <TrainerField name="minEnrollment" label="Minimum enrollment" type="number" min="2" max="200" step="1" value={values.minEnrollment} onChange={change} required error={error} /> : null}
      <p className="trainer-help">Registration cannot start before the current time. Days before teaching is the gap you choose between registration closing and the first teaching date. Sessions must fall on the teaching dates, each with its own start time and length.</p>
    </fieldset>
    <FormActions busy={busy} blocked={blocked} onCancel={onCancel} label={submitLabel || (intake ? 'Save schedule' : 'Create Draft')} />
  </form>;
}

export function SessionForm({ session, intake, onSave, onCancel, busy, blocked, error, onError }) {
  const [values, setValues] = useState(() => initialSession(session, intake));
  const delivery = initialSchedule(intake);
  const startLocal = `${values.sessionDate}T${String(values.startTime || '').slice(0, 5)}`;
  const endLocal = sessionEndLocal(values.sessionDate, values.startTime, values.durationMinutes);
  function clearField(name) {
    const nextError = clearFieldErrors(error, name);
    if (nextError !== error) onError(nextError);
  }
  const change = (event) => {
    setValues((current) => ({ ...current, [event.target.name]: event.target.type === 'checkbox' ? event.target.checked : event.target.value }));
    clearField(event.target.name);
  };
  function changeClassTime(event) {
    setValues((current) => ({ ...current, [event.target.name]: event.target.value }));
    const next = clearFieldErrors(clearFieldErrors(error, 'startsAt'), 'endsAt');
    if (next !== error) onError(next);
  }
  function submit(event) {
    event.preventDefault();
    try { onSave(sessionPayload(values, intake, session)); } catch (failure) { onError(failure); }
  }
  return <form onSubmit={submit} className="trainer-form">
    <fieldset disabled={busy || blocked}><legend>{session ? 'Edit Session' : 'Add a Session'}</legend>
      <p>One session stays on a single day in {localTimezone}. Choose the date and start time. It lasts 2 hours unless this session already has another length.</p>
      <TrainerField name="label" label="Session label" maxLength={128} value={values.label} onChange={change} required error={error} />
      <div className="trainer-form-grid">
        <WhenField name="sessionDate" label="Class date" kind="date" value={values.sessionDate} min={delivery.deliveryStartDate} max={delivery.deliveryEndDate} onChange={changeClassTime} required error={error} />
        <div className="form-group">
          <label htmlFor="trainer-startTime">Class starts</label>
          <input id="trainer-startTime" name="startTime" type="time" step="60" required value={values.startTime} onChange={changeClassTime} />
        </div>
      </div>
      <p className="trainer-help">{endLocal ? `Ends at ${endLocal.slice(11, 16)} the same day (${values.durationMinutes} minutes).` : 'This start time would finish after midnight. Choose an earlier time so the session stays on the same day.'}</p>
      <TrainerField name="meetingLink" label="Online meeting link (optional with a physical location)" type="url" maxLength={2048} placeholder="https://…" value={values.meetingLink} onChange={change} error={error} />
      <label className="trainer-checkbox"><input name="physical" type="checkbox" checked={values.physical} onChange={change} /> Include a physical location</label>
      {values.physical ? <div className="trainer-physical-fields">
        <TrainerField name="physicalAddress" label="Physical address" maxLength={512} value={values.physicalAddress} onChange={change} required error={error} />
        <div className="trainer-form-grid"><TrainerField name="physicalCapacity" label="Physical capacity" type="number" min="1" max="2147483647" step="1" value={values.physicalCapacity} onChange={change} required error={error} />
          <WhenField name="physicalBookingDeadline" label="Physical booking deadline" max={startLocal} value={values.physicalBookingDeadline} onChange={change} required error={error} /></div>
        <p className="trainer-help">Physical and online attendance share this Session's capacity. The physical booking deadline applies, and Members do not choose a mode yet.</p>
      </div> : null}
    </fieldset>
    <FormActions busy={busy} blocked={blocked} onCancel={onCancel} label={session ? 'Save Session' : 'Add Session'} />
  </form>;
}
