import { cleanup, fireEvent, render, screen, within } from '@testing-library/react';
import { afterEach, expect, test } from 'vitest';
import SessionCalendar from './SessionCalendar';

afterEach(cleanup);

const at = (year, month, day, hour, minute = 0) => new Date(year, month - 1, day, hour, minute).toISOString();
const session = (id, start, hours = 2, clashes = []) => ({
  id, label: `Session ${id}`, startsAt: start, endsAt: new Date(new Date(start).getTime() + hours * 3600000).toISOString(), clashes,
});

test('opens on the month of the first session and places each session on its day', () => {
  render(<SessionCalendar sessions={[session(1, at(2027, 3, 4, 18)), session(2, at(2027, 3, 11, 18, 30))]} />);

  expect(screen.getByRole('heading', { name: 'March 2027' })).toBeTruthy();
  const fourth = screen.getByRole('gridcell', { name: /^4 March 2027/ });
  expect(within(fourth).getByText('18:00–20:00')).toBeTruthy();
  expect(within(fourth).getByText('Session 1')).toBeTruthy();
  expect(within(screen.getByRole('gridcell', { name: /^11 March 2027/ })).getByText('18:30–20:30')).toBeTruthy();
  expect(screen.getByText(/2 sessions · 4 hours in total/)).toBeTruthy();
});

test('moves between the months that hold sessions only', () => {
  render(<SessionCalendar sessions={[session(1, at(2027, 3, 25, 9)), session(2, at(2027, 4, 1, 9))]} />);
  const previous = screen.getByRole('button', { name: 'Previous month' });
  const next = screen.getByRole('button', { name: 'Next month' });

  expect(previous.disabled).toBe(true);
  fireEvent.click(next);
  expect(screen.getByRole('heading', { name: 'April 2027' })).toBeTruthy();
  expect(within(screen.getByRole('gridcell', { name: /^1 April 2027/ })).getByText('Session 2')).toBeTruthy();
  expect(next.disabled).toBe(true);
});

test('shows every session the same way, without marking clashes', () => {
  const clash = { intakeId: 9, courseCode: 'INFT 2051', sessionLabel: 'Session 3', startsAt: at(2027, 3, 4, 17), endsAt: at(2027, 3, 4, 19) };
  render(<SessionCalendar sessions={[session(1, at(2027, 3, 4, 18), 2, [clash]), session(2, at(2027, 3, 11, 18))]} />);

  const item = within(screen.getByRole('gridcell', { name: /^4 March 2027/ })).getByText('Session 1').closest('li');
  expect(item.className).toBe('');
  expect(item.getAttribute('title')).toBeNull();
  expect(screen.queryByRole('alert')).toBeNull();
});

test('says so when there are no sessions yet', () => {
  render(<SessionCalendar sessions={[]} />);
  expect(screen.getByText('No sessions scheduled yet.')).toBeTruthy();
});
