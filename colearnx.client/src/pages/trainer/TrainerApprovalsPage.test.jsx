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

const courses = [{ id: 1, code: 'UX101', title: 'UX Foundations' }];
const intake = (id, status) => ({ id, courseId: 1, status, startsAt: '2099-03-01T01:00:00Z', endsAt: '2099-04-01T01:00:00Z' });

function renderPage(intakes) {
  const fetch = vi.fn(async (path) => {
    const data = path === '/api/trainer/intakes' ? intakes
      : path === '/api/courses' ? courses
        : path === '/api/trainer/intakes/11' ? { ...intake(11, 'Rejected'), confirmationNote: 'Clarify the delivery dates' }
          : [];
    return new Response(JSON.stringify(data), { status: 200, headers: { 'Content-Type': 'application/json' } });
  });
  vi.stubGlobal('fetch', fetch);
  render(<MemoryRouter initialEntries={['/trainer/approvals']}><AuthContext.Provider value={auth}><AppRouter /></AuthContext.Provider></MemoryRouter>);
  return fetch;
}

afterEach(() => { cleanup(); vi.unstubAllGlobals(); });

it('groups submitted Intakes by Creator decision and shows the rejection note', async () => {
  const fetch = renderPage([intake(10, 'Draft'), intake(11, 'Rejected'), intake(12, 'PendingApproval'), intake(13, 'Published'), intake(14, 'Completed')]);
  const changes = await screen.findByRole('region', { name: 'Needs changes' });
  expect(await within(changes).findByText('Clarify the delivery dates')).toBeTruthy();
  expect(within(changes).getByRole('link', { name: 'Revise Intake →' }).getAttribute('href')).toBe('/trainer/courses/intakes/11');
  expect(within(screen.getByRole('region', { name: 'Waiting for the Creator' })).getByText('Intake #12')).toBeTruthy();
  const approved = screen.getByRole('region', { name: 'Approved' });
  expect(within(approved).getByText('Intake #13')).toBeTruthy();
  expect(within(approved).getByText('Intake #14')).toBeTruthy();
  expect(screen.queryByText('Intake #10')).toBeNull();
  expect(fetch.mock.calls.map(([path]) => path)).not.toContain('/api/trainer/intakes/12');
  expect(screen.getByRole('link', { name: 'Approval status' }).className).toContain('active');
});

it('explains when nothing has been submitted yet', async () => {
  renderPage([intake(10, 'Draft')]);
  expect(await screen.findByText('No Intakes submitted yet')).toBeTruthy();
});
