import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter, Route, Routes, useLocation, useNavigate } from 'react-router-dom';
import { AuthContext } from '../auth/AuthContext';
import RoleShell from './RoleShell';
import MemberShell from '../components/MemberShell';

vi.mock('../components/Logo', () => ({ default: () => <span>Logo</span> }));
vi.mock('../components/WorkspaceSwitcher', () => ({ default: () => <span>Workspace</span> }));
vi.mock('../components/UserAvatar', () => ({ default: () => <span>Avatar</span> }));
vi.mock('../components/MemberNotifications', () => ({ default: () => null }));
vi.mock('../components/MemberNotificationsProvider', () => ({ MemberNotificationsProvider: ({ children }) => children }));

function LocationStateProbe({ next }) {
  const location = useLocation();
  const navigate = useNavigate();
  return (
    <>
      <output aria-label="location state">{location.state?.accessNotice || ''}</output>
      <button type="button" onClick={() => navigate(next)}>Navigate</button>
    </>
  );
}

const auth = {
  token: 'token',
  user: { fullName: 'Demo User', roles: ['Trainer'], activeRole: 'Trainer' },
  logout: vi.fn(),
};

afterEach(() => cleanup());

function renderRoleShell(role, initialPath, nextPath) {
  return render(
    <MemoryRouter initialEntries={[{ pathname: initialPath, state: { accessNotice: 'This page requires the Trainer workspace.' } }]}>
      <AuthContext.Provider value={auth}>
        <Routes>
          <Route element={<RoleShell role={role} />}>
            <Route path={initialPath} element={<LocationStateProbe next={nextPath} />} />
            <Route path={nextPath} element={<div>Next page</div>} />
          </Route>
        </Routes>
      </AuthContext.Provider>
    </MemoryRouter>,
  );
}

function renderMemberShell(initialPath, nextPath) {
  return render(
    <MemoryRouter initialEntries={[{ pathname: initialPath, state: { accessNotice: 'This page requires the Trainer workspace.' } }]}>
      <AuthContext.Provider value={auth}>
        <Routes>
          <Route path={initialPath} element={<MemberShell title="Member home"><LocationStateProbe next={nextPath} /></MemberShell>} />
          <Route path={nextPath} element={<MemberShell title="Member home"><div>Next page</div></MemberShell>} />
        </Routes>
      </AuthContext.Provider>
    </MemoryRouter>,
  );
}

it.each([
  ['trainer', '/trainer/home', '/trainer/courses'],
  ['creator', '/creator/home', '/creator/courses'],
])('shows a role-boundary notice once in the %s shell and consumes route state', async (role, initialPath, nextPath) => {
  renderRoleShell(role, initialPath, nextPath);

  expect((await screen.findByRole('status', { name: 'Access notice' })).textContent).toMatch(/requires the Trainer workspace/);
  await waitFor(() => expect(screen.getByLabelText('location state').textContent).toBe(''));
  fireEvent.click(screen.getByRole('button', { name: 'Navigate' }));
  await waitFor(() => expect(screen.queryByRole('status', { name: 'Access notice' })).toBeNull());
});

it('shows and consumes the same notice in the Member shell', async () => {
  renderMemberShell('/member/home', '/member/courses');

  expect((await screen.findByRole('status', { name: 'Access notice' })).textContent).toMatch(/requires the Trainer workspace/);
  await waitFor(() => expect(screen.getByLabelText('location state').textContent).toBe(''));
  fireEvent.click(screen.getByRole('button', { name: 'Navigate' }));
  await waitFor(() => expect(screen.queryByRole('status', { name: 'Access notice' })).toBeNull());
});

it.each([
  ['/creator/courses', 'Courses'],
  ['/creator/courses/17', 'Courses'],
  ['/creator/courses/new', 'Create Course'],
  ['/creator/courses/intake-applications', 'Session approvals'],
  ['/creator/courses/intake-applications/7', 'Session approvals'],
])('highlights only %s in Creator navigation for %s', (path, label) => {
  render(
    <MemoryRouter initialEntries={[path]}>
      <AuthContext.Provider value={auth}>
        <Routes>
          <Route element={<RoleShell role="creator" />}>
            <Route path="*" element={<div>Workspace</div>} />
          </Route>
        </Routes>
      </AuthContext.Provider>
    </MemoryRouter>,
  );
  const current = screen.getByRole('link', { name: label });
  expect(current.className).toMatch(/active/);
  for (const name of ['Courses', 'Create Course', 'Session approvals']) {
    if (name === label) continue;
    expect(screen.getByRole('link', { name }).className).not.toMatch(/active/);
  }
});
