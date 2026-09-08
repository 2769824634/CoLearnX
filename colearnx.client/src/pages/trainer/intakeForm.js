import { ApiError } from '../../api/client.js';

export const intakeStatuses = ['Draft', 'PendingApproval', 'Published', 'InProgress', 'Completed', 'Rejected', 'Cancelled'];
export const statusLabel = (status) => ({ PendingApproval: 'Pending approval', InProgress: 'In progress' })[status] || status;
export const isEditable = (status) => status === 'Draft' || status === 'Rejected';
export const intakeLink = (id) => `/trainer/courses/intakes/${id}`;
export const localTimezone = Intl.DateTimeFormat().resolvedOptions().timeZone;
export const formatDate = (value) => value ? new Date(value).toLocaleString('en-GB', { dateStyle: 'medium', timeStyle: 'short' }) : '—';

// datetime-local has no zone. Use local date parts, never slice a UTC ISO string.
export function toLocalInput(value) {
  if (!value) return '';
  const date = new Date(value);
  if (!Number.isFinite(date.getTime())) return '';
  const pad = (part, size = 2) => String(part).padStart(size, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}:${pad(date.getSeconds())}.${pad(date.getMilliseconds(), 3)}`;
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
  if (original && toLocalInput(original) === toLocalInput(date)) return new Date(original).toISOString();
  return date.toISOString();
}

const scheduleFields = ['registrationOpensAt', 'registrationClosesAt', 'startsAt', 'endsAt'];
export const initialSchedule = (intake = {}) => Object.fromEntries(scheduleFields.map((field) => [field, toLocalInput(intake[field])]));

export function schedulePayload(values, version, original) {
  const payload = Object.fromEntries(scheduleFields.map((field) => [field, toUtc(values[field], field, original?.[field])]));
  if (!(payload.registrationOpensAt < payload.registrationClosesAt && payload.registrationClosesAt <= payload.startsAt && payload.startsAt < payload.endsAt)) {
    invalid('registrationClosesAt', 'Registration must open before it closes; delivery must start at or after closing and end after it starts.');
  }
  return version ? { ...payload, version } : payload;
}

export function initialSession(session, intake) {
  return {
    label: session?.label || '', startsAt: toLocalInput(session?.startsAt || intake.startsAt),
    endsAt: toLocalInput(session?.endsAt || intake.endsAt), meetingLink: session?.meetingLink || '',
    physical: Boolean(session?.physicalAddress), physicalAddress: session?.physicalAddress || '',
    physicalCapacity: session?.physicalCapacity || 1, physicalBookingDeadline: toLocalInput(session?.physicalBookingDeadline),
  };
}

export function sessionPayload(values, intake, original) {
  const label = values.label.trim();
  if (!label || label.length > 128) invalid('label', 'Enter a session label of up to 128 characters.');
  const startsAt = toUtc(values.startsAt, 'startsAt', original?.startsAt);
  const endsAt = toUtc(values.endsAt, 'endsAt', original?.endsAt);
  if (!(startsAt < endsAt && new Date(startsAt) >= new Date(intake.startsAt) && new Date(endsAt) <= new Date(intake.endsAt))) {
    invalid('startsAt', 'The session must start before it ends and fit within the Intake delivery period.');
  }
  const meetingLink = values.meetingLink.trim() || null;
  if (meetingLink) {
    let url;
    try { url = new URL(meetingLink); } catch { invalid('meetingLink', 'Enter an absolute HTTP or HTTPS meeting link.'); }
    if (!['https:', 'http:'].includes(url.protocol) || meetingLink.length > 2048) invalid('meetingLink', 'Enter an absolute HTTP or HTTPS meeting link, up to 2048 characters.');
  }
  const physicalAddress = values.physical ? values.physicalAddress.trim() : null;
  const physicalCapacity = values.physical ? Number(values.physicalCapacity) : 0;
  const physicalBookingDeadline = values.physical ? toUtc(values.physicalBookingDeadline, 'physicalBookingDeadline', original?.physicalBookingDeadline) : null;
  if (values.physical) {
    if (!physicalAddress || physicalAddress.length > 512) invalid('physicalAddress', 'Enter a physical address of up to 512 characters.');
    if (!Number.isInteger(physicalCapacity) || physicalCapacity < 1 || physicalCapacity > 2147483647) invalid('physicalCapacity', 'Enter a positive whole-number capacity.');
    if (physicalBookingDeadline > startsAt) invalid('physicalBookingDeadline', 'The physical booking deadline must be at or before the session start.');
  } else if (!meetingLink) invalid('meetingLink', 'Provide an online meeting link or enable a physical location.');
  return { label, startsAt, endsAt, meetingLink, physicalAddress, physicalCapacity, physicalBookingDeadline, version: intake.version };
}

export function fieldMessages(error, name) {
  return Object.entries(error?.fieldErrors || {}).filter(([key]) => key.split('.').at(-1).toLowerCase() === name.toLowerCase()).flatMap(([, messages]) => messages);
}

export function safeMeetingLink(value) {
  try { return ['http:', 'https:'].includes(new URL(value).protocol) ? value : null; } catch { return null; }
}
