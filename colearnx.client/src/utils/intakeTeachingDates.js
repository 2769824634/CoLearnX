import { ApiError } from '../api/client';
import { utcDate } from './utcDates';
import { initialSchedule, toUtc } from '../pages/trainer/intakeForm';

export function canEditTeachingDates(status) {
  return ['Draft', 'Rejected', 'Published', 'InProgress'].includes(status);
}

function dateError(field, message) {
  return new ApiError('INVALID_INTAKE_DATES', message, 400, { [field]: [message] });
}

export function teachingDatesPayload(values, intake) {
  const original = initialSchedule(intake);
  if (!values.deliveryStartDate) throw dateError('startsAt', 'Choose the first teaching date.');
  if (!values.deliveryEndDate || values.deliveryEndDate < values.deliveryStartDate)
    throw dateError('endsAt', 'The last teaching date must be on or after the first teaching date.');
  const startsAt = values.deliveryStartDate === original.deliveryStartDate ? intake.startsAt
    : toUtc(`${values.deliveryStartDate}T00:00`, 'startsAt');
  let endsAt = intake.endsAt;
  if (values.deliveryEndDate !== original.deliveryEndDate) {
    // The last teaching date is inclusive. Use the next local midnight, including across DST.
    const nextDay = new Date(`${values.deliveryEndDate}T12:00`);
    nextDay.setDate(nextDay.getDate() + 1);
    const nextDate = `${nextDay.getFullYear()}-${String(nextDay.getMonth() + 1).padStart(2, '0')}-${String(nextDay.getDate()).padStart(2, '0')}`;
    endsAt = toUtc(`${nextDate}T00:00`, 'endsAt');
  }
  // Normalize SQLite timestamps too, while preserving unchanged instants and their precision.
  const start = utcDate(startsAt);
  const end = utcDate(endsAt);
  if (start >= end) throw dateError('endsAt', 'Teaching must end after it starts.');
  if (start < utcDate(intake.registrationClosesAt))
    throw dateError('startsAt', 'Teaching must start on or after registration closes.');
  if ((intake.sessions || []).some((session) => utcDate(session.startsAt) < start || utcDate(session.endsAt) > end))
    throw dateError('endsAt', 'The teaching dates must include every existing session.');
  return { startsAt: start.toISOString(), endsAt: end.toISOString(), version: intake.version };
}
