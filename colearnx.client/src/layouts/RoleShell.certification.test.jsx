import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, within } from '@testing-library/react';
import { MemoryRouter, Navigate, Route, Routes, useLocation } from 'react-router-dom';
import { AuthContext } from '../auth/AuthContext';
import RoleShell from './RoleShell';

vi.mock('../components/Logo', () => ({ default: () => <span>Logo</span> }));
vi.mock('../components/UserAvatar', () => ({ default: () => <span>Avatar</span> }));
vi.mock('../components/MemberNotifications', () => ({ default: () => null }));
vi.mock('../components/MemberNotificationsProvider', () => ({ MemberNotificationsProvider: ({ children }) => children }));

afterEach(() => cleanup());

function PathProbe() {
  const { pathname } = useLocation();
  return <output aria-label="current path">{pathname}</output>;
}

function renderWorkspace(role, path, certifiedAt = null) {
  const user = { fullName: 'Demo User', roles: [role], activeRole: role, certifiedAt };
  render(
    <MemoryRouter initialEntries={[path]}>
      <AuthContext.Provider value={{ token: 'token', user, logout: vi.fn() }}>
        <PathProbe />
        <Routes>
          <Route path={`/${role}`} element={<RoleShell role={role} />}>
            <Route index element={<Navigate to="home" replace />} />
            <Route path="home" element={<div>Workspace home</div>} />
            <Route path="courses" element={<div>Courses page</div>} />
            <Route path="courses/new" element={<div>Create course page</div>} />
            <Route path="account" element={<div>Account page</div>} />
            <Route path="certification" element={<div>Certification page</div>} />
          </Route>
        </Routes>
      </AuthContext.Provider>
    </MemoryRouter>,
  );
}

it.each([['trainer', 'Course'], ['creator', 'Courses'], ['trainer', 'Earnings'], ['creator', 'Earnings']])(
  'blocks the %s %s navigation without changing the address',
  (role, label) => {
    renderWorkspace(role, `/${role}/home`);
    fireEvent.click(screen.getByRole('link', { name: label }));
    expect(screen.getByLabelText('current path').textContent).toBe(`/${role}/home`);
    expect(screen.getByRole('dialog', { name: 'Certification required' })).toBeTruthy();
    fireEvent.click(screen.getByRole('button', { name: 'Close' }));
    expect(screen.queryByRole('dialog', { name: 'Certification required' })).toBeNull();
    fireEvent.click(screen.getByRole('link', { name: label }));
    expect(screen.getByRole('dialog', { name: 'Certification required' })).toBeTruthy();
  },
);

it.each(['trainer', 'creator'])('lets an uncertified %s open My Account', (role) => {
  renderWorkspace(role, `/${role}/home`);
  fireEvent.click(screen.getByRole('link', { name: 'My Account' }));
  expect(screen.getByLabelText('current path').textContent).toBe(`/${role}/account`);
  expect(screen.getByText('Account page')).toBeTruthy();
  expect(screen.queryByRole('dialog', { name: 'Certification required' })).toBeNull();
});

it('redirects a direct restricted address to home and offers certification', () => {
  renderWorkspace('creator', '/creator/courses/new');
  expect(screen.getByLabelText('current path').textContent).toBe('/creator/home');
  expect(screen.queryByText('Create course page')).toBeNull();
  const modal = screen.getByRole('dialog', { name: 'Certification required' });
  fireEvent.click(within(modal).getByRole('link', { name: 'Start certification' }));
  expect(screen.getByLabelText('current path').textContent).toBe('/creator/certification');
  expect(screen.getByText('Certification page')).toBeTruthy();
  expect(screen.queryByRole('dialog', { name: 'Certification required' })).toBeNull();
});

it('takes the workspace root to home without showing a certification prompt', () => {
  renderWorkspace('creator', '/creator');
  expect(screen.getByLabelText('current path').textContent).toBe('/creator/home');
  expect(screen.queryByRole('dialog', { name: 'Certification required' })).toBeNull();
});

it('clears a direct-link prompt when dismissed and allows it to be opened again', () => {
  renderWorkspace('creator', '/creator/courses/new');
  expect(screen.getByRole('dialog', { name: 'Certification required' })).toBeTruthy();
  fireEvent.keyDown(document, { key: 'Escape' });
  expect(screen.queryByRole('dialog', { name: 'Certification required' })).toBeNull();
  fireEvent.click(screen.getByRole('link', { name: 'Home' }));
  expect(screen.queryByRole('dialog', { name: 'Certification required' })).toBeNull();
  fireEvent.click(screen.getByRole('link', { name: 'Courses' }));
  const modal = screen.getByRole('dialog', { name: 'Certification required' });
  fireEvent.click(within(modal).getByRole('link', { name: 'Edit my profile' }));
  expect(screen.getByLabelText('current path').textContent).toBe('/creator/account');
  expect(screen.queryByRole('dialog', { name: 'Certification required' })).toBeNull();
});

it('allows a certified Creator to navigate normally', () => {
  renderWorkspace('creator', '/creator/home', '2026-11-12T00:00:00Z');
  fireEvent.click(screen.getByRole('link', { name: 'Courses' }));
  expect(screen.getByLabelText('current path').textContent).toBe('/creator/courses');
  expect(screen.getByText('Courses page')).toBeTruthy();
  expect(screen.queryByRole('dialog', { name: 'Certification required' })).toBeNull();
});
