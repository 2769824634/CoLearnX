import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import EditClassScheduleForm from './EditClassScheduleForm';

afterEach(cleanup);

const soon = (days, hour) => { const d = new Date(); d.setDate(d.getDate() + days); d.setHours(hour, 0, 0, 0); return d.toISOString(); };
const intake = {
  version: 'v1', minEnrollment: 3, registrationOpensAt: soon(-1, 9), registrationClosesAt: soon(10, 18),
  startsAt: soon(12, 18), endsAt: soon(12, 20), sessions: [{ id: 1, startsAt: soon(12, 18), endsAt: soon(12, 20) }],
};

it('asks only for the registration close time and the minimum number of learners', () => {
  const onSave = vi.fn();
  render(<EditClassScheduleForm intake={intake} onSave={onSave} onError={vi.fn()} />);

  expect(screen.queryByLabelText(/Days before teaching/)).toBeNull();
  expect(screen.queryByLabelText(/Registration starts/)).toBeNull();
  fireEvent.change(screen.getByLabelText('Minimum learners for the class to run'), { target: { value: '5' } });
  fireEvent.click(screen.getByRole('button', { name: 'Save schedule' }));

  expect(onSave).toHaveBeenCalledWith(expect.objectContaining({
    registrationOpensAt: intake.registrationOpensAt, startsAt: intake.startsAt, endsAt: intake.endsAt, minEnrollment: 5, version: 'v1',
  }));
});

it('reports a closing time after the first session instead of saving', () => {
  const onSave = vi.fn();
  const onError = vi.fn();
  render(<EditClassScheduleForm intake={intake} onSave={onSave} onError={onError} />);

  const late = new Date(intake.startsAt); late.setDate(late.getDate() + 1);
  const iso = `${late.getFullYear()}-${String(late.getMonth() + 1).padStart(2, '0')}-${String(late.getDate()).padStart(2, '0')}`;
  fireEvent.change(screen.getByLabelText('Registration closes on'), { target: { value: iso } });
  fireEvent.click(screen.getByRole('button', { name: 'Save schedule' }));

  expect(onSave).not.toHaveBeenCalled();
  expect(onError).toHaveBeenCalledWith(expect.objectContaining({ message: expect.stringMatching(/before the first session/) }));
});
