import { useState } from 'react';
import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { AuthContext } from '../auth/AuthContext';
import { AuthProvider } from '../auth/AuthProvider';
import AppRouter from './AppRouter';

vi.mock('../components/MemberNotifications', () => ({ default: () => null }));
vi.mock('../components/MemberNotificationsProvider', () => ({ MemberNotificationsProvider: ({ children }) => children }));

afterEach(() => { cleanup(); localStorage.clear(); vi.unstubAllGlobals(); });

function json(data, status = 200) {
  return new Response(JSON.stringify(data), { status, headers: { 'Content-Type': 'application/json' } });
}

function TestAuth({ initialUser, children }) {
  const [user, setUser] = useState(initialUser);
  return <AuthContext.Provider value={{ user, token: 'token', isAuthenticated: true, booting: false, activeRole: user.activeRole.toLowerCase(), logout: vi.fn(), refreshUser: setUser }}>{children}</AuthContext.Provider>;
}

function renderWorkspace(role, path, requests = [], approved = false) {
  const user = {
    id: 1, fullName: 'Demo User', displayName: 'Demo', email: 'demo@example.test',
    roles: [role], activeRole: role, certifiedAt: null,
  };
  const fetch = vi.fn(async (url, options = {}) => {
    if (url === '/api/role-requests/my') return requests instanceof Error ? json({ message: requests.message }, 500) : json(requests);
    if (url === '/api/role-requests/specialties') return json([]);
    if (url === '/api/auth/me') return json({ ...user, certifiedAt: approved ? '2026-11-12T00:00:00Z' : null });
    if (url === '/api/users/1' && options.method === 'PUT') return json({ ...user, ...JSON.parse(options.body) });
    return json([]);
  });
  vi.stubGlobal('fetch', fetch);
  render(
    <MemoryRouter initialEntries={[path]}>
      <TestAuth initialUser={user}>
        <AppRouter />
      </TestAuth>
    </MemoryRouter>,
  );
  return fetch;
}

it.each(['Trainer', 'Creator'])('shows the %s certification home without loading protected workspace data', async (role) => {
  const fetch = renderWorkspace(role, `/${role.toLowerCase()}/home`);
  expect(await screen.findByText('You have not applied for certification yet.')).toBeTruthy();
  expect(screen.getByRole('link', { name: 'Start certification' })).toBeTruthy();
  expect(screen.getByRole('link', { name: 'Edit my profile' })).toBeTruthy();
  expect(screen.getByRole('navigation', { name: `${role.toLowerCase()} navigation` })).toBeTruthy();
  expect(fetch.mock.calls.some(([url]) => /^\/api\/(trainer|creator)\//.test(url))).toBe(false);
});

it('shows the pending application and an option to view it', async () => {
  renderWorkspace('Creator', '/creator/home', [{ requestedRole: 'Creator', status: 'Pending' }]);
  expect(await screen.findByText('Submitted. Waiting for administrator review.')).toBeTruthy();
  expect(screen.getByRole('link', { name: 'View my application' }).getAttribute('href')).toBe('/creator/certification');
});

it('shows the rejection reason and permits reapplying', async () => {
  renderWorkspace('Trainer', '/trainer/home', [{ requestedRole: 'Trainer', status: 'Rejected', reviewNote: 'Unreadable ID' }]);
  expect(await screen.findByText(/Not approved: Unreadable ID/)).toBeTruthy();
  expect(screen.getByRole('link', { name: 'Start certification' })).toBeTruthy();
});

it('does not report an application as missing when status loading fails', async () => {
  renderWorkspace('Trainer', '/trainer/home', new Error('Status unavailable'));
  expect(await screen.findByRole('alert')).toBeTruthy();
  expect(screen.queryByText('You have not applied for certification yet.')).toBeNull();
  expect(screen.getByRole('button', { name: 'Retry' })).toBeTruthy();
});

it.each(['Trainer', 'Creator'])('lets an uncertified %s open My Account without protected requests', async (role) => {
  const fetch = renderWorkspace(role, `/${role.toLowerCase()}/account`);
  expect(await screen.findByRole('button', { name: 'Edit Profile' })).toBeTruthy();
  await waitFor(() => expect(fetch.mock.calls.some(([url]) => /^\/api\/(trainer|creator)\//.test(url))).toBe(false));
  expect(screen.queryByRole('alert')).toBeNull();
});

it.each(['Trainer', 'Creator'])('allows an uncertified %s to save a profile change', async (role) => {
  const fetch = renderWorkspace(role, `/${role.toLowerCase()}/account`);
  fireEvent.click(await screen.findByRole('button', { name: 'Edit Profile' }));
  const dialog = screen.getByRole('dialog', { name: 'Edit Profile' });
  fireEvent.change(within(dialog).getByLabelText('Display name'), { target: { value: 'New display name' } });
  fireEvent.click(within(dialog).getByRole('button', { name: 'Save Changes' }));
  await waitFor(() => expect(screen.queryByRole('dialog', { name: 'Edit Profile' })).toBeNull());
  const update = fetch.mock.calls.find(([url, options]) => url === '/api/users/1' && options?.method === 'PUT');
  expect(JSON.parse(update[1].body).displayName).toBe('New display name');
  expect(screen.getAllByText(/New display name/).length).toBeGreaterThan(0);
});

it.each(['Trainer', 'Creator'])('opens the %s workspace after checking approval', async (role) => {
  renderWorkspace(role, `/${role.toLowerCase()}/certification`, [{ requestedRole: role, status: 'Approved' }], true);
  fireEvent.click(await screen.findByRole('button', { name: 'Check approval' }));
  expect(await screen.findByRole('heading', { name: 'Welcome back, Demo.' })).toBeTruthy();
  fireEvent.click(screen.getByRole('link', { name: role === 'Trainer' ? 'Course' : 'Courses' }));
  expect(screen.queryByRole('dialog', { name: 'Certification required' })).toBeNull();
});

it('keeps the certification form inside the workspace', async () => {
  renderWorkspace('Creator', '/creator/certification');
  expect(await screen.findByRole('heading', { name: 'Creator certification' })).toBeTruthy();
  expect(screen.getByRole('navigation', { name: 'creator navigation' })).toBeTruthy();
  expect(screen.getByRole('button', { name: 'Check approval' })).toBeTruthy();
  expect(screen.queryByRole('button', { name: 'Sign out' })).toBeNull();
  fireEvent.click(screen.getByRole('link', { name: 'My Account' }));
  expect(await screen.findByRole('button', { name: 'Edit Profile' })).toBeTruthy();
});

it('shows the certification prompt when sign-in returns an uncertified Creator to a restricted page', async () => {
  const user = { id: 1, fullName: 'Demo User', activeRole: 'Creator', roles: ['Creator'], certifiedAt: null };
  const fetch = vi.fn(async (url) => {
    if (url === '/api/auth/available-roles') return json({ roles: ['Creator'] });
    if (url === '/api/auth/login') return json({ accessToken: 'token', user });
    if (url === '/api/auth/me') return json(user);
    if (url === '/api/role-requests/my') return json([]);
    return json([]);
  });
  vi.stubGlobal('fetch', fetch);
  render(<MemoryRouter initialEntries={['/creator/courses/new']}><AuthProvider><AppRouter /></AuthProvider></MemoryRouter>);
  fireEvent.change(screen.getByLabelText('Email'), { target: { value: 'creator@example.test' } });
  fireEvent.change(screen.getByLabelText('Password'), { target: { value: 'StrongPass123!' } });
  fireEvent.click(screen.getByRole('button', { name: 'Sign in' }));
  expect(await screen.findByRole('dialog', { name: 'Certification required' })).toBeTruthy();
  expect(screen.getByRole('heading', { name: 'Welcome to your Creator workspace' })).toBeTruthy();
  expect(fetch.mock.calls.some(([url]) => url.startsWith('/api/creator/'))).toBe(false);
});
