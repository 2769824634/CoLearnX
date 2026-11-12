import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, within } from '@testing-library/react';
import OpenClassForm from './OpenClassForm';

afterEach(cleanup);

it('saves the intake schedule without choosing a trainer', () => {
  const onSave = vi.fn();
  render(<OpenClassForm onSave={onSave} onError={vi.fn()} />);

  expect(screen.queryByLabelText('Trainer')).toBeNull();
  expect(within(screen.getByLabelText('Intake summary')).getByText(/Registration opens when you publish and closes/)).toBeTruthy();
  fireEvent.change(screen.getByLabelText('Online meeting link'), { target: { value: 'https://example.com/room' } });
  fireEvent.click(screen.getByRole('button', { name: 'Save intake' }));

  expect(onSave).toHaveBeenCalledTimes(1);
  const [body] = onSave.mock.calls[0];
  expect(body).toMatchObject({ publish: false, sendToTrainer: false, sessionMinutes: 120, registrationClosesDaysBefore: 2, minEnrollment: 3 });
  expect(body).not.toHaveProperty('trainerId');
  expect(body.sessionStarts).toHaveLength(1);
});

it('previews every session', () => {
  const onSave = vi.fn();
  render(<OpenClassForm onSave={onSave} onError={vi.fn()} />);

  fireEvent.change(screen.getByLabelText('Number of sessions'), { target: { value: '3' } });
  expect(screen.getAllByTestId('planned-session')).toHaveLength(3);
  fireEvent.change(screen.getByLabelText('Online meeting link'), { target: { value: 'https://example.com/room' } });
  fireEvent.click(screen.getByRole('button', { name: 'Save intake' }));

  expect(onSave.mock.calls[0][0].sessionStarts).toHaveLength(3);
});

it('blocks saving and says why when the first session is too soon', () => {
  const onSave = vi.fn();
  render(<OpenClassForm onSave={onSave} onError={vi.fn()} />);

  const today = new Date();
  const iso = `${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, '0')}-${String(today.getDate()).padStart(2, '0')}`;
  fireEvent.change(screen.getByLabelText('First session date'), { target: { value: iso } });

  expect(screen.getByRole('alert').textContent).toMatch(/too soon/i);
  expect(screen.getByRole('button', { name: 'Save intake' }).disabled).toBe(true);
  expect(onSave).not.toHaveBeenCalled();
});

it('shows the planned sessions on a calendar', () => {
  render(<OpenClassForm onSave={vi.fn()} onError={vi.fn()} />);

  fireEvent.change(screen.getByLabelText('First session date'), { target: { value: '2027-06-10' } });
  fireEvent.change(screen.getByLabelText('Number of sessions'), { target: { value: '2' } });
  const summary = screen.getByLabelText('Intake summary');
  expect(within(summary).getAllByRole('gridcell', { name: /1 session$/ })).toHaveLength(2);
});
