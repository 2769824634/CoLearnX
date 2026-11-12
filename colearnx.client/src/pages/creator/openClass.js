import { ApiError } from '../../api/client.js';
import { utcDate } from '../../utils/utcDates.js';

// The Creator says when the Intake meets. Registration opens on publishing and closes a number of days
// before the first session, so the dates can never contradict each other.
const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
const DAYS = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];
const pad = (part) => String(part).padStart(2, '0');

export const dateInput = (date) => `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
export const dayLabel = (date) => `${DAYS[date.getDay()]} ${date.getDate()} ${MONTHS[date.getMonth()]}`;
export const timeLabel = (date) => `${pad(date.getHours())}:${pad(date.getMinutes())}`;

export function defaultOpenClass(now = new Date()) {
  const first = new Date(now.getFullYear(), now.getMonth(), now.getDate() + 14);
  return {
    firstDate: dateInput(first), firstTime: '18:00', sessionCount: '1', sessionHours: '2', everyDays: '7',
    closesDaysBefore: '2', minEnrollment: '3', meetingLink: '', physical: false, physicalAddress: '', physicalCapacity: '',
  };
}

const noPlan = (problem) => ({ sessions: [], closes: null, problem });

// Local wall-clock times, so "every Thursday 18:00" stays 18:00 across daylight-saving changes.
export function planClass(values, now = new Date()) {
  const [year, month, day] = String(values.firstDate || '').split('-').map(Number);
  const [hour, minute] = String(values.firstTime || '').slice(0, 5).split(':').map(Number);
  const count = Number(values.sessionCount);
  const minutes = Math.round(Number(values.sessionHours) * 60);
  const every = Number(values.everyDays) || 7;
  const before = Number(values.closesDaysBefore);
  if (![year, month, day, hour, minute].every(Number.isInteger)) return noPlan('Choose the date and time of the first session.');
  if (!Number.isInteger(count) || count < 1 || count > 60) return noPlan('Enter 1 to 60 sessions.');
  if (!(minutes >= 30 && minutes <= 720)) return noPlan('A session lasts between 30 minutes and 12 hours.');
  if (values.closesDaysBefore === '' || !Number.isInteger(before) || before < 0 || before > 60) {
    return noPlan('Registration closes 0 to 60 days before the first session.');
  }

  const sessions = Array.from({ length: count }, (_, index) => {
    const start = new Date(year, month - 1, day + index * every, hour, minute);
    return { start, end: new Date(start.getTime() + minutes * 60000) };
  });
  const closes = new Date(year, month - 1, day - before, hour, minute);
  let problem = null;
  if (closes.getTime() <= now.getTime()) {
    const earliest = new Date(now.getFullYear(), now.getMonth(), now.getDate() + before, now.getHours(), now.getMinutes());
    problem = `The first session is too soon. With registration closing ${before} day${before === 1 ? '' : 's'} before it, `
      + `the first session can start after ${dayLabel(earliest)} ${timeLabel(earliest)}.`;
  }
  return { sessions, closes, problem };
}

function invalid(field, message) {
  throw new ApiError('INVALID_FORM', message, 400, { [field]: [message] });
}

// Publishing waits for the Trainer to accept the schedule, so the form can only send it or keep a draft.
export function openClassPayload(values, trainerId, sendToTrainer, now = new Date()) {
  if (!trainerId) invalid('trainerId', 'Choose the Trainer who will teach this intake.');
  const plan = planClass(values, now);
  if (plan.problem) invalid('sessionStarts', plan.problem);
  const meetingLink = String(values.meetingLink || '').trim() || null;
  const physicalAddress = values.physical ? String(values.physicalAddress || '').trim() || null : null;
  if (!meetingLink && !physicalAddress) invalid('meetingLink', 'Provide an online meeting link or a physical location.');
  const minEnrollment = Number(values.minEnrollment);
  if (!Number.isInteger(minEnrollment) || minEnrollment < 2 || minEnrollment > 200) {
    invalid('minEnrollment', 'The minimum is between 2 and 200 learners.');
  }
  const physicalCapacity = physicalAddress ? Number(values.physicalCapacity) : 0;
  if (physicalAddress && !(Number.isInteger(physicalCapacity) && physicalCapacity >= minEnrollment)) {
    invalid('physicalCapacity', 'The room must hold at least the minimum number of learners.');
  }
  return {
    trainerId: Number(trainerId),
    sessionStarts: plan.sessions.map((session) => session.start.toISOString()),
    sessionMinutes: Math.round(Number(values.sessionHours) * 60),
    registrationClosesDaysBefore: Number(values.closesDaysBefore),
    meetingLink, physicalAddress, physicalCapacity, minEnrollment, publish: false, sendToTrainer: Boolean(sendToTrainer),
  };
}

// Intake times are shown the way the Creator entered them: in the local time zone.
export function localDateTime(value) {
  if (!value) return 'Date unavailable';
  const date = value instanceof Date ? value : utcDate(value);
  if (!Number.isFinite(date.getTime())) return 'Date unavailable';
  return `${dayLabel(date)} ${date.getFullYear()}, ${timeLabel(date)}`;
}

// After an Intake exists, only the registration close time and the minimum are edited here.
// Teaching dates follow the sessions.
export function initialEditSchedule(intake) {
  const closes = utcDate(intake.registrationClosesAt);
  return { closesDate: dateInput(closes), closesTime: timeLabel(closes), minEnrollment: String(intake.minEnrollment ?? 3) };
}

export function editSchedulePayload(intake, values, now = new Date()) {
  const [year, month, day] = String(values.closesDate || '').split('-').map(Number);
  const [hour, minute] = String(values.closesTime || '').slice(0, 5).split(':').map(Number);
  if (![year, month, day, hour, minute].every(Number.isInteger)) invalid('registrationClosesAt', 'Choose when registration closes.');
  const closes = new Date(year, month - 1, day, hour, minute);
  if (closes.getTime() <= now.getTime()) invalid('registrationClosesAt', 'Registration must close in the future.');
  // Teaching starts with the first session, or earlier when the Intake was given explicit teaching dates.
  const first = [utcDate(intake.startsAt), ...(intake.sessions || []).map((session) => utcDate(session.startsAt))]
    .sort((left, right) => left.getTime() - right.getTime())[0];
  if (closes.getTime() > first.getTime()) {
    invalid('registrationClosesAt', `Registration must close before the first session, which starts ${localDateTime(first)}.`);
  }
  const minEnrollment = Number(values.minEnrollment);
  if (!Number.isInteger(minEnrollment) || minEnrollment < 2 || minEnrollment > 200) {
    invalid('minEnrollment', 'The minimum is between 2 and 200 learners.');
  }
  return {
    registrationOpensAt: utcDate(intake.registrationOpensAt).toISOString(), registrationClosesAt: closes.toISOString(),
    startsAt: utcDate(intake.startsAt).toISOString(), endsAt: utcDate(intake.endsAt).toISOString(),
    minEnrollment, daysBeforeTeaching: 0, version: intake.version,
  };
}
