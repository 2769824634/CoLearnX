import { useState } from 'react';
import { fieldMessages, localTimezone } from '../trainer/intakeForm';
import { dateInput, editSchedulePayload, initialEditSchedule, localDateTime } from './openClass';

// Teaching dates follow the sessions, so the schedule only asks when registration closes and how many learners are needed.
export default function EditClassScheduleForm({ intake, onSave, onCancel, busy, error, onError }) {
  const [values, setValues] = useState(() => initialEditSchedule(intake));
  const change = (event) => {
    setValues((current) => ({ ...current, [event.target.name]: event.target.value }));
    if (error) onError?.(null);
  };
  function submit(event) {
    event.preventDefault();
    try { onSave(editSchedulePayload(intake, values)); } catch (failure) { onError?.(failure); }
  }
  const closeErrors = fieldMessages(error, 'registrationClosesAt');
  const minimumErrors = fieldMessages(error, 'minEnrollment');
  return <form className="trainer-form" onSubmit={submit}>
    <fieldset disabled={busy}><legend>Edit schedule</legend>
      <p>Registration opened {localDateTime(intake.registrationOpensAt)}. Teaching dates follow the sessions below: add, move or delete a session to change them. Times are in your local time zone ({localTimezone}).</p>
      <div className="trainer-form-grid">
        <div className="form-group">
          <label htmlFor="edit-class-closesDate">Registration closes on</label>
          <input id="edit-class-closesDate" name="closesDate" type="date" required min={dateInput(new Date())} value={values.closesDate} onChange={change} aria-invalid={closeErrors.length ? true : undefined} />
        </div>
        <div className="form-group">
          <label htmlFor="edit-class-closesTime">Closing time</label>
          <input id="edit-class-closesTime" name="closesTime" type="time" step="60" required value={values.closesTime} onChange={change} />
        </div>
      </div>
      {closeErrors.length ? <span className="trainer-field-error">{closeErrors.join(' ')}</span> : null}
      <div className="form-group">
        <label htmlFor="edit-class-minEnrollment">Minimum learners for the intake to run</label>
        <input id="edit-class-minEnrollment" name="minEnrollment" type="number" min="2" max="200" step="1" required value={values.minEnrollment} onChange={change} aria-invalid={minimumErrors.length ? true : undefined} />
        {minimumErrors.length ? <span className="trainer-field-error">{minimumErrors.join(' ')}</span> : null}
      </div>
    </fieldset>
    <div className="trainer-actions">
      <button type="submit" className="btn btn-primary" disabled={busy}>{busy ? 'Saving…' : 'Save schedule'}</button>
      {onCancel ? <button type="button" className="btn btn-ghost" disabled={busy} onClick={onCancel}>Cancel</button> : null}
    </div>
  </form>;
}
