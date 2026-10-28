import { ApiError } from '../../api/client.js';
import { utcDate } from '../../utils/utcDates.js';
import { formatUtcDateTime } from '../businessPresentation.js';

export const intakeStatuses = ['Draft', 'PendingApproval', 'Published', 'InProgress', 'Completed', 'Rejected', 'Cancelled'];
export const statusLabel = (status) => ({ PendingApproval: 'Pending approval', InProgress: 'In progress' })[status] || status;
export const intakeLink = (id) => `/trainer/courses/intakes/${id}`;
export const localTimezone = Intl.DateTimeFormat().resolvedOptions().timeZone;
export const formatDate = (value) => value ? formatUtcDateTime(value) : '—';

const nonDismissibleErrorCodes = new Set([
  'UNAUTHENTICATED', 'TRAINER_REQUIRED', 'INTAKE_VERSION_CONFLICT', 'INTAKE_NOT_EDITABLE', 'INTAKE_NOT_FOUND',
]);

export function normalizeFieldKey(field) {
  return String(field || '').split('.').at(-1).replace(/\[\d+\]/g, '').toLowerCase();
}

export function clearFieldErrors(error, name) {
  if (!error || nonDismissibleErrorCodes.has(error.code) || [401, 403].includes(Number(error.status))) return error;
  const fieldEntries = Object.entries(error.fieldErrors || {});
  if (!fieldEntries.length) {
    return [400, 422].includes(Number(error.status)) || ['INVALID_FORM', 'INVALID_INPUT'].includes(error.code) ? null : error;
  }
  const target = normalizeFieldKey(name);
  const fieldErrors = Object.fromEntries(fieldEntries.filter(([field]) => normalizeFieldKey(field) !== target));
  if (Object.keys(fieldErrors).length === fieldEntries.length) return error;
  if (!Object.keys(fieldErrors).length) return null;
  const remainingMessages = Object.values(fieldErrors).flatMap((messages) => Array.isArray(messages) ? messages : [messages]).filter(Boolean);
  return {
    ...error,
    message: remainingMessages.length ? `Please correct the remaining fields: ${remainingMessages.join(' ')}` : 'Please correct the remaining fields.',
    fieldErrors,
  };
}

const pad = (part) => String(part).padStart(2, '0');

// datetime values have no zone. Minute precision only — never include seconds.
export function toLocalInput(value) {
  if (!value) return '';
  const date = value instanceof Date ? value : utcDate(value);
  if (!Number.isFinite(date.getTime())) return '';
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

// Registration is a UTC calendar date. The stored instant stays 00:00:00Z and is not shown.
export function toUtcDateInput(value) {
  if (!value) return '';
  const date = value instanceof Date ? value : utcDate(value);
  if (!Number.isFinite(date.getTime())) return '';
  return `${date.getUTCFullYear()}-${pad(date.getUTCMonth() + 1)}-${pad(date.getUTCDate())}`;
}

function invalid(field, message) {
  throw new ApiError('INVALID_FORM', message, 400, { [field]: [message] });
}

export function toUtc(value, field, original) {
  const date = new Date(value);
  if (!value || !Number.isFinite(date.getTime())) invalid(field, 'Enter a valid local date and time.');
  // Reject local times skipped by daylight-saving changes instead of silently moving them.
  if (toLocalInput(date).slice(0, 16) !== value.slice(0, 16)) invalid(field, 'This local time does not exist in your time zone.');
  // In a repeated DST hour, an unchanged field must retain its original offset.
  if (original && toLocalInput(original) === toLocalInput(date)) return utcDate(original).toISOString();
  return date.toISOString();
}

function wholeDays(opens, closes) {
  const span = utcDate(closes).getTime() - utcDate(opens).getTime();
  const days = Math.round(span / 86400000);
  return days >= 1 ? String(days) : '1';
}

function leadDays(closes, starts) {
  const span = utcDate(starts).getTime() - utcDate(closes).getTime();
  const days = Math.floor(span / 86400000);
  return days >= 0 ? String(days) : '0';
}

export function todayInput(now = new Date()) {
  return `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}`;
}

function addUtcDays(iso, days) {
  const date = utcDate(iso);
  date.setUTCDate(date.getUTCDate() + days);
  return date.toISOString();
}

function addLocalDays(dateString, days) {
  const [year, month, day] = dateString.split('-').map(Number);
  const date = new Date(year, month - 1, day);
  date.setDate(date.getDate() + days);
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
}

function localDate(value) {
  return toLocalInput(value).slice(0, 10);
}

function inclusiveDeliveryEnd(endsAt, startDate) {
  const endLocal = toLocalInput(endsAt);
  let endDate = endLocal.slice(0, 10);
  if (endLocal.slice(11, 16) === '00:00' && endDate) {
    const previous = addLocalDays(endDate, -1);
    if (previous >= startDate) endDate = previous;
  }
  return endDate;
}

export const initialSchedule = (intake = {}) => {
  const openLocal = toLocalInput(intake.registrationOpensAt);
  const startDate = localDate(intake.startsAt);
  return {
    registrationStartDate: openLocal.slice(0, 10),
    registrationStartTime: openLocal.slice(11, 16),
    registrationDays: intake.registrationOpensAt && intake.registrationClosesAt ? wholeDays(intake.registrationOpensAt, intake.registrationClosesAt) : '',
    daysBeforeTeaching: intake.registrationClosesAt && intake.startsAt ? leadDays(intake.registrationClosesAt, intake.startsAt) : '',
    deliveryStartDate: startDate,
    deliveryEndDate: intake.endsAt ? inclusiveDeliveryEnd(intake.endsAt, startDate) : '',
    minEnrollment: intake.minEnrollment ?? 10,
  };
};

export function schedulePayload(values, version, original, trainerId, now = new Date()) {
  const shown = original ? initialSchedule(original) : null;
  const same = (key) => shown && values[key] === shown[key];
  let registrationOpensAt;
  let registrationClosesAt;
  if (same('registrationStartDate') && same('registrationStartTime') && same('registrationDays')) {
    registrationOpensAt = utcDate(original.registrationOpensAt).toISOString();
    registrationClosesAt = utcDate(original.registrationClosesAt).toISOString();
  } else {
    const time = String(values.registrationStartTime || '').slice(0, 5);
    const days = Number(values.registrationDays);
    if (!Number.isInteger(days) || days < 1) invalid('registrationClosesAt', 'Enter how many days registration stays open.');
    registrationOpensAt = same('registrationStartDate') && same('registrationStartTime') && original?.registrationOpensAt
      ? utcDate(original.registrationOpensAt).toISOString()
      : toUtc(`${values.registrationStartDate}T${time}`, 'registrationOpensAt');
    registrationClosesAt = addUtcDays(registrationOpensAt, days);
  }
  let startsAt;
  let endsAt;
  if (same('deliveryStartDate') && same('deliveryEndDate')) {
    startsAt = utcDate(original.startsAt).toISOString();
    endsAt = utcDate(original.endsAt).toISOString();
  } else {
    if (!/^\d{4}-\d{2}-\d{2}$/.test(values.deliveryStartDate || '')) invalid('startsAt', 'Choose the first teaching date.');
    if (!/^\d{4}-\d{2}-\d{2}$/.test(values.deliveryEndDate || '')) invalid('endsAt', 'Choose the last teaching date.');
    if (values.deliveryEndDate < values.deliveryStartDate) invalid('endsAt', 'Teaching must end on or after the day it starts.');
    startsAt = toUtc(`${values.deliveryStartDate}T00:00`, 'startsAt');
    endsAt = toUtc(`${addLocalDays(values.deliveryEndDate, 1)}T00:00`, 'endsAt');
  }
  const lead = Number(values.daysBeforeTeaching);
  if (values.daysBeforeTeaching === '' || values.daysBeforeTeaching == null || !Number.isInteger(lead) || lead < 0) {
    invalid('daysBeforeTeaching', 'Enter how many days must pass after registration closes before teaching starts.');
  }
  const currentMinute = new Date(now);
  currentMinute.setSeconds(0, 0);
  if (utcDate(registrationOpensAt).getTime() < currentMinute.getTime()) {
    invalid('registrationOpensAt', 'Registration cannot start before the current time.');
  }
  if (utcDate(startsAt).getTime() < utcDate(registrationClosesAt).getTime() + lead * 86400000) {
    invalid('daysBeforeTeaching', lead === 0
      ? 'Teaching must start on or after registration closes.'
      : `Teaching must start at least ${lead} days after registration closes.`);
  }
  const payload = { registrationOpensAt, registrationClosesAt, startsAt, endsAt, minEnrollment: Number(values.minEnrollment), daysBeforeTeaching: lead };
  if (version) return { ...payload, version };
  return trainerId ? { ...payload, trainerId: Number(trainerId) } : payload;
}

export const DEFAULT_SESSION_MINUTES = 120;

function clockMinutes(value) {
  const [hour, minute] = String(value || '').split(':').map(Number);
  return hour * 60 + minute;
}

export function sessionEndLocal(sessionDate, startTime, durationMinutes = DEFAULT_SESSION_MINUTES) {
  const time = String(startTime || '').slice(0, 5);
  if (!/^\d{4}-\d{2}-\d{2}$/.test(sessionDate || '') || !/^\d{2}:\d{2}$/.test(time)) return '';
  const length = Number(durationMinutes);
  const end = clockMinutes(time) + length;
  if (!Number.isInteger(length) || length < 1 || end >= 24 * 60) return '';
  return `${sessionDate}T${pad(Math.floor(end / 60))}:${pad(end % 60)}`;
}

export function initialSession(session, intake) {
  const startLocal = toLocalInput(session?.startsAt || intake?.startsAt);
  const sessionDate = startLocal.slice(0, 10);
  let startTime = startLocal.slice(11, 16);
  let durationMinutes = DEFAULT_SESSION_MINUTES;
  if (session?.startsAt && session?.endsAt) {
    const endLocal = toLocalInput(session.endsAt);
    const length = clockMinutes(endLocal.slice(11, 16)) - clockMinutes(startTime);
    if (endLocal.slice(0, 10) === sessionDate && length > 0) durationMinutes = length;
  }
  if (!sessionEndLocal(sessionDate, startTime, durationMinutes)) startTime = '09:00';
  return {
    label: session?.label || '', sessionDate, startTime, durationMinutes, meetingLink: session?.meetingLink || '',
    physical: Boolean(session?.physicalAddress), physicalAddress: session?.physicalAddress || '',
    physicalCapacity: session?.physicalCapacity || 1, physicalBookingDeadline: toLocalInput(session?.physicalBookingDeadline),
  };
}

export function sessionPayload(values, intake, original) {
  const label = values.label.trim();
  const startTime = String(values.startTime || '').slice(0, 5);
  const startLocal = `${values.sessionDate}T${startTime}`;
  const endLocal = sessionEndLocal(values.sessionDate, startTime, values.durationMinutes);
  if (!endLocal) invalid('startsAt', 'A session must start and end on the same day. The default length is 2 hours.');
  const startsAt = toUtc(startLocal, 'startsAt', original?.startsAt);
  const endsAt = toUtc(endLocal, 'endsAt', original?.endsAt);
  const meetingLink = values.meetingLink.trim() || null;
  const physicalAddress = values.physical ? values.physicalAddress.trim() : null;
  const physicalCapacity = values.physical ? Number(values.physicalCapacity) : 0;
  const physicalBookingDeadline = values.physical ? toUtc(values.physicalBookingDeadline, 'physicalBookingDeadline', original?.physicalBookingDeadline) : null;
  return { label, startsAt, endsAt, meetingLink, physicalAddress, physicalCapacity, physicalBookingDeadline, version: intake.version };
}

export function fieldMessages(error, name) {
  const target = normalizeFieldKey(name);
  return Object.entries(error?.fieldErrors || {}).filter(([key]) => normalizeFieldKey(key) === target).flatMap(([, messages]) => messages);
}

export function safeMeetingLink(value) {
  try { return ['http:', 'https:'].includes(new URL(value).protocol) ? value : null; } catch { return null; }
}
