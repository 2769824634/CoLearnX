import { useState } from 'react';
import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { ScheduleForm, SessionForm } from './IntakeForms';
import { TrainerError } from './TrainerUi';
import { clearFieldErrors, initialSession, sessionPayload, toLocalInput, toUtc } from './intakeForm';

afterEach(cleanup);

it('clears only the date field that the trainer changed', () => {
  const onError = vi.fn();
  const intake = {
    registrationOpensAt: '2026-09-01T00:00:00.000Z',
    registrationClosesAt: '2026-09-20T00:00:00.000Z',
    startsAt: '2026-10-10T00:00:00.000Z',
    endsAt: '2026-10-11T00:00:00.000Z',
    minEnrollment: 2,
    version: 'v1',
  };
  render(<ScheduleForm
    intake={intake}
    error={{ fieldErrors: { registrationClosesAt: ['Registration must close after it opens.'], endsAt: ['Delivery must end after it starts.'] } }}
    onError={onError}
    onSave={vi.fn()}
  />);

  fireEvent.change(screen.getByLabelText('Registration closes'), { target: { value: '2026-09-21T08:00' } });
  expect(onError).toHaveBeenCalledWith({
    message: 'Please correct the remaining fields: Delivery must end after it starts.',
    fieldErrors: { endsAt: ['Delivery must end after it starts.'] },
  });
});

it('explains that a physical Session shares capacity with online attendance', () => {
  const intake = {
    startsAt: '2026-10-10T00:00:00.000Z',
    endsAt: '2026-10-11T00:00:00.000Z',
    minEnrollment: 2,
    version: 'v1',
  };
  render(<SessionForm
    intake={intake}
    session={{ physicalAddress: 'Studio 1', physicalCapacity: 2, startsAt: intake.startsAt, endsAt: intake.endsAt }}
    onError={vi.fn()}
    onSave={vi.fn()}
  />);

  expect(screen.getByText(/Physical and online attendance share this Session's capacity/i)).toBeTruthy();
  expect(screen.getByText(/physical booking deadline applies/i)).toBeTruthy();
  expect(screen.getByText(/Members do not choose a mode yet/i)).toBeTruthy();
});

it('clears a matching nested Session error while preserving other field errors', () => {
  const onError = vi.fn();
  const intake = {
    startsAt: '2026-10-10T00:00:00.000Z',
    endsAt: '2026-10-11T00:00:00.000Z',
    minEnrollment: 2,
    version: 'v1',
  };
  render(<SessionForm
    intake={intake}
    error={{ code: 'INVALID_INPUT', message: 'Check the session.', fieldErrors: {
      'sessions[0].startsAt': ['Session start is invalid.'],
      meetingLink: ['Provide a valid meeting link.'],
    } }}
    onError={onError}
    onSave={vi.fn()}
  />);

  fireEvent.change(screen.getByLabelText('Session starts'), { target: { value: '2026-10-10T09:00' } });
  expect(onError).toHaveBeenCalledWith({
    code: 'INVALID_INPUT',
    message: 'Please correct the remaining fields: Provide a valid meeting link.',
    fieldErrors: { meetingLink: ['Provide a valid meeting link.'] },
  });
});

it('updates the parent TrainerError banner when a nested field is corrected', () => {
  const intake = {
    startsAt: '2026-10-10T00:00:00.000Z',
    endsAt: '2026-10-11T00:00:00.000Z',
    minEnrollment: 2,
    version: 'v1',
  };
  function Harness() {
    const [error, setError] = useState({ code: 'INVALID_INPUT', message: 'Check the session.', fieldErrors: {
      'sessions[0].startsAt': ['Session start is invalid.'],
      meetingLink: ['Provide a valid meeting link.'],
    } });
    return <>
      <TrainerError error={error} />
      <SessionForm intake={intake} error={error} onError={setError} onSave={vi.fn()} />
    </>;
  }

  render(<Harness />);
  fireEvent.change(screen.getByLabelText('Session starts'), { target: { value: '2026-10-10T09:00' } });
  const banner = screen.getByRole('alert');
  expect(banner.textContent).toMatch(/Provide a valid meeting link/);
  expect(banner.textContent).not.toMatch(/Check the session|Session start is invalid/);
});

it('does not dismiss authentication or version conflict banners while editing', () => {
  const onError = vi.fn();
  const intake = {
    startsAt: '2026-10-10T00:00:00.000Z',
    endsAt: '2026-10-11T00:00:00.000Z',
    minEnrollment: 2,
    version: 'v1',
  };
  render(<SessionForm
    intake={intake}
    error={{ code: 'INTAKE_VERSION_CONFLICT', message: 'Reload the current Intake.', fieldErrors: { startsAt: ['Stale version.'] } }}
    onError={onError}
    onSave={vi.fn()}
  />);

  fireEvent.change(screen.getByLabelText('Session starts'), { target: { value: '2026-10-10T08:00' } });
  expect(onError).not.toHaveBeenCalled();
});

it('clears a correctable global validation error but preserves blocking errors', () => {
  expect(clearFieldErrors({ status: 422, message: 'Validation failed.' }, 'startsAt')).toBeNull();
  expect(clearFieldErrors({ status: 400, code: 'INVALID_INPUT', message: 'Validation failed.' }, 'startsAt')).toBeNull();
  for (const error of [
    { status: 401, message: 'Sign in again.' },
    { status: 403, message: 'Trainer role required.' },
    { status: 409, code: 'INTAKE_VERSION_CONFLICT', message: 'Reload the Intake.' },
    { status: 400, code: 'INTAKE_NOT_EDITABLE', message: 'This Intake is locked.' },
  ]) {
    expect(clearFieldErrors(error, 'startsAt')).toBe(error);
  }
});

it('parses server UTC timestamps with and without a zone consistently for local inputs', () => {
  const withZone = '2099-01-01T01:30:00.000Z';
  const withoutZone = '2099-01-01T01:30:00.000';
  expect(toLocalInput(withoutZone)).toBe(toLocalInput(withZone));
  expect(toUtc(toLocalInput(withoutZone), 'startsAt', withoutZone)).toBe(withZone);
  expect(toUtc(toLocalInput(withZone), 'startsAt', withZone)).toBe(withZone);

  const intake = { startsAt: withoutZone, endsAt: '2099-01-01T03:30:00.000', minEnrollment: 2, version: 'v1' };
  const values = {
    ...initialSession(null, intake),
    label: 'UTC session',
    meetingLink: 'https://meet.example.test/session',
    physical: false,
  };
  const payload = sessionPayload(values, intake, { startsAt: withoutZone, endsAt: intake.endsAt });
  expect(payload.startsAt).toBe(withZone);
  expect(payload.endsAt).toBe('2099-01-01T03:30:00.000Z');
});
