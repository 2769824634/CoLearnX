import { afterEach, expect, it } from 'vitest';
import { cleanup, render } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { AuthContext } from './auth/AuthContext';
import AdminAuthContext from './auth/AdminAuthContext';
import { SITE_TITLE } from './siteTitle';
import MemberShell from './components/MemberShell';
import { AdminRoleShell } from './layouts/RoleShell';
import { CreatorHeader } from './pages/creator/CreatorUi';
import { TrainerHeader } from './pages/trainer/TrainerUi';
import AdminLoginPage from './pages/admin/AdminLoginPage';

afterEach(() => {
  cleanup();
  document.title = SITE_TITLE;
});

const memberAuth = {
  token: 'token',
  user: { fullName: 'Demo User', roles: ['Member'], activeRole: 'Member' },
  logout: () => {},
};

it('keeps the browser tab on CoLearnX in signed-in workspaces', () => {
  document.title = SITE_TITLE;
  render(
    <MemoryRouter>
      <AuthContext.Provider value={memberAuth}>
        <MemberShell title="Member Dashboard">Home</MemberShell>
      </AuthContext.Provider>
    </MemoryRouter>,
  );
  expect(document.title).toBe('CoLearnX');

  cleanup();
  render(<TrainerHeader title="Trainer home">Workspace</TrainerHeader>);
  expect(document.title).toBe('CoLearnX');

  cleanup();
  render(<CreatorHeader title="Create Course">Workspace</CreatorHeader>);
  expect(document.title).toBe('CoLearnX');

  cleanup();
  render(
    <MemoryRouter>
      <AdminAuthContext.Provider value={{ admin: { email: 'admin@colearnx.com' }, logout: () => {} }}>
        <AdminRoleShell />
      </AdminAuthContext.Provider>
    </MemoryRouter>,
  );
  expect(document.title).toBe('CoLearnX');
});

it('keeps the administrator sign-in tab on CoLearnX', () => {
  document.title = SITE_TITLE;
  render(
    <MemoryRouter>
      <AdminAuthContext.Provider value={{ booting: false, isAuthenticated: false, login: () => {} }}>
        <AdminLoginPage />
      </AdminAuthContext.Provider>
    </MemoryRouter>,
  );
  expect(document.title).toBe('CoLearnX');
});
