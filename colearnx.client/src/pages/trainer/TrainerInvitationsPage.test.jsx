import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { AuthContext } from '../../auth/AuthContext';
import AppRouter from '../../routes/AppRouter';

vi.mock('../../components/notificationRealtime', () => ({ connectNotificationRealtime: () => ({ connected: false, stop: async () => {} }) }));

const auth = {
  user: { id: 4, email: 'trainer@colearnx.test', fullName: 'Gu Yincheng', activeRole: 'Trainer', roles: ['Trainer'] },
  booting: false,
  isAuthenticated: true,
  activeRole: 'trainer',
  roles: ['Trainer'],
  logout: vi.fn(),
  switchRole: vi.fn(),
};

const invitation = (id, status, extra = {}) => ({
  id, courseId: id * 10, courseCode: `DES-${id}`, courseTitle: `Design ${id}`, creatorName: 'Zou Ruiqi', status,
  invitedAt: '2026-10-20T01:00:00Z', respondedAt: null, removedAt: null, ...extra,
});

function json(data, status = 200) {
  return new Response(JSON.stringify(data), { status, headers: { 'Content-Type': 'application/json' } });
}

function renderPage(items, respond = (id, action) => json(invitation(id, action === 'accept' ? 'Accepted' : 'Declined', { respondedAt: '2026-10-21T01:00:00Z' }))) {
  const fetch = vi.fn(async (path, init = {}) => {
    const match = /^\/api\/trainer\/course-invitations\/(\d+)\/(accept|decline)$/.exec(path);
    if (match && init.method === 'POST') return respond(Number(match[1]), match[2]);
    return json(path === '/api/trainer/course-invitations' ? items : []);
  });
  vi.stubGlobal('fetch', fetch);
  render(<MemoryRouter initialEntries={['/trainer/invitations']}><AuthContext.Provider value={{ ...auth, token: `trainer-token-${Math.random()}` }}><AppRouter /></AuthContext.Provider></MemoryRouter>);
  return fetch;
}

afterEach(() => { cleanup(); vi.unstubAllGlobals(); });

it('groups invitations into waiting, my courses and history', async () => {
  renderPage([invitation(1, 'Invited'), invitation(2, 'Accepted'), invitation(3, 'Declined'), invitation(4, 'Removed')]);
  const waiting = await screen.findByRole('region', { name: 'Waiting for you' });
  expect(within(waiting).getByText('Design 1')).toBeTruthy();
  expect(within(waiting).getByRole('button', { name: 'Accept' })).toBeTruthy();
  expect(within(waiting).getByText(/Zou Ruiqi/)).toBeTruthy();
  expect(within(screen.getByRole('region', { name: 'My courses' })).getByText('Design 2')).toBeTruthy();
  const history = screen.getByRole('region', { name: 'History' });
  expect(within(history).getByText('Design 3')).toBeTruthy();
  expect(within(history).getByText('Design 4')).toBeTruthy();
  expect(within(history).queryByRole('button')).toBeNull();
  expect(screen.getByRole('link', { name: 'Teaching invitations' }).className).toContain('active');
});

it('moves an accepted invitation to My courses and a declined one to History', async () => {
  const fetch = renderPage([invitation(1, 'Invited'), invitation(2, 'Invited')]);
  const waiting = await screen.findByRole('region', { name: 'Waiting for you' });
  fireEvent.click(within(within(waiting).getByText('Design 1').closest('article')).getByRole('button', { name: 'Accept' }));
  expect(await within(screen.getByRole('region', { name: 'My courses' })).findByText('Design 1')).toBeTruthy();

  fireEvent.click(within(within(waiting).getByText('Design 2').closest('article')).getByRole('button', { name: 'Decline' }));
  expect(await within(screen.getByRole('region', { name: 'History' })).findByText('Design 2')).toBeTruthy();
  expect(within(waiting).getByText('No invitations are waiting for you.')).toBeTruthy();
  expect(fetch.mock.calls.filter(([, init = {}]) => init.method === 'POST').map(([path]) => path))
    .toEqual(['/api/trainer/course-invitations/1/accept', '/api/trainer/course-invitations/2/decline']);
});

it('keeps a failed answer in place and explains why', async () => {
  renderPage([invitation(1, 'Invited')], () => json({ code: 'INVITATION_NOT_PENDING', message: 'This invitation is no longer waiting for your answer.' }, 409));
  const waiting = await screen.findByRole('region', { name: 'Waiting for you' });
  const row = within(waiting).getByText('Design 1').closest('article');
  fireEvent.click(within(row).getByRole('button', { name: 'Accept' }));
  expect((await within(row).findByRole('alert')).textContent).toContain('This invitation is no longer waiting for your answer.');
  expect(within(waiting).getByText('Design 1')).toBeTruthy();
});

it('explains when there are no invitations yet', async () => {
  renderPage([]);
  expect(await screen.findByText('No teaching invitations yet')).toBeTruthy();
});
