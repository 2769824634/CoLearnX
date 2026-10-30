import { useState } from 'react';
import Modal from './Modal';
import { canEditTeachingDates, teachingDatesPayload } from '../utils/intakeTeachingDates';
import { fieldMessages, initialSchedule, localTimezone } from '../pages/trainer/intakeForm';

export default function IntakeTeachingDatesEditor({ intake, onSave, onClose, onReload }) {
  const [values, setValues] = useState(() => initialSchedule(intake));
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);
  const editable = canEditTeachingDates(intake.status);

  async function submit(event) {
    event.preventDefault();
    if (busy || !editable || error?.code === 'INTAKE_VERSION_CONFLICT') return;
    setError(null);
    try {
      const body = teachingDatesPayload(values, intake);
      setBusy(true);
      await onSave(body);
      onClose(true);
    } catch (failure) { setError(failure); }
    finally { setBusy(false); }
  }

  async function reload() {
    setBusy(true);
    try { await onReload(); }
    catch (failure) { setError(failure); }
    finally { setBusy(false); }
  }

  function change(key, value) {
    setValues((current) => ({ ...current, [key]: value }));
    if (error?.code !== 'INTAKE_VERSION_CONFLICT') setError(null);
  }

  return <Modal open title={`Edit teaching dates · Class #${intake.id}`} onClose={() => !busy && onClose(false)} width={560}>
    <form onSubmit={submit}>
      <p>Choose the first and last teaching dates in {localTimezone}. The last date includes the whole day.</p>
      <p>Existing sessions must fit within these dates.</p>
      {!editable ? <p role="alert">This class no longer allows date changes.</p> : null}
      <div className="grid-2">
        {[
          { key: 'deliveryStartDate', field: 'startsAt', label: 'First teaching date' },
          { key: 'deliveryEndDate', field: 'endsAt', label: 'Last teaching date' },
        ].map(({ key, field, label }) => {
          const messages = fieldMessages(error, field);
          return <div className="form-group" key={key}>
            <label htmlFor={`teaching-${field}`}>{label}</label>
            <input id={`teaching-${field}`} type="date" value={values[key]} required disabled={busy || !editable}
              aria-invalid={messages.length ? true : undefined} aria-describedby={messages.length ? `teaching-${field}-error` : undefined}
              onChange={(event) => change(key, event.target.value)} />
            {messages.length ? <span className="trainer-field-error" id={`teaching-${field}-error`}>{messages.join(' ')}</span> : null}
          </div>;
        })}
      </div>
      {error ? <div className="callout warn" role="alert">{error.message}
        {error.code === 'INTAKE_VERSION_CONFLICT' ? <button type="button" className="btn btn-ghost btn-sm" onClick={reload} disabled={busy}>Reload class</button> : null}
      </div> : null}
      <div className="modal-actions">
        <button type="button" className="btn btn-ghost" disabled={busy} onClick={() => onClose(false)}>Cancel</button>
        <button type="submit" className="btn btn-primary" disabled={busy || !editable || error?.code === 'INTAKE_VERSION_CONFLICT'}>{busy ? 'Saving…' : 'Save dates'}</button>
      </div>
    </form>
  </Modal>;
}
