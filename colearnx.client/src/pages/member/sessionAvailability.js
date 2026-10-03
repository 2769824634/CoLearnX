import { utcDate } from '../../utils/utcDates';

// Use the same result for the status label, initial selection and reservation controls.
export function sessionAvailability(session, now) {
  if (!session) return { label: 'No available sessions', canReserve: false };
  let label;
  if (session.cancelledAt || session.intakeStatus === 'Cancelled') label = 'Class cancelled';
  else if (session.intakeStatus === 'InProgress') label = 'Class in progress';
  else if (session.intakeStatus === 'Completed') label = 'Class completed';
  else if (session.intakeStatus && session.intakeStatus !== 'Published') label = 'Registration unavailable';
  else if (session.confirmedToRunAt) label = 'Class confirmed';
  else if (!session.startsAt || !session.endsAt
    || !Number.isFinite(utcDate(session.startsAt).getTime()) || !Number.isFinite(utcDate(session.endsAt).getTime())) label = 'Schedule unavailable';
  else if (now >= utcDate(session.endsAt).getTime()) label = 'Class completed';
  else if (now >= utcDate(session.startsAt).getTime()) label = 'Class in progress';
  else if (session.registrationOpensAt && now < utcDate(session.registrationOpensAt).getTime()) label = 'Registration not open';
  else if (session.registrationClosesAt && now >= utcDate(session.registrationClosesAt).getTime()) label = 'Registration closed';
  else if (Number(session.capacity) > 0 && session.physicalBookingDeadline
    && now > utcDate(session.physicalBookingDeadline).getTime()) label = 'Physical booking closed';
  else if (Number(session.capacity) > 0 && Number(session.seats) <= 0) label = 'Session full';
  return { label: label || 'Registration open', canReserve: !label };
}
