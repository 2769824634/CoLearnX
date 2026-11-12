import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { AuthContext } from '../auth/AuthContext';
import RegisterPage from './RegisterPage';
import PublicSiteShell from '../components/PublicSiteShell';

afterEach(cleanup);

const trainer = (logout = vi.fn()) => ({
  token: 'token', booting: false, isAuthenticated: true, activeRole: 'trainer', logout, register: vi.fn(),
  user: { fullName: 'Tessa Trainer', email: 'tessa@example.test', activeRole: 'Trainer', roles: ['Trainer'], certifiedAt: null },
});
const guest = { token: null, user: null, booting: false, isAuthenticated: false, activeRole: null, register: vi.fn(), logout: vi.fn() };

function renderRegister(auth) {
  return render(
    <MemoryRouter initialEntries={['/register?role=Creator']}>
      <AuthContext.Provider value={auth}>
        <Routes>
          <Route path="/register" element={<RegisterPage />} />
          <Route path="/trainer/home" element={<div>Trainer home</div>} />
          <Route path="/trainer/certification" element={<div>Certification upload</div>} />
        </Routes>
      </AuthContext.Provider>
    </MemoryRouter>,
  );
}

it('tells a signed-in account why it cannot sign up again instead of sending it to another page', () => {
  renderRegister(trainer());

  expect(screen.getByRole('heading', { name: 'You are already signed in' })).toBeTruthy();
  expect(screen.getByText(/Tessa Trainer/)).toBeTruthy();
  expect(screen.getByText(/Trainer account/)).toBeTruthy();
  expect(screen.getByRole('link', { name: 'Go to my workspace' }).getAttribute('href')).toBe('/trainer/home');
  expect(screen.queryByText('Certification upload')).toBeNull();
  expect(screen.queryByText('Trainer home')).toBeNull();
  expect(screen.queryByLabelText('Full name')).toBeNull();
});

it('lets the signed-in person sign out and then shows the sign-up form', () => {
  const logout = vi.fn();
  const view = renderRegister(trainer(logout));

  fireEvent.click(screen.getByRole('button', { name: 'Sign out and create another account' }));
  expect(logout).toHaveBeenCalledTimes(1);

  view.unmount();
  renderRegister(guest);
  expect(screen.getByRole('heading', { name: 'Create your account' })).toBeTruthy();
});

function renderShell(auth) {
  return render(
    <MemoryRouter initialEntries={['/']}>
      <AuthContext.Provider value={auth}>
        <PublicSiteShell><div>Public content</div></PublicSiteShell>
      </AuthContext.Provider>
    </MemoryRouter>,
  );
}

it('shows the workspace link and sign out in the public header for a signed-in Trainer or Creator', () => {
  const logout = vi.fn();
  renderShell(trainer(logout));

  expect(screen.getByRole('link', { name: 'My workspace' }).getAttribute('href')).toBe('/trainer/home');
  expect(screen.queryByRole('link', { name: 'Sign up' })).toBeNull();
  expect(screen.queryByRole('link', { name: 'Log in' })).toBeNull();
  fireEvent.click(screen.getByRole('button', { name: 'Sign out' }));
  expect(logout).toHaveBeenCalledTimes(1);
});

it('keeps Log in and Sign up in the public header for a guest', () => {
  renderShell(guest);
  expect(screen.getByRole('link', { name: 'Log in' }).getAttribute('href')).toBe('/login');
  expect(screen.getByRole('link', { name: 'Sign up' }).getAttribute('href')).toBe('/register');
  expect(screen.queryByRole('link', { name: 'My workspace' })).toBeNull();
});
