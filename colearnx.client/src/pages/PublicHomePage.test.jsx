import { afterEach, expect, it } from 'vitest';
import { cleanup, render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { AuthContext } from '../auth/AuthContext';
import PublicHomePage from './PublicHomePage';

afterEach(() => cleanup());

function mount(auth) {
  return render(
    <MemoryRouter>
      <AuthContext.Provider value={auth}>
        <PublicHomePage />
      </AuthContext.Provider>
    </MemoryRouter>,
  );
}

it('shows the public home with sign-in and registration, not a login form', () => {
  mount({ booting: false, isAuthenticated: false, activeRole: null });
  expect(screen.getByRole('heading', { name: 'CoLearnX' })).toBeTruthy();
  expect(screen.getByRole('link', { name: 'Sign in' }).getAttribute('href')).toBe('/login');
  expect(screen.getByRole('link', { name: 'Create an account' }).getAttribute('href')).toBe('/register');
  expect(screen.queryByLabelText('Email')).toBeNull();
  expect(screen.queryByLabelText('Password')).toBeNull();
});

it('lets a signed-in member continue into their workspace from the public home', () => {
  mount({ booting: false, isAuthenticated: true, activeRole: 'member' });
  expect(screen.getByRole('link', { name: 'Continue to workspace' }).getAttribute('href')).toBe('/member/home');
  expect(screen.queryByRole('link', { name: 'Sign in' })).toBeNull();
});
