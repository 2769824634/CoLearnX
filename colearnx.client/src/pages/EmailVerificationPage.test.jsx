import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { StrictMode } from 'react';
import { authApi } from '../api';
import AppRouter from '../routes/AppRouter';
import { AuthContext } from '../auth/AuthContext';

vi.mock('../api', () => ({ authApi: { verifyEmail: vi.fn() } }));

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
  window.history.replaceState({}, '', '/');
});

it('consumes the email token, removes it from the address, and offers sign in', async () => {
  window.history.replaceState({}, '', '/verify-email#token=one-time-token');
  authApi.verifyEmail.mockResolvedValue({ message: 'Email verified. You can now sign in.' });

  render(
    <StrictMode>
      <MemoryRouter initialEntries={['/verify-email']}>
        <AuthContext.Provider value={{ booting: false, isAuthenticated: false, activeRole: null }}>
          <AppRouter />
        </AuthContext.Provider>
      </MemoryRouter>
    </StrictMode>,
  );

  expect(await screen.findByText('Email verified. You can now sign in.')).toBeTruthy();
  expect(authApi.verifyEmail).toHaveBeenCalledWith('one-time-token');
  expect(window.location.hash).toBe('');
  expect(screen.getByRole('link', { name: 'Sign in' }).getAttribute('href')).toBe('/login');
});
