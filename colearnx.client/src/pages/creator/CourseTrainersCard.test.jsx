import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, within } from '@testing-library/react';
import { AuthContext } from '../../auth/AuthContext';
import CourseTrainersCard from './CourseTrainersCard';

const auth = { user: { id: 1 }, isAuthenticated: true, activeRole: 'creator' };

const row = (trainerId, fullName, status) => ({ trainerId, fullName, headline: `${fullName} headline`, status, invitedAt: '2026-10-20T01:00:00Z', respondedAt: null, removedAt: null });
const roster = [row(2, 'Alex Chen', 'Accepted'), row(3, 'Blair Ng', 'Invited'), row(4, 'Casey Lee', 'Declined'), row(5, 'Drew Park', 'Removed')];

const isGet = (init) => !init.method || init.method === 'GET';

function json(data, status = 200) {
  return new Response(JSON.stringify(data), { status, headers: { 'Content-Type': 'application/json' } });
}

function mount(handler = () => null) {
  const fetch = vi.fn(async (path, init = {}) => {
    const custom = await handler(path, init);
    if (custom) return custom;
    if (path === '/api/creator/courses/9/trainers' && isGet(init)) return json(roster);
    if (path.startsWith('/api/creator/trainers?search=')) return json([
      { id: 2, fullName: 'Alex Chen', headline: 'Coach', specialisations: 'Accessibility' },
      { id: 6, fullName: 'Erin Dual', headline: 'Facilitator', specialisations: 'Workshops' },
    ]);
    return json(null);
  });
  vi.stubGlobal('fetch', fetch);
  render(<AuthContext.Provider value={{ ...auth, token: `creator-token-${Math.random()}` }}><CourseTrainersCard courseId={9} /></AuthContext.Provider>);
  return fetch;
}

const calls = (fetch, predicate) => fetch.mock.calls.filter(([path, init = {}]) => predicate(path, init));

afterEach(() => { cleanup(); vi.unstubAllGlobals(); });

it('shows each roster status with the action the Creator can take', async () => {
  mount();
  const alex = (await screen.findByText('Alex Chen')).closest('li');
  expect(within(alex).getByText('Available')).toBeTruthy();
  expect(within(alex).getByRole('button', { name: 'Remove' })).toBeTruthy();
  const blair = screen.getByText('Blair Ng').closest('li');
  expect(within(blair).getByText('Invited')).toBeTruthy();
  expect(within(blair).getByRole('button', { name: 'Withdraw' })).toBeTruthy();
  expect(within(screen.getByText('Casey Lee').closest('li')).getByRole('button', { name: 'Invite again' })).toBeTruthy();
  expect(within(screen.getByText('Drew Park').closest('li')).getByRole('button', { name: 'Invite again' })).toBeTruthy();
});

it('explains an empty roster', async () => {
  mount((path, init) => path === '/api/creator/courses/9/trainers' && isGet(init) ? json([]) : null);
  expect(await screen.findByText('No trainers yet. Invite one to teach this course.')).toBeTruthy();
});

it('searches after two characters and refreshes the roster after an invitation', async () => {
  const fetch = mount((path, init) => init.method === 'POST' ? json(row(6, 'Erin Dual', 'Invited')) : null);
  fireEvent.click(await screen.findByRole('button', { name: 'Invite trainer' }));
  const dialog = screen.getByRole('dialog', { name: 'Invite a trainer' });
  const search = within(dialog).getByLabelText('Search trainers by name');

  fireEvent.change(search, { target: { value: 'e' } });
  await new Promise((resolve) => setTimeout(resolve, 400));
  expect(calls(fetch, (path) => path.startsWith('/api/creator/trainers'))).toHaveLength(0);

  fireEvent.change(search, { target: { value: 'er' } });
  const erin = (await within(dialog).findByText('Erin Dual')).closest('li');
  expect(calls(fetch, (path) => path.startsWith('/api/creator/trainers'))).toEqual([['/api/creator/trainers?search=er', expect.anything()]]);
  const alex = within(dialog).getByText('Alex Chen').closest('li');
  expect(within(alex).queryByRole('button', { name: 'Invite' })).toBeNull();
  expect(within(alex).getByText('Available')).toBeTruthy();

  fireEvent.click(within(erin).getByRole('button', { name: 'Invite' }));
  expect(await within(dialog).findByText('Invitation sent to Erin Dual.')).toBeTruthy();
  const [, invite] = calls(fetch, (path, init) => init.method === 'POST')[0];
  expect(JSON.parse(invite.body)).toEqual({ trainerId: 6 });
  expect(calls(fetch, (path, init) => path === '/api/creator/courses/9/trainers' && isGet(init))).toHaveLength(2);
});

it('asks before removing an available trainer', async () => {
  const fetch = mount((path, init) => init.method === 'POST' ? json(row(2, 'Alex Chen', 'Removed')) : null);
  fireEvent.click(within((await screen.findByText('Alex Chen')).closest('li')).getByRole('button', { name: 'Remove' }));
  const dialog = screen.getByRole('dialog', { name: 'Remove Alex Chen?' });
  expect(calls(fetch, (path, init) => init.method === 'POST')).toHaveLength(0);

  fireEvent.click(within(dialog).getByRole('button', { name: 'Remove trainer' }));
  await screen.findByRole('button', { name: 'Invite trainer' });
  await vi.waitFor(() => expect(calls(fetch, (path, init) => init.method === 'POST').map(([path]) => path)).toEqual(['/api/creator/courses/9/trainers/2/remove']));
  expect(screen.queryByRole('dialog')).toBeNull();
});

it('shows why an action failed', async () => {
  mount((path, init) => init.method === 'POST'
    ? json({ code: 'TRAINER_ALREADY_INVITED', message: 'This Trainer is already invited to or teaching this Course.' }, 409) : null);
  fireEvent.click(within((await screen.findByText('Casey Lee')).closest('li')).getByRole('button', { name: 'Invite again' }));
  expect((await screen.findByRole('alert')).textContent).toContain('This Trainer is already invited to or teaching this Course.');
});
