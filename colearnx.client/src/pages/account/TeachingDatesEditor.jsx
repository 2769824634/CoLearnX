import { useId, useState } from 'react';
import { initialSchedule, localTimezone } from '../trainer/intakeForm';
import { utcDate } from '../../utils/utcDates';

function datePayload(values, intake) {
  const original = initialSchedule(intake);
  if (!values.deliveryStartDate || !values.deliveryEndDate || values.deliveryEndDate < values.deliveryStartDate) {
    throw new Error('The last teaching date must be on or after the first teaching date.');
  }
  const last = new Date(`${values.deliveryEndDate}T00:00`);
  last.setDate(last.getDate() + 1);
  return {
    startsAt: values.deliveryStartDate === original.deliveryStartDate ? intake.startsAt : new Date(`${values.deliveryStartDate}T00:00`).toISOString(),
    endsAt: values.deliveryEndDate === original.deliveryEndDate ? intake.endsAt : last.toISOString(),
    version: intake.version,
  };
}

export default function TeachingDatesEditor({ intake, save, reload, onSaved }) {
  const id = useId();
  const [editing, setEditing] = useState(false);
  const [values, setValues] = useState(() => initialSchedule(intake));
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);
  const [notice, setNotice] = useState('');
  const [checkedAt, setCheckedAt] = useState(() => Date.now());
  const closed = ['Completed', 'Cancelled', 'PendingApproval'].includes(intake.status);
  const started = utcDate(intake.startsAt).getTime() <= checkedAt;
  const shown = initialSchedule(intake);

  async function submit(event) {
    event.preventDefault();
    setBusy(true); setError(null); setNotice('');
    try {
      const updated = await save(datePayload(values, intake));
      onSaved?.(updated);
      setEditing(false);
      setNotice('Teaching dates saved.');
    } catch (failure) { setError(failure); }
    finally { setBusy(false); }
  }

  async function refresh() {
    setBusy(true);
    try {
      const updated = await reload();
      onSaved?.(updated);
      setValues(initialSchedule(updated));
      setError(null); setEditing(false);
    } catch (failure) { setError(failure); }
    finally { setBusy(false); }
  }

  return <section aria-label={`Teaching dates for class ${intake.id}`}>
    <div className="creator-section-heading">
      <div><h2>Teaching dates</h2><p>{shown.deliveryStartDate} → {shown.deliveryEndDate}</p></div>
      {!closed && !editing ? <button type="button" className="btn btn-ghost btn-sm" onClick={() => {
        setValues(initialSchedule(intake)); setCheckedAt(Date.now()); setEditing(true); setError(null); setNotice('');
      }}>Edit teaching dates</button> : null}
    </div>
    {notice ? <p role="status">{notice}</p> : null}
    {editing ? <form className="trainer-form" onSubmit={submit}>
      <fieldset disabled={busy}>
        <legend>Edit teaching dates</legend>
        <p>Dates use {localTimezone}. The last teaching date includes that whole day. Existing session times and registration dates stay the same.</p>
        <div className="trainer-form-grid">
          <div className="form-group"><label htmlFor={`${id}-start`}>First teaching date</label>
            <input id={`${id}-start`} type="date" required disabled={started} value={values.deliveryStartDate} onChange={(event) => setValues((current) => ({ ...current, deliveryStartDate: event.target.value }))} /></div>
          <div className="form-group"><label htmlFor={`${id}-end`}>Last teaching date</label>
            <input id={`${id}-end`} type="date" required min={values.deliveryStartDate} value={values.deliveryEndDate} onChange={(event) => setValues((current) => ({ ...current, deliveryEndDate: event.target.value }))} /></div>
        </div>
        <p className="trainer-help">The period must include every scheduled session and start on or after registration closes.{started ? ' Teaching has started, so only the last date can change.' : ''}</p>
      </fieldset>
      {error ? <div className="callout warn" role="alert">{error.message}</div> : null}
      <div className="trainer-actions">
        <button type="submit" className="btn btn-primary" disabled={busy || error?.code === 'INTAKE_VERSION_CONFLICT'}>{busy ? 'Saving…' : 'Save dates'}</button>
        <button type="button" className="btn btn-ghost" disabled={busy} onClick={() => { setEditing(false); setError(null); }}>Cancel</button>
        {error?.code === 'INTAKE_VERSION_CONFLICT' && reload ? <button type="button" className="btn btn-ghost" disabled={busy} onClick={refresh}>Reload class</button> : null}
      </div>
    </form> : null}
    {closed ? <p className="trainer-help">Dates are locked for completed, cancelled and pending classes.</p> : null}
  </section>;
}
