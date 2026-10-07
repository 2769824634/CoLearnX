import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter, Route, Routes, useLocation, useNavigate } from 'react-router-dom';
import { AuthContext } from '../auth/AuthContext';
import AdminAuthContext from '../auth/AdminAuthContext';
import RoleShell, { AdminRoleShell } from './RoleShell';
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

function PathProbe() {
  const location = useLocation();
  return <output aria-label="current path">{`${location.pathname}${location.hash}`}</output>;
}

it.each([
  ['member', () => render(
    <MemoryRouter initialEntries={['/member/home']}>
      <AuthContext.Provider value={auth}>
        <MemberShell title="Member home"><PathProbe /></MemberShell>
      </AuthContext.Provider>
    </MemoryRouter>,
  )],
  ['trainer', () => render(
    <MemoryRouter initialEntries={['/trainer/home']}>
      <AuthContext.Provider value={auth}>
        <Routes>
          <Route element={<RoleShell role="trainer" />}>
            <Route path="/trainer/home" element={<PathProbe />} />
          </Route>
        </Routes>
      </AuthContext.Provider>
    </MemoryRouter>,
  )],
])('lets keyboard users skip past the %s shell chrome without changing the route', (_role, renderShell) => {
  renderShell();

  expect(screen.getByRole('banner')).toBeTruthy();
  const main = screen.getByRole('main');
  fireEvent.click(screen.getByRole('link', { name: 'Skip to main content' }));

  expect(document.activeElement).toBe(main);
  expect(screen.getByLabelText('current path').textContent).toMatch(/^\/(member|trainer)\/home$/);
});

it.each([
  ['member', '/member/home', <MemberShell key="m" title="Home"><div /></MemberShell>],
  ['trainer', '/trainer/home', <Routes key="t"><Route element={<RoleShell role="trainer" />}><Route path="*" element={<div />} /></Route></Routes>],
  ['creator', '/creator/home', <Routes key="c"><Route element={<RoleShell role="creator" />}><Route path="*" element={<div />} /></Route></Routes>],
  ['admin', '/admin/home', <Routes key="a"><Route element={<AdminRoleShell />}><Route path="*" element={<div />} /></Route></Routes>],
])('marks the user chip with the %s workspace so the avatar ring can follow the role', (role, path, shell) => {
  const { container } = render(
    <MemoryRouter initialEntries={[path]}>
      <AdminAuthContext.Provider value={{ admin: { email: 'admin@colearnx.test' }, logout: vi.fn() }}>
        <AuthContext.Provider value={auth}>{shell}</AuthContext.Provider>
      </AdminAuthContext.Provider>
    </MemoryRouter>,
  );

  expect(container.querySelector('.user-chip').dataset.role).toBe(role);
});

function stubHorizontalNavLayout() {
  const restores = [];
  const define = (prop, get) => {
    const original = Object.getOwnPropertyDescriptor(HTMLElement.prototype, prop);
    Object.defineProperty(HTMLElement.prototype, prop, { configurable: true, get });
    restores.push(() => (original
      ? Object.defineProperty(HTMLElement.prototype, prop, original)
      : delete HTMLElement.prototype[prop]));
  };
  define('scrollWidth', function scrollWidth() { return this.tagName === 'NAV' ? 900 : 0; });
  define('clientWidth', function clientWidth() { return this.tagName === 'NAV' ? 390 : 0; });
  define('offsetWidth', function offsetWidth() { return this.classList.contains('nav-item') ? 100 : 0; });
  define('offsetLeft', function offsetLeft() { return this.classList.contains('active') ? 700 : 0; });
  const scrollTo = vi.fn();
  HTMLElement.prototype.scrollTo = scrollTo;
  restores.push(() => { delete HTMLElement.prototype.scrollTo; });
  return { scrollTo, restore: () => restores.forEach((fn) => fn()) };
}

it.each([
  ['member', '/member/account', () => <MemberShell title="Account"><div>Account</div></MemberShell>],
  ['trainer', '/trainer/account', () => (
    <Routes>
      <Route element={<RoleShell role="trainer" />}>
        <Route path="/trainer/account" element={<div>Account</div>} />
      </Route>
    </Routes>
  )],
])('scrolls the active %s nav item into view when the nav overflows sideways', (_role, path, Shell) => {
  const { scrollTo, restore } = stubHorizontalNavLayout();
  try {
    render(
      <MemoryRouter initialEntries={[path]}>
        <AuthContext.Provider value={auth}><Shell /></AuthContext.Provider>
      </MemoryRouter>,
    );

    expect(scrollTo).toHaveBeenCalledWith({ left: 555 });
    expect(scrollTo.mock.contexts[0]).toBe(screen.getByRole('navigation'));
  } finally {
    restore();
  }
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
