import { afterEach, expect, it } from 'vitest';
import { cleanup, render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { AuthContext } from '../auth/AuthContext';
import AdminAuthContext from '../auth/AdminAuthContext';
import AppRouter from './AppRouter';

afterEach(() => cleanup());

function mount(path, auth = { booting: false, isAuthenticated: false, activeRole: null }) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <AuthContext.Provider value={auth}>
        <AdminAuthContext.Provider value={{ booting: false, isAuthenticated: false }}>
          <AppRouter />
        </AdminAuthContext.Provider>
      </AuthContext.Provider>
    </MemoryRouter>,
  );
}

it('opens the public home at the site root instead of the login form', () => {
  mount('/');
  expect(screen.getByRole('heading', { name: 'CoLearnX' })).toBeTruthy();
  expect(screen.queryByRole('heading', { name: 'Login to your account' })).toBeNull();
});

it('keeps an explicit login URL on the sign-in page', () => {
  mount('/login');
  expect(screen.getByRole('heading', { name: 'Login to your account' })).toBeTruthy();
});
