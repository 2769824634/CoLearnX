import { utcDate } from '../utils/utcDates';

const utcFormatter = new Intl.DateTimeFormat('en-GB', {
  year: 'numeric', month: 'short', day: '2-digit', hour: '2-digit', minute: '2-digit',
  hour12: false, timeZone: 'UTC', timeZoneName: 'short',
});

export function formatCount(count, singular, plural = `${singular}s`) {
  const value = Number(count);
  const amount = Number.isFinite(value) ? value : 0;
  return `${amount} ${amount === 1 ? singular : plural}`;
}

export function formatUtcDateTime(value) {
  if (!value) return 'Date unavailable';
  const date = utcDate(value);
  return Number.isFinite(date.getTime()) ? utcFormatter.format(date) : 'Date unavailable';
}

export function formatUtcRange(startsAt, endsAt) {
  return `${formatUtcDateTime(startsAt)} → ${formatUtcDateTime(endsAt)}`;
}

// An older deployment can still return unexpected implementation details.
export function userFacingError(error, fallback = 'The request could not be completed. Please try again.') {
  const reference = error?.traceId ? ` Reference: ${error.traceId}` : '';
  if (!error || error.status >= 500) return fallback + reference;
  const message = String(error.message || '');
  return message && !/SqlServer|DbUpdate|ExecutionStrategy|System\.|stack trace|InnerException|transaction|Internal Server Error/i.test(message)
    ? message + reference : fallback + reference;
}

const fieldLabels = {
  startsat: 'Start date and time', endsat: 'End date and time', registrationopensat: 'Registration opens',
  registrationclosesat: 'Registration closes', physicalbookingdeadline: 'Physical booking deadline',
  title: 'Title', label: 'Session label', courseid: 'Course', mode: 'Delivery mode',
  physicalcapacity: 'Physical capacity', minenrollment: 'Minimum enrollment', physicaladdress: 'Physical address',
  meetinglink: 'Meeting link', code: 'Course code', creditcost: 'Credit cost', category: 'Category',
  courselevelid: 'Course level', learningpathid: 'Learning path', learningoutcomes: 'Learning outcomes',
};
export function friendlyFieldLabel(field) {
  const leaf = String(field).split('.').pop().replace(/\[\d+\]/g, '').toLowerCase();
  return fieldLabels[leaf] || 'Form field';
}
