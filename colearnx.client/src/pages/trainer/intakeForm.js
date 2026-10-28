import { ApiError } from '../../api/client.js';
import { utcDate } from '../../utils/utcDates.js';
import { formatUtcDateTime } from '../businessPresentation.js';

export const intakeStatuses = ['Draft', 'PendingApproval', 'Published', 'InProgress', 'Completed', 'Rejected', 'Cancelled'];
export const statusLabel = (status) => ({ PendingApproval: 'Pending approval', InProgress: 'In progress' })[status] || status;
export const isEditable = (status) => status === 'Draft' || status === 'Rejected';
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

function registrationInstant(value, field, original) {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(value || '')) invalid(field, 'Enter a valid date.');
  if (original && toUtcDateInput(original) === value) return utcDate(original).toISOString();
  const date = new Date(`${value}T00:00:00.000Z`);
  if (date.toISOString().slice(0, 10) !== value) invalid(field, 'Enter a valid date.');
  return date.toISOString();
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

const registrationFields = ['registrationOpensAt', 'registrationClosesAt'];
const deliveryFields = ['startsAt', 'endsAt'];
export const initialSchedule = (intake = {}) => ({
  ...Object.fromEntries(registrationFields.map((field) => [field, toUtcDateInput(intake[field])])),
  ...Object.fromEntries(deliveryFields.map((field) => [field, toLocalInput(intake[field])])),
  minEnrollment: intake.minEnrollment ?? 10,
});

export function schedulePayload(values, version, original, trainerId) {
  const payload = {
    ...Object.fromEntries(registrationFields.map((field) => [field, registrationInstant(values[field], field, original?.[field])])),
    ...Object.fromEntries(deliveryFields.map((field) => [field, toUtc(values[field], field, original?.[field])])),
  };
  payload.minEnrollment = Number(values.minEnrollment);
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
