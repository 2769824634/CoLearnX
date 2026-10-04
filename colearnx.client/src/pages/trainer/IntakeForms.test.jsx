import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { ScheduleForm } from './IntakeForms';

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
    fieldErrors: { endsAt: ['Delivery must end after it starts.'] },
  });
});
