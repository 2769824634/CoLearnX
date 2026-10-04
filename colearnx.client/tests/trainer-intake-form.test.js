import assert from 'node:assert/strict';
import test from 'node:test';
import process from 'node:process';
import { fieldMessages, initialSchedule, initialSession, safeMeetingLink, schedulePayload, sessionPayload, toLocalInput, toUtc } from '../src/pages/trainer/intakeForm.js';

const intake = { startsAt: '2026-10-10T00:00:00.000Z', endsAt: '2026-10-11T08:00:00.000Z', version: 'current-parent-version' };
const session = { label: 'Workshop', startsAt: '2026-10-10T01:00:00.000Z', endsAt: '2026-10-10T02:00:00.000Z', meetingLink: 'https://example.com/meeting' };

test('local date inputs use minute precision and round-trip UTC in UTC+8, UTC and a DST zone', () => {
  const previous = process.env.TZ;
  try {
    for (const [zone, expected] of [['Asia/Shanghai', '2026-10-10T09:00'], ['UTC', '2026-10-10T01:00'], ['America/New_York', '2026-10-09T21:00']]) {
      process.env.TZ = zone;
      assert.equal(toLocalInput(session.startsAt), expected);
      assert.equal(toUtc(expected, 'startsAt'), session.startsAt);
      assert.equal(expected.length, 16);
    }
  } finally { if (previous === undefined) delete process.env.TZ; else process.env.TZ = previous; }
});

test('invalid dates and a local time skipped by DST are rejected, not silently shifted', () => {
  const previous = process.env.TZ;
  try {
    process.env.TZ = 'America/New_York';
    for (const value of ['', 'invalid', '2026-03-08T02:30', '2026-02-30T12:00']) assert.throws(() => toUtc(value, 'startsAt'), { code: 'INVALID_FORM' });
  } finally { if (previous === undefined) delete process.env.TZ; else process.env.TZ = previous; }
});

test('editing an unrelated field preserves the later occurrence of a repeated DST hour', () => {
  const previous = process.env.TZ;
  try {
    process.env.TZ = 'America/New_York';
    const original = { ...intake, registrationOpensAt: '2026-10-01T00:00:00Z', registrationClosesAt: '2026-10-20T00:00:00Z', startsAt: '2026-11-01T06:30:00.000Z', endsAt: '2026-11-01T08:00:00.000Z' };
    assert.equal(schedulePayload(initialSchedule(original), original.version, original).startsAt, original.startsAt);
    const originalSession = { ...session, startsAt: original.startsAt, endsAt: original.endsAt };
    assert.equal(sessionPayload(initialSession(originalSession, original), original, originalSession).startsAt, original.startsAt);
  } finally { if (previous === undefined) delete process.env.TZ; else process.env.TZ = previous; }
});

test('schedule payload converts local times and forwards minEnrollment without local business rules', () => {
  const close = '2026-09-30T00:00:00.000Z';
  const values = initialSchedule({ ...intake, registrationOpensAt: '2026-09-01T00:00:00Z', registrationClosesAt: close });
  const body = schedulePayload({ ...values, status: 'Published', trainerId: 99 }, intake.version);
  assert.equal(body.registrationClosesAt, close);
  assert.equal(body.version, intake.version);
  assert.equal('status' in body, false);
  assert.equal('trainerId' in body, false);
  const inverted = schedulePayload({ ...values, registrationOpensAt: values.registrationClosesAt, registrationClosesAt: values.registrationOpensAt }, intake.version);
  assert.ok(inverted.registrationOpensAt);
  assert.ok(inverted.registrationClosesAt);
});

test('switching physical delivery off clears hidden physical fields and retains the latest parent version', () => {
  const values = { ...initialSession(session, intake), physical: false, physicalAddress: 'Old address', physicalCapacity: 20, physicalBookingDeadline: 'invalid hidden value' };
  const body = sessionPayload(values, intake);
  assert.equal(body.physicalAddress, null);
  assert.equal(body.physicalBookingDeadline, null);
  assert.equal(body.physicalCapacity, 0);
  assert.equal(body.version, intake.version);
  assert.equal(body.startsAt, session.startsAt);
});

test('physical-only and hybrid sessions assemble capacity and deadline for the server', () => {
  const values = { ...initialSession(session, intake), meetingLink: '', physical: true, physicalAddress: '  Room 3  ', physicalCapacity: '12', physicalBookingDeadline: toLocalInput(session.startsAt) };
  const body = sessionPayload(values, intake);
  assert.equal(body.physicalAddress, 'Room 3');
  assert.equal(body.physicalCapacity, 12);
  assert.equal(body.meetingLink, null);
  assert.equal(body.physicalBookingDeadline, session.startsAt);
  assert.ok(sessionPayload({ ...values, meetingLink: session.meetingLink }, intake).meetingLink);
});

test('sessions still assemble when location or window would be rejected by the server', () => {
  const values = initialSession(session, intake);
  const outside = sessionPayload({ ...values, startsAt: toLocalInput('2026-10-01T00:00:00Z') }, intake);
  assert.ok(outside.startsAt);
  const emptyLocation = sessionPayload({ ...values, meetingLink: '' }, intake);
  assert.equal(emptyLocation.meetingLink, null);
});

test('executable/non-HTTP links are never rendered as links; payload still forwards the raw value', () => {
  for (const link of ['javascript:alert(1)', 'data:text/html,test', 'file:///C:/test', 'not a URL']) {
    assert.equal(safeMeetingLink(link), null);
    assert.equal(sessionPayload({ ...initialSession(session, intake), meetingLink: link }, intake).meetingLink, link);
  }
  assert.equal(safeMeetingLink(session.meetingLink), session.meetingLink);
});

test('server model-state paths and camel-case domain fields map to the same input', () => {
  assert.deepEqual(fieldMessages({ fieldErrors: { '$.StartsAt': ['Invalid UTC.'], startsAt: ['Outside Intake.'], endsAt: ['Other.'] } }, 'startsAt'), ['Invalid UTC.', 'Outside Intake.']);
});
