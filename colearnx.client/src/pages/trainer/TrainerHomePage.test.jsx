import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, render, screen, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { AuthContext } from '../../auth/AuthContext';
import AppRouter from '../../routes/AppRouter';

const auth = {
  token: 'trainer-token',
  user: { id: 4, email: 'trainer@colearnx.test', fullName: 'Gu Yincheng', activeRole: 'Trainer', roles: ['Trainer'] },
  booting: false,
  isAuthenticated: true,
  activeRole: 'trainer',
  roles: ['Trainer'],
  logout: vi.fn(),
  switchRole: vi.fn(),
};

const courses = [
  { id: 1, code: 'UX101', title: 'UX Foundations' },
  { id: 2, code: 'SEC200', title: 'Security Basics' },
];

const intakes = [
  { id: 10, courseId: 1, status: 'Draft', startsAt: '2099-03-01T01:00:00Z', endsAt: '2099-04-01T01:00:00Z' },
  { id: 11, courseId: 2, status: 'Rejected', startsAt: '2099-05-01T01:00:00Z', endsAt: '2099-06-01T01:00:00Z' },
  { id: 12, courseId: 1, status: 'PendingApproval', startsAt: '2099-07-01T01:00:00Z', endsAt: '2099-08-01T01:00:00Z' },
  { id: 13, courseId: 2, status: 'Published', startsAt: '2099-02-01T01:00:00Z', endsAt: '2099-02-20T01:00:00Z' },
  { id: 14, courseId: 1, status: 'Published', startsAt: '2099-09-01T01:00:00Z', endsAt: '2099-10-01T01:00:00Z' },
  { id: 15, courseId: 2, status: 'Published', startsAt: '2000-01-01T01:00:00Z', endsAt: '2000-02-01T01:00:00Z' },
];

function stubApi({ list = intakes, fail = false } = {}) {
  vi.stubGlobal('fetch', vi.fn(async (path) => {
    if (path === '/api/trainer/intakes' && fail) {
      return new Response(JSON.stringify({ message: 'Unavailable' }), { status: 503, headers: { 'Content-Type': 'application/json' } });
    }
    const data = path === '/api/trainer/intakes' ? list : path === '/api/courses' ? courses : [];
    return new Response(JSON.stringify(data), { status: 200, headers: { 'Content-Type': 'application/json' } });
  }));
}

function renderHome() {
  render(
    <MemoryRouter initialEntries={['/trainer/home']}>
      <AuthContext.Provider value={auth}><AppRouter /></AuthContext.Provider>
    </MemoryRouter>,
  );
}

afterEach(() => { cleanup(); vi.unstubAllGlobals(); });

it('greets the Trainer and summarises what needs attention', async () => {
  stubApi();
  renderHome();
  expect(await screen.findByRole('heading', { name: 'Welcome back, Gu.' })).toBeTruthy();
  expect(screen.getByText('2 Intakes to prepare and 1 Intake waiting for the Creator.')).toBeTruthy();
});

it('features the earliest confirmed Intake that has not ended', async () => {
  stubApi();
  renderHome();
  const next = await screen.findByRole('region', { name: 'Teaching next' });
  expect(within(next).getByText('Security Basics')).toBeTruthy();
  expect(within(next).getByRole('link', { name: 'Open Intake' }).getAttribute('href')).toBe('/trainer/courses/intakes/13');
});

it('prefers an Intake already in progress', async () => {
  stubApi({ list: [...intakes, { id: 16, courseId: 1, status: 'InProgress', startsAt: '2000-01-01T01:00:00Z', endsAt: '2099-12-01T01:00:00Z' }] });
  renderHome();
  const next = await screen.findByRole('region', { name: 'Teaching next' });
  expect(within(next).getByText('In progress')).toBeTruthy();
  expect(within(next).getByRole('link', { name: 'Open Intake' }).getAttribute('href')).toBe('/trainer/courses/intakes/16');
});

it('counts Intakes at each stage and links each stage to its filtered list', async () => {
  stubApi();
  renderHome();
  const pipeline = await screen.findByRole('list', { name: 'Intake pipeline' });
  const stages = within(pipeline).getAllByRole('link').map((link) => [link.querySelector('strong').textContent, link.querySelector('.trainer-stage-count').textContent, link.getAttribute('href')]);
  expect(stages).toEqual([
    ['Ready to prepare', '2', '/trainer/courses?status=editable'],
    ['With the Creator', '1', '/trainer/courses?status=PendingApproval'],
    ['Confirmed delivery', '3', '/trainer/courses?status=delivery'],
  ]);
});

it('explains the empty state and keeps quick actions', async () => {
  stubApi({ list: [] });
  renderHome();
  expect(await screen.findByText('Nothing needs your attention right now.')).toBeTruthy();
  expect(within(screen.getByRole('region', { name: 'Teaching next' })).getByText('No confirmed delivery yet')).toBeTruthy();
  expect(screen.getByRole('link', { name: '+ Create Intake' }).getAttribute('href')).toBe('/trainer/courses/new');
  const actions = screen.getByRole('region', { name: 'Quick actions' });
  expect(within(actions).getByRole('link', { name: /Attendance/ }).getAttribute('href')).toBe('/trainer/attendance');
  expect(within(actions).getByRole('link', { name: /Learner List/ }).getAttribute('href')).toBe('/trainer/learners');
});

it('offers a retry when Intakes cannot load', async () => {
  stubApi({ fail: true });
  renderHome();
  expect(await screen.findByRole('button', { name: 'Try again' })).toBeTruthy();
});
