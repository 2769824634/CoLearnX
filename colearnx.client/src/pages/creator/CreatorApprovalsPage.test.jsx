import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, render, screen, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { AuthContext } from '../../auth/AuthContext';
import AppRouter from '../../routes/AppRouter';

const auth = {
  token: 'creator-token',
  user: { id: 3, email: 'creator@colearnx.test', fullName: 'Course Creator', activeRole: 'Creator', roles: ['Creator'] },
  booting: false,
  isAuthenticated: true,
  activeRole: 'creator',
  roles: ['Creator'],
  logout: vi.fn(),
  switchRole: vi.fn(),
};

const course = (id, status, extra = {}) => ({ id, code: `CRT-${id}`, title: `Course ${id}`, status, courseLevelName: 'Beginner', learningPathName: 'Design', creditCost: 20, reviewReason: null, ...extra });

function renderPage(courses) {
  vi.stubGlobal('fetch', vi.fn(async (path) => new Response(JSON.stringify(path === '/api/creator/courses' ? courses : []), { status: 200, headers: { 'Content-Type': 'application/json' } })));
  render(<MemoryRouter initialEntries={['/creator/approvals']}><AuthContext.Provider value={auth}><AppRouter /></AuthContext.Provider></MemoryRouter>);
}

afterEach(() => { cleanup(); vi.unstubAllGlobals(); });

it('groups submitted Courses by Admin decision and shows the feedback', async () => {
  renderPage([
    course(1, 'Draft'),
    course(2, 'PendingApproval'),
    course(3, 'Rejected', { reviewReason: 'Learning outcomes need clearer assessment criteria.' }),
    course(4, 'Published'),
  ]);
  const waiting = await screen.findByRole('region', { name: 'Waiting for Admin' });
  expect(within(waiting).getByText('Course 2')).toBeTruthy();
  const changes = screen.getByRole('region', { name: 'Needs changes' });
  expect(within(changes).getByText('Learning outcomes need clearer assessment criteria.')).toBeTruthy();
  expect(within(changes).getByRole('link', { name: 'Edit and resubmit →' }).getAttribute('href')).toBe('/creator/courses/3');
  expect(within(screen.getByRole('region', { name: 'Published' })).getByText('Course 4')).toBeTruthy();
  expect(screen.queryByText('Course 1')).toBeNull();
  expect(screen.getByRole('link', { name: 'Approval status' }).className).toContain('active');
});

it('explains when nothing has been submitted yet', async () => {
  renderPage([course(1, 'Draft')]);
  expect(await screen.findByText('No Courses submitted yet')).toBeTruthy();
});
