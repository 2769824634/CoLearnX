import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, within } from '@testing-library/react';
import OpenClassForm from './OpenClassForm';

afterEach(cleanup);

const trainers = [{ trainerId: 5, fullName: 'Alex Chen', status: 'Accepted' }];

it('sends the whole schedule to the trainer from the defaults once a meeting link is given', () => {
  const onSave = vi.fn();
  render(<OpenClassForm trainers={trainers} onSave={onSave} onError={vi.fn()} />);

  // The only Trainer is chosen for the Creator.
  expect(screen.getByLabelText('Trainer').value).toBe('5');
  expect(within(screen.getByLabelText('Intake summary')).getByText(/Registration opens when you publish and closes/)).toBeTruthy();
  fireEvent.change(screen.getByLabelText('Online meeting link'), { target: { value: 'https://example.com/room' } });
  fireEvent.click(screen.getByRole('button', { name: 'Send schedule to trainer' }));

  expect(onSave).toHaveBeenCalledTimes(1);
  const [body] = onSave.mock.calls[0];
  expect(body).toMatchObject({ trainerId: 5, publish: false, sendToTrainer: true, sessionMinutes: 120, registrationClosesDaysBefore: 2, minEnrollment: 3 });
  expect(body.sessionStarts).toHaveLength(1);
});

it('previews every session and can save a draft', () => {
  const onSave = vi.fn();
  render(<OpenClassForm trainers={trainers} onSave={onSave} onError={vi.fn()} />);

  fireEvent.change(screen.getByLabelText('Number of sessions'), { target: { value: '3' } });
  expect(screen.getAllByTestId('planned-session')).toHaveLength(3);
  fireEvent.change(screen.getByLabelText('Online meeting link'), { target: { value: 'https://example.com/room' } });
  fireEvent.click(screen.getByRole('button', { name: 'Save as draft' }));

  expect(onSave.mock.calls[0][0]).toMatchObject({ publish: false, sendToTrainer: false });
  expect(onSave.mock.calls[0][0].sessionStarts).toHaveLength(3);
});

it('blocks sending and says why when the first session is too soon', () => {
  const onSave = vi.fn();
  const onError = vi.fn();
  render(<OpenClassForm trainers={trainers} onSave={onSave} onError={onError} />);

  const today = new Date();
  const iso = `${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, '0')}-${String(today.getDate()).padStart(2, '0')}`;
  fireEvent.change(screen.getByLabelText('First session date'), { target: { value: iso } });

  expect(screen.getByRole('alert').textContent).toMatch(/too soon/i);
  expect(screen.getByRole('button', { name: 'Send schedule to trainer' }).disabled).toBe(true);
  expect(onSave).not.toHaveBeenCalled();
});

it('shows the planned sessions on a calendar and marks trainers who only applied', () => {
  render(<OpenClassForm trainers={[...trainers, { trainerId: 6, fullName: 'Bo Li', status: 'Requested' }]} onSave={vi.fn()} onError={vi.fn()} />);

  fireEvent.change(screen.getByLabelText('First session date'), { target: { value: '2027-06-10' } });
  fireEvent.change(screen.getByLabelText('Number of sessions'), { target: { value: '2' } });
  const summary = screen.getByLabelText('Intake summary');
  expect(within(summary).getAllByRole('gridcell', { name: /1 session$/ })).toHaveLength(2);
  expect(screen.getByRole('option', { name: 'Bo Li (applied to teach)' })).toBeTruthy();
});
