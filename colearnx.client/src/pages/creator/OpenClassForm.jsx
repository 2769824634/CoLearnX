import { useState } from 'react';
import { fieldMessages, localTimezone } from '../trainer/intakeForm';
import { dateInput, dayLabel, defaultOpenClass, openClassPayload, planClass, timeLabel } from './openClass';
import SessionCalendar from '../../components/SessionCalendar';

function Field({ name, label, error, children, hint, ...input }) {
  const messages = fieldMessages(error, name);
  const id = `open-class-${name}`;
  return <div className="form-group">
    <label htmlFor={id}>{label}</label>
    {children
      ? <select id={id} name={name} {...input}>{children}</select>
      : <input id={id} name={name} aria-invalid={messages.length ? true : undefined} {...input} />}
    {hint ? <small className="trainer-help">{hint}</small> : null}
    {messages.length ? <span className="trainer-field-error">{messages.join(' ')}</span> : null}
  </div>;
}

// One screen: every session of the Intake, where it meets, and how long registration stays open.
// The Trainer receives this whole schedule and must accept it before the Intake can be published.
export default function OpenClassForm({ courses, trainers = [], onCourseChange, onSave, onCancel, busy, error, onError }) {
  const [values, setValues] = useState(() => defaultOpenClass());
  const [courseId, setCourseId] = useState('');
  const [pickedTrainer, setPickedTrainer] = useState('');
  // A single Trainer needs no choice.
  const trainerId = pickedTrainer || (trainers.length === 1 ? String(trainers[0].trainerId) : '');
  const plan = planClass(values);
  const many = Number(values.sessionCount) > 1;

  const change = (event) => {
    const { name, type, checked, value } = event.target;
    setValues((current) => ({ ...current, [name]: type === 'checkbox' ? checked : value }));
    if (error) onError?.(null);
  };
  function save(sendToTrainer) {
    try { onSave(openClassPayload(values, trainerId, sendToTrainer), courseId); } catch (failure) { onError?.(failure); }
  }

  return <form className="trainer-form" onSubmit={(event) => { event.preventDefault(); save(true); }}>
    <fieldset disabled={busy}><legend>Create an intake</legend>
      <p>Enter every session of this intake. The trainer receives the whole schedule and accepts it before you publish, so the timetable stays fixed. Registration opens when you publish and closes before the first session. Times are in your local time zone ({localTimezone}).</p>
      {courses ? <Field name="courseId" label="Published course" value={courseId} required error={error}
        onChange={(event) => { setCourseId(event.target.value); setPickedTrainer(''); onCourseChange?.(event.target.value); }}>
        <option value="">Choose a published course</option>
        {courses.map((course) => <option key={course.id} value={course.id}>{course.code} — {course.title}</option>)}
      </Field> : null}
      <Field name="trainerId" label="Trainer" value={trainerId} required error={error} onChange={(event) => setPickedTrainer(event.target.value)}>
        <option value="">Choose a Trainer for this course</option>
        {trainers.map((trainer) => <option key={trainer.trainerId} value={trainer.trainerId}>{trainer.fullName}{trainer.status === 'Requested' ? ' (applied to teach)' : ''}</option>)}
      </Field>

      <div className="trainer-form-grid">
        <Field name="firstDate" label="First session date" type="date" required min={dateInput(new Date())} value={values.firstDate} onChange={change} />
        <Field name="firstTime" label="Start time" type="time" step="60" required value={values.firstTime} onChange={change} />
        <Field name="sessionCount" label="Number of sessions" type="number" min="1" max="60" step="1" required value={values.sessionCount} onChange={change} />
        <Field name="sessionHours" label="Hours per session" type="number" min="0.5" max="12" step="0.5" required value={values.sessionHours} onChange={change} />
        {many ? <Field name="everyDays" label="Repeats" value={values.everyDays} onChange={change}>
          <option value="1">Every day</option>
          <option value="7">Every week</option>
          <option value="14">Every two weeks</option>
        </Field> : null}
      </div>

      <Field name="meetingLink" label="Online meeting link" type="url" maxLength={2048} placeholder="https://…" value={values.meetingLink} onChange={change} error={error}
        hint="Used for every session. You can change a single session later." />
      <label className="trainer-checkbox"><input name="physical" type="checkbox" checked={values.physical} onChange={change} /> Include a physical location</label>
      {values.physical ? <div className="trainer-form-grid">
        <Field name="physicalAddress" label="Physical address" maxLength={512} required value={values.physicalAddress} onChange={change} error={error} />
        <Field name="physicalCapacity" label="Room capacity" type="number" min="1" step="1" required value={values.physicalCapacity} onChange={change} error={error} />
      </div> : null}

      <div className="trainer-form-grid">
        <Field name="closesDaysBefore" label="Registration closes (days before the first session)" type="number" min="0" max="60" step="1" required value={values.closesDaysBefore} onChange={change} />
        <Field name="minEnrollment" label="Minimum learners for the intake to run" type="number" min="2" max="200" step="1" required value={values.minEnrollment} onChange={change} error={error} />
      </div>

      <div className="callout" aria-label="Intake summary">
        <div className="callout-title">Summary</div>
        {plan.problem ? <p role="alert" className="trainer-field-error">{plan.problem}</p> : <>
          <p>Registration opens when you publish and closes {dayLabel(plan.closes)} {timeLabel(plan.closes)}.</p>
          <ul>{plan.sessions.map((session, index) => <li key={session.start.getTime()} data-testid="planned-session">
            Session {index + 1}: {dayLabel(session.start)} {timeLabel(session.start)}–{timeLabel(session.end)}
          </li>)}</ul>
          <SessionCalendar sessions={plan.sessions.map((session, index) => ({ id: index, label: `Session ${index + 1}`, startsAt: session.start, endsAt: session.end }))} />
          <p>The intake runs if at least {values.minEnrollment} learners reserve a place. Otherwise their credits are released.</p>
        </>}
      </div>
    </fieldset>
    <div className="trainer-actions">
      <button type="submit" className="btn btn-primary" disabled={busy || Boolean(plan.problem)}>{busy ? 'Saving…' : 'Send schedule to trainer'}</button>
      <button type="button" className="btn btn-ghost" disabled={busy || Boolean(plan.problem)} onClick={() => save(false)}>Save as draft</button>
      {onCancel ? <button type="button" className="btn btn-ghost" disabled={busy} onClick={onCancel}>Cancel</button> : null}
    </div>
  </form>;
}
