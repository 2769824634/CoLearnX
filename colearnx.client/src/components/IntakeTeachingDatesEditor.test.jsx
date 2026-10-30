import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { ApiError } from '../api/client';
import IntakeTeachingDatesEditor from './IntakeTeachingDatesEditor';

afterEach(() => cleanup());

const intake = {
  id: 9, status: 'InProgress', version: 'version-one', registrationClosesAt: '2026-10-01T00:00:00Z',
  startsAt: '2026-11-10T09:15:12.345Z', endsAt: '2026-11-12T11:45:43.210Z', sessions: [],
};

it('preserves unchanged UTC instants and sends only the dates and version', async () => {
  const onSave = vi.fn().mockResolvedValue({});
  const onClose = vi.fn();
  render(<IntakeTeachingDatesEditor intake={intake} onSave={onSave} onClose={onClose} />);
  fireEvent.submit(screen.getByRole('button', { name: 'Save dates' }).closest('form'));
  await waitFor(() => expect(onClose).toHaveBeenCalledWith(true));
  expect(onSave).toHaveBeenCalledWith({ startsAt: intake.startsAt, endsAt: intake.endsAt, version: intake.version });
});

it('stores an inclusive last teaching day at the next local midnight', async () => {
  const onSave = vi.fn().mockResolvedValue({});
  render(<IntakeTeachingDatesEditor intake={intake} onSave={onSave} onClose={vi.fn()} />);
  fireEvent.change(screen.getByLabelText('Last teaching date'), { target: { value: '2026-11-15' } });
  fireEvent.submit(screen.getByRole('button', { name: 'Save dates' }).closest('form'));
  await waitFor(() => expect(onSave).toHaveBeenCalledWith({ startsAt: intake.startsAt,
    endsAt: new Date(2026, 10, 16).toISOString(), version: intake.version }));
});

it('keeps the editor open when the proposed window excludes an existing session', async () => {
  const onSave = vi.fn();
  render(<IntakeTeachingDatesEditor intake={{ ...intake,
    sessions: [{ startsAt: intake.startsAt, endsAt: intake.endsAt }] }} onSave={onSave} onClose={vi.fn()} />);
  fireEvent.change(screen.getByLabelText('Last teaching date'), { target: { value: '2026-11-11' } });
  fireEvent.submit(screen.getByRole('button', { name: 'Save dates' }).closest('form'));
  expect(await screen.findByRole('alert')).toHaveProperty('textContent', 'The teaching dates must include every existing session.');
  expect(onSave).not.toHaveBeenCalled();
  expect(screen.getByRole('dialog')).toBeTruthy();
});

it('blocks stale saves and lets the user reload the class', async () => {
  const onSave = vi.fn().mockRejectedValue(new ApiError('INTAKE_VERSION_CONFLICT', 'The class has changed.', 409));
  const onReload = vi.fn().mockResolvedValue({});
  render(<IntakeTeachingDatesEditor intake={intake} onSave={onSave} onClose={vi.fn()} onReload={onReload} />);
  fireEvent.submit(screen.getByRole('button', { name: 'Save dates' }).closest('form'));
  expect(await screen.findByRole('alert')).toHaveProperty('textContent', 'The class has changed.Reload class');
  expect(screen.getByRole('button', { name: 'Save dates' }).disabled).toBe(true);
  fireEvent.click(screen.getByRole('button', { name: 'Reload class' }));
  await waitFor(() => expect(onReload).toHaveBeenCalledOnce());
});

it('disables saving after the class becomes closed', () => {
  render(<IntakeTeachingDatesEditor intake={{ ...intake, status: 'Completed' }} onSave={vi.fn()} onClose={vi.fn()} />);
  expect(screen.getByRole('button', { name: 'Save dates' }).disabled).toBe(true);
  expect(screen.getByRole('alert').textContent).toBe('This class no longer allows date changes.');
});
