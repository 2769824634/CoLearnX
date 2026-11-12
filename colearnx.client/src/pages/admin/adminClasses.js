import { utcDate } from '../../utils/utcDates.js';
import { initialSchedule } from '../trainer/intakeForm.js';

export const LOW_ATTENDANCE = 0.7;
const DAY_MS = 24 * 60 * 60 * 1000;

const STATUS_LABELS = {
  Draft: 'Draft',
  PendingApproval: 'Pending approval',
  Published: 'Open for registration',
  InProgress: 'In progress',
  Completed: 'Finished',
  Rejected: 'Rejected',
  Cancelled: 'Cancelled',
};

const STATUS_PILLS = {
  Published: 'info',
  InProgress: 'success',
  PendingApproval: 'warn',
  Cancelled: 'danger',
  Rejected: 'danger',
};

export const CLASS_STATUS_FILTERS = [
  { value: '', label: 'All statuses' },
  { value: 'Published', label: 'Open for registration' },
  { value: 'InProgress', label: 'In progress' },
  { value: 'Completed', label: 'Finished' },
  { value: 'Cancelled', label: 'Cancelled' },
];

export function classStatusLabel(status) {
  return STATUS_LABELS[status] || status;
}

export function classStatusPill(status) {
  return `pill ${STATUS_PILLS[status] || 'neutral'}`;
}

export function attendanceText(rate) {
  return rate == null ? '—' : `${Math.round(rate * 100)}%`;
}

export function isLowAttendance(rate) {
  return rate != null && rate < LOW_ATTENDANCE;
}

function dayCount(ms) {
  const days = Math.ceil(ms / DAY_MS);
  return `${days} ${days === 1 ? 'day' : 'days'}`;
}

// Plain-language time left for a class, counted in whole days.
export function timeLeftText(row, now = new Date()) {
  if (row.status === 'Cancelled' || row.status === 'Rejected') return '—';
  const start = utcDate(row.startsAt).getTime();
  const end = utcDate(row.endsAt).getTime();
  const current = now.getTime();
  if (current < start) return `Starts in ${dayCount(start - current)}`;
  if (current <= end) return `${dayCount(end - current)} left`;
  return 'Ended';
}

// Same first and last teaching day the Trainer and Creator see (local dates, last day inclusive).
export function classDates(row) {
  if (!row?.startsAt) return { start: '—', end: '—' };
  const schedule = initialSchedule(row);
  return { start: schedule.deliveryStartDate || '—', end: schedule.deliveryEndDate || '—' };
}

// Classes that are still running come first, the one ending soonest at the top.
export function sortEndingSoon(rows, now = new Date()) {
  const current = now.getTime();
  const rank = (row) => {
    const end = utcDate(row.endsAt).getTime();
    return row.status === 'Cancelled' || end < current ? Number.MAX_SAFE_INTEGER : end;
  };
  return [...rows].sort((a, b) => rank(a) - rank(b));
}

export function filterClasses(rows, { status = '', trainerId = '', creatorId = '' }) {
  return rows.filter((row) => (!status || row.status === status)
    && (!trainerId || String(row.trainerId) === String(trainerId))
    && (!creatorId || String(row.creatorId) === String(creatorId)));
}

// Rows without a name (an Intake that has no Trainer yet) are left out of the filter.
export function peopleOptions(rows, idKey, nameKey) {
  const people = new Map();
  rows.forEach((row) => { if (row[nameKey]) people.set(row[idKey], row[nameKey]); });
  return [...people].map(([id, name]) => ({ id, name })).sort((a, b) => a.name.localeCompare(b.name));
}
