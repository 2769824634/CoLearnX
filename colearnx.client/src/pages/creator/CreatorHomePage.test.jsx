import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, render, screen, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { AuthContext } from '../../auth/AuthContext';
import AppRouter from '../../routes/AppRouter';

const auth = {
  token: 'creator-token',
  user: { id: 3, email: 'creator@colearnx.test', fullName: 'Zou Ruiqi', activeRole: 'Creator', roles: ['Creator'] },
  booting: false,
  isAuthenticated: true,
  activeRole: 'creator',
  roles: ['Creator'],
  logout: vi.fn(),
  switchRole: vi.fn(),
};

const courses = [
  { id: 1, code: 'CRT-1', title: 'Draft One', status: 'Draft' },
  { id: 2, code: 'CRT-2', title: 'Draft Two', status: 'Draft' },
  { id: 3, code: 'CRT-3', title: 'In Review', status: 'PendingApproval' },
  { id: 4, code: 'CRT-4', title: 'Live Course', status: 'Published' },
  { id: 5, code: 'CRT-5', title: 'Needs Rework', status: 'Rejected' },
];

function stubApi({ failCourses = false } = {}) {
  vi.stubGlobal('fetch', vi.fn(async (path) => {
    if (path === '/api/creator/courses' && failCourses) {
      return new Response(JSON.stringify({ message: 'Unavailable' }), { status: 503, headers: { 'Content-Type': 'application/json' } });
    }
    const data = path === '/api/creator/courses' ? courses : [];
    return new Response(JSON.stringify(data), { status: 200, headers: { 'Content-Type': 'application/json' } });
  }));
}

function renderHome() {
  render(
    <MemoryRouter initialEntries={['/creator/home']}>
      <AuthContext.Provider value={auth}><AppRouter /></AuthContext.Provider>
    </MemoryRouter>,
  );
}

afterEach(() => { cleanup(); vi.unstubAllGlobals(); });

it('greets the Creator and summarises what needs attention', async () => {
  stubApi();
  renderHome();
  expect(await screen.findByRole('heading', { name: 'Welcome back, Zou.' })).toBeTruthy();
  expect(screen.getByText('1 returned course needs your attention.')).toBeTruthy();
});

it('counts courses at each stage of the publishing pipeline', async () => {
  stubApi();
  renderHome();
  const pipeline = await screen.findByRole('list', { name: 'Course pipeline' });
  const stages = within(pipeline).getAllByRole('listitem').map((item) => [item.querySelector('strong').textContent, item.querySelector('.creator-stage-count').textContent]);
  expect(stages).toEqual([['Drafts', '2'], ['In Admin review', '1'], ['Published', '1']]);
  expect(screen.getByRole('link', { name: /Needs Rework/ }).getAttribute('href')).toBe('/creator/courses/5');
});

it('links to classes and keeps quick actions', async () => {
  stubApi();
  renderHome();
  expect(await screen.findByRole('link', { name: 'All classes →' })).toBeTruthy();
  expect(screen.getByRole('link', { name: '+ Create Course' }).getAttribute('href')).toBe('/creator/courses/new');
  const actions = screen.getByRole('region', { name: 'Quick actions' });
  expect(within(actions).queryByRole('link', { name: /Upload Material/ })).toBeNull();
  expect(within(actions).getByRole('link', { name: /Usage Records/ }).getAttribute('href')).toBe('/creator/usage');
});

it('offers a retry when the workspace overview cannot load', async () => {
  stubApi({ failCourses: true });
  renderHome();
  expect(await screen.findByRole('button', { name: 'Retry loading' })).toBeTruthy();
});
