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
    return json(null);
  });
  vi.stubGlobal('fetch', fetch);
  render(<AuthContext.Provider value={{ ...auth, token: `creator-token-${Math.random()}` }}><CourseTrainersCard courseId={9} /></AuthContext.Provider>);
  return fetch;
}

const posts = (fetch) => fetch.mock.calls.filter(([, init = {}]) => init.method === 'POST').map(([path]) => path);

afterEach(() => { cleanup(); vi.unstubAllGlobals(); });

it('lists the course trainers without a course-level invite', async () => {
  mount();
  const alex = (await screen.findByText('Alex Chen')).closest('li');
  expect(within(alex).getByText('Available')).toBeTruthy();
  expect(within(alex).getByRole('button', { name: 'Remove' })).toBeTruthy();
  expect(within(screen.getByText('Blair Ng').closest('li')).getByRole('button', { name: 'Withdraw' })).toBeTruthy();
  expect(within(screen.getByText('Casey Lee').closest('li')).queryByRole('button')).toBeNull();
  expect(within(screen.getByText('Drew Park').closest('li')).queryByRole('button')).toBeNull();
  expect(screen.queryByRole('button', { name: /Invite/ })).toBeNull();
  expect(screen.getByText('Trainers join this Course when they accept an Intake.')).toBeTruthy();
});

it('explains an empty roster', async () => {
  mount((path, init) => path === '/api/creator/courses/9/trainers' && isGet(init) ? json([]) : null);
  expect(await screen.findByText('No trainers yet. Invite one from an Intake.')).toBeTruthy();
});

it('asks before removing an available trainer', async () => {
  const fetch = mount((path, init) => init.method === 'POST' ? json(row(2, 'Alex Chen', 'Removed')) : null);
  fireEvent.click(within((await screen.findByText('Alex Chen')).closest('li')).getByRole('button', { name: 'Remove' }));
  const dialog = screen.getByRole('dialog', { name: 'Remove Alex Chen?' });
  expect(posts(fetch)).toHaveLength(0);

  fireEvent.click(within(dialog).getByRole('button', { name: 'Remove trainer' }));
  await vi.waitFor(() => expect(posts(fetch)).toEqual(['/api/creator/courses/9/trainers/2/remove']));
  expect(screen.queryByRole('dialog')).toBeNull();
});

it('shows why an action failed', async () => {
  mount((path, init) => init.method === 'POST'
    ? json({ code: 'COURSE_TRAINER_NOT_FOUND', message: 'This Trainer is not on the Course roster.' }, 404) : null);
  fireEvent.click(within((await screen.findByText('Blair Ng')).closest('li')).getByRole('button', { name: 'Withdraw' }));
  expect((await screen.findByRole('alert')).textContent).toContain('This Trainer is not on the Course roster.');
});
