import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import TeachingDatesEditor from './TeachingDatesEditor';
import { toLocalInput } from '../trainer/intakeForm';

afterEach(cleanup);
const intake = { id: 11, startsAt: '2027-01-10T00:00:00Z', endsAt: '2027-01-13T00:00:00Z', status: 'Published', version: 'current-version' };

it('saves the chosen teaching dates with the current version and refreshes the displayed period', async () => {
  const onSaved = vi.fn();
  const save = vi.fn().mockResolvedValue({ ...intake, endsAt: new Date('2027-01-16T00:00:00').toISOString(), version: 'next-version' });
  render(<TeachingDatesEditor intake={intake} save={save} onSaved={onSaved} />);
  fireEvent.click(screen.getByRole('button', { name: 'Edit teaching dates' }));
  fireEvent.change(screen.getByLabelText('Last teaching date'), { target: { value: '2027-01-15' } });
  fireEvent.click(screen.getByRole('button', { name: 'Save dates' }));
  await waitFor(() => expect(onSaved).toHaveBeenCalled());
  expect(save.mock.calls[0][0]).toEqual({ startsAt: intake.startsAt, endsAt: new Date('2027-01-16T00:00:00').toISOString(), version: intake.version });
  expect(screen.getByRole('status').textContent).toContain('Teaching dates saved');
});

it('keeps edits after a failed save and offers refresh on a version conflict', async () => {
  const reload = vi.fn().mockResolvedValue({ ...intake, version: 'refreshed-version' });
  render(<TeachingDatesEditor intake={intake} save={vi.fn().mockRejectedValue({ code: 'INTAKE_VERSION_CONFLICT', message: 'Reload before trying again.' })} reload={reload} onSaved={vi.fn()} />);
  fireEvent.click(screen.getByRole('button', { name: 'Edit teaching dates' }));
  fireEvent.click(screen.getByRole('button', { name: 'Save dates' }));
  expect(await screen.findByRole('alert')).toBeTruthy();
  fireEvent.click(screen.getByRole('button', { name: 'Reload class' }));
  await waitFor(() => expect(reload).toHaveBeenCalledOnce());
  expect(screen.getByRole('button', { name: 'Edit teaching dates' })).toBeTruthy();
});

it('rejects an end date before the first date without submitting', () => {
  const save = vi.fn();
  render(<TeachingDatesEditor intake={intake} save={save} />);
  fireEvent.click(screen.getByRole('button', { name: 'Edit teaching dates' }));
  fireEvent.change(screen.getByLabelText('Last teaching date'), { target: { value: '2027-01-01' } });
  fireEvent.submit(screen.getByRole('button', { name: 'Save dates' }).closest('form'));
  expect(screen.getByRole('alert').textContent).toContain('on or after');
  expect(save).not.toHaveBeenCalled();
});

it('does not offer editing for a completed class', () => {
  render(<TeachingDatesEditor intake={{ ...intake, status: 'Completed' }} save={vi.fn()} />);
  expect(screen.queryByRole('button', { name: 'Edit teaching dates' })).toBeNull();
});

it('preserves both timestamps including their original clock times when unchanged', async () => {
  const save = vi.fn().mockResolvedValue(intake);
  const original = { ...intake, startsAt: '2027-01-10T09:15:00Z', endsAt: '2027-01-13T18:30:00Z' };
  render(<TeachingDatesEditor intake={original} save={save} />);
  fireEvent.click(screen.getByRole('button', { name: 'Edit teaching dates' }));
  expect(screen.getByLabelText('First teaching date').value).toBe(toLocalInput(original.startsAt).slice(0, 10));
  fireEvent.click(screen.getByRole('button', { name: 'Save dates' }));
  await waitFor(() => expect(save).toHaveBeenCalled());
  expect(save.mock.calls[0][0]).toEqual({ startsAt: original.startsAt, endsAt: original.endsAt, version: original.version });
});
