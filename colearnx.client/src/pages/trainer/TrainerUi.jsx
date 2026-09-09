import { useEffect, useRef } from 'react';
import { Link } from 'react-router-dom';
import { fieldMessages, formatDate, intakeLink, statusLabel } from './intakeForm';
import '../../styles/trainer.css';

export function TrainerHeader({ eyebrow = 'Trainer workspace', title, children, action }) {
  useEffect(() => {
    const previous = document.title;
    document.title = 'CoLearnX — Trainer';
    return () => { document.title = previous; };
  }, []);
  return <header className="trainer-header"><div><p className="trainer-eyebrow">{eyebrow}</p><h1>{title}</h1><p>{children}</p></div>{action}</header>;
}

export function IntakeStatus({ status }) {
  return <span className={`trainer-status ${status.toLowerCase()}`}>{statusLabel(status)}</span>;
}

export function TrainerError({ error, onRetry, retryLabel = 'Try again' }) {
  const ref = useRef(null);
  useEffect(() => { if (error) ref.current?.focus(); }, [error]);
  if (!error) return null;
  return <div className="trainer-error" role="alert" tabIndex={-1} ref={ref}>
    <strong>{error.message || 'The request could not reach the server. Please try again.'}</strong>
    <code>{error.code || 'NETWORK_ERROR'}</code>
    {Object.keys(error.fieldErrors || {}).length ? <ul>{Object.entries(error.fieldErrors).map(([field, messages]) => <li key={field}>{field}: {messages.join(' ')}</li>)}</ul> : null}
    {error.status === 401 ? <p>Your session is no longer valid. Log out and sign in again.</p> : null}
    {error.status === 403 ? <p>An enabled Trainer role is required. Check your account permissions or sign in again.</p> : null}
    {onRetry ? <button type="button" className="btn btn-ghost" onClick={onRetry}>{retryLabel}</button> : null}
  </div>;
}

export function TrainerLoading() {
  return <div className="trainer-empty" role="status">Loading your workspace…</div>;
}

export function TrainerField({ name, label, error, children, ...inputProps }) {
  const messages = fieldMessages(error, name);
  const props = { id: `trainer-${name}`, name, 'aria-invalid': messages.length ? true : undefined, 'aria-describedby': messages.length ? `trainer-${name}-error` : undefined, ...inputProps };
  return <div className="form-group"><label htmlFor={props.id}>{label}</label>{children ? <select {...props}>{children}</select> : <input {...props} />}
    {messages.length ? <span className="trainer-field-error" id={`trainer-${name}-error`}>{messages.join(' ')}</span> : null}
  </div>;
}

export function IntakeRows({ intakes, courses, empty = 'No Intakes yet. Create one from a published course to get started.' }) {
  const courseMap = new Map(courses.map((course) => [course.id, course]));
  if (!intakes.length) return <div className="trainer-empty">{empty}</div>;
  return <div className="trainer-intake-list">{intakes.map((intake) => {
    const course = courseMap.get(intake.courseId);
    return <article className="trainer-intake-row" key={intake.id}>
      <div className="trainer-date-tile" aria-hidden="true"><strong>{new Date(intake.startsAt).getDate()}</strong><span>{new Date(intake.startsAt).toLocaleDateString('en-GB', { month: 'short', year: 'numeric' })}</span></div>
      <div className="trainer-intake-title"><small>{course?.code || `Course #${intake.courseId}`} · Intake #{intake.id}</small><h3><Link to={intakeLink(intake.id)}>{course?.title || `Course #${intake.courseId}`}</Link></h3><p>{formatDate(intake.startsAt)} → {formatDate(intake.endsAt)}</p></div>
      <IntakeStatus status={intake.status} /><Link className="trainer-open" to={intakeLink(intake.id)} aria-label={`Open Intake ${intake.id}`}>Open →</Link>
    </article>;
  })}</div>;
}
