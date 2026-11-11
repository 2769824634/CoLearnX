import { expect, it } from 'vitest';
import { defaultOpenClass, openClassPayload, planClass } from './openClass';

const now = new Date(2026, 10, 12, 10, 0); // Thu 12 Nov 2026 10:00 local

it('starts with values that can be published without touching a date', () => {
  const values = { ...defaultOpenClass(now), meetingLink: 'https://example.com/room' };
  expect(values.firstDate).toBe('2026-11-26');
  expect(values.firstTime).toBe('18:00');
  expect(planClass(values, now).problem).toBeNull();
  const body = openClassPayload(values, 7, true, now);
  expect(body).toMatchObject({ trainerId: 7, sessionMinutes: 120, registrationClosesDaysBefore: 2, minEnrollment: 3, publish: true, meetingLink: 'https://example.com/room' });
  expect(body.sessionStarts).toEqual([new Date(2026, 10, 26, 18, 0).toISOString()]);
});

it('repeats weekly at the same local time and closes registration before the first session', () => {
  const plan = planClass({ ...defaultOpenClass(now), sessionCount: '3', everyDays: '7' }, now);
  expect(plan.sessions.map((session) => session.start)).toEqual([
    new Date(2026, 10, 26, 18, 0), new Date(2026, 11, 3, 18, 0), new Date(2026, 11, 10, 18, 0),
  ]);
  expect(plan.sessions[0].end).toEqual(new Date(2026, 10, 26, 20, 0));
  expect(plan.closes).toEqual(new Date(2026, 10, 24, 18, 0));
});

it('explains the earliest possible first session when the chosen one is too soon', () => {
  const plan = planClass({ ...defaultOpenClass(now), firstDate: '2026-11-13', firstTime: '09:00' }, now);
  expect(plan.problem).toMatch(/too soon/i);
  expect(plan.problem).toContain('14 Nov');
  expect(() => openClassPayload({ ...defaultOpenClass(now), firstDate: '2026-11-13', firstTime: '09:00', meetingLink: 'https://example.com' }, 7, true, now)).toThrow(/too soon/i);
});

it('requires a place to meet and a trainer', () => {
  expect(() => openClassPayload(defaultOpenClass(now), 7, true, now)).toThrow(/meeting link or a physical location/i);
  expect(() => openClassPayload({ ...defaultOpenClass(now), meetingLink: 'https://example.com' }, '', true, now)).toThrow(/trainer/i);
});

it('sends a physical location only when one is included', () => {
  const body = openClassPayload({ ...defaultOpenClass(now), physical: true, physicalAddress: ' Room 2 ', physicalCapacity: '12' }, 7, false, now);
  expect(body).toMatchObject({ meetingLink: null, physicalAddress: 'Room 2', physicalCapacity: 12, publish: false });
});

import { initialSession } from '../trainer/intakeForm';

it('pre-fills the next session from the class and the sessions it already has', () => {
  const intake = { startsAt: new Date(2026, 10, 26, 0, 0).toISOString(), endsAt: new Date(2026, 11, 11, 0, 0).toISOString(), sessions: [] };
  // A first session starts in the morning, not at midnight.
  expect(initialSession(undefined, intake)).toMatchObject({ label: 'Session 1', sessionDate: '2026-11-26', startTime: '09:00', meetingLink: '' });

  const withOne = { ...intake, sessions: [{ id: 1, label: 'Session 1', startsAt: new Date(2026, 10, 26, 18, 0).toISOString(), endsAt: new Date(2026, 10, 26, 20, 0).toISOString(), meetingLink: 'https://example.com/room' }] };
  // The next one follows a week later at the same time, in the same room.
  expect(initialSession(undefined, withOne)).toMatchObject({ label: 'Session 2', sessionDate: '2026-12-03', startTime: '18:00', meetingLink: 'https://example.com/room' });
});

import { editSchedulePayload, initialEditSchedule, localDateTime } from './openClass';

const klass = {
  version: 'v1', minEnrollment: 3,
  registrationOpensAt: new Date(2026, 10, 12, 9, 0).toISOString(),
  registrationClosesAt: new Date(2026, 10, 24, 18, 0).toISOString(),
  startsAt: new Date(2026, 10, 26, 18, 0).toISOString(),
  endsAt: new Date(2026, 11, 3, 20, 0).toISOString(),
  sessions: [{ id: 1, startsAt: new Date(2026, 10, 26, 18, 0).toISOString(), endsAt: new Date(2026, 10, 26, 20, 0).toISOString() }],
};

it('shows class times in the local time zone', () => {
  expect(localDateTime(klass.startsAt)).toBe('Thu 26 Nov 2026, 18:00');
  // The API may omit the Z suffix; the value is still UTC.
  expect(localDateTime(klass.startsAt.replace('Z', ''))).toBe('Thu 26 Nov 2026, 18:00');
});

it('edits only the registration close time and the minimum, keeping every other date', () => {
  const values = initialEditSchedule(klass);
  expect(values).toEqual({ closesDate: '2026-11-24', closesTime: '18:00', minEnrollment: '3' });
  const body = editSchedulePayload(klass, { ...values, closesDate: '2026-11-25', minEnrollment: '4' }, now);
  expect(body).toEqual({
    registrationOpensAt: klass.registrationOpensAt, registrationClosesAt: new Date(2026, 10, 25, 18, 0).toISOString(),
    startsAt: klass.startsAt, endsAt: klass.endsAt, minEnrollment: 4, daysBeforeTeaching: 0, version: 'v1',
  });
});

it('keeps registration closing in the future and before the first session', () => {
  const values = initialEditSchedule(klass);
  expect(() => editSchedulePayload(klass, { ...values, closesDate: '2026-11-27' }, now)).toThrow(/before the first session.*Thu 26 Nov 2026, 18:00/);
  expect(() => editSchedulePayload(klass, { ...values, closesDate: '2026-11-11' }, now)).toThrow(/in the future/);
});
