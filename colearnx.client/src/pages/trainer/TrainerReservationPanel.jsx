import { useState } from 'react';
import { trainerLaterPhaseApi } from '../../api/trainerLaterPhase';
import { formatCount, formatUtcDateTime } from '../businessPresentation';
import { TrainerError, TrainerLoading } from './TrainerUi';
import useTrainerQuery from './useTrainerQuery';

const loadReservations = (token, id, signal) => trainerLaterPhaseApi.reservations(token, id, signal);

function ReservationList({ intakeId }) {
  const query = useTrainerQuery(loadReservations, intakeId);
  if (query.loading) return <TrainerLoading />;
  if (query.error) return <TrainerError error={query.error} onRetry={query.refresh} retryLabel="Retry loading" />;
  if (!query.data.length) return <p className="trainer-help">No reserved places remain in this Intake.</p>;
  return <div className="later-table-wrap"><table className="later-table">
    <caption>Reserved places (read-only)</caption>
    <thead><tr><th>Learner</th><th>Reservation</th><th>Session</th><th>Credits</th><th>Reserved at (UTC)</th></tr></thead>
    <tbody>{query.data.map((item) => <tr key={item.enrollmentId}>
      <td>{item.learnerName}</td><td>ENR-{item.enrollmentId}</td>
      <td>{item.sessionLabel} · Session #{item.courseSessionId}</td>
      <td>{formatCount(item.creditsHeld, 'credit')} on hold</td><td>{formatUtcDateTime(item.reservedAt)}</td>
    </tr>)}</tbody>
  </table></div>;
}

export default function TrainerReservationPanel({ intake }) {
  const [open, setOpen] = useState(false);
  const reserved = intake.reservedEnrollmentCount;
  const minimum = intake.minEnrollment ?? 10;
  const remaining = intake.remainingToMinimum;
  return <section className="later-panel" aria-label="Reservation progress">
    <h2>Reservation progress</h2>
    <p><span>{reserved == null ? 'Reservation count unavailable' : `${reserved} reserved`}</span> · <span>{formatCount(minimum, 'learner')} minimum</span> · <span>{intake.activeEnrollmentCount ?? '—'} active</span></p>
    <p>{intake.confirmedToRunAt ? 'Class confirmed to run' : remaining == null ? 'Refresh the Intake to load the current count.' : remaining > 0 ? `${formatCount(remaining, 'more learner', 'more learners')} needed` : 'Minimum reached; confirmation occurs at the registration deadline.'}</p>
    <p className="trainer-help">Reserved places hold credits. Attendance, assessment and completion become available after confirmation.</p>
    <button type="button" className="btn btn-ghost" aria-expanded={open} onClick={() => setOpen(!open)}>{open ? 'Hide reserved places' : 'View reserved places'}</button>
    {open ? <ReservationList intakeId={intake.id} /> : null}
  </section>;
}
