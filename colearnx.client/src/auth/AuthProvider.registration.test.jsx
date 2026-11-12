import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { AuthProvider } from './AuthProvider';
import { RequireAuth } from './RequireAuth';
import RegisterPage from '../pages/RegisterPage';

afterEach(() => { cleanup(); localStorage.clear(); vi.unstubAllGlobals(); });

function json(data, status = 200) {
  return new Response(JSON.stringify(data), { status, headers: { 'Content-Type': 'application/json' } });
}

function renderRegistration(role, { verificationRequired = false, loginFails = false } = {}) {
  const user = { id: 1, activeRole: role, roles: [role], certifiedAt: null };
  const fetch = vi.fn(async (url) => {
    if (url === '/api/auth/register') return json({ accessToken: '', user, emailVerificationRequired: verificationRequired });
    if (url === '/api/auth/login') return loginFails ? json({ message: 'Sign-in unavailable' }, 500) : json({ accessToken: 'new-token', user });
    if (url === '/api/auth/me') return json(user);
    return json({});
  });
  vi.stubGlobal('fetch', fetch);
  render(
    <MemoryRouter initialEntries={[`/register?role=${role}`]}>
      <AuthProvider>
        <Routes>
          <Route path="/register" element={<RegisterPage />} />
          <Route path="/login" element={<div>Sign-in page</div>} />
          <Route element={<RequireAuth role={role.toLowerCase()} />}>
            <Route path={`/${role.toLowerCase()}/home`} element={<div>Workspace home</div>} />
          </Route>
        </Routes>
      </AuthProvider>
    </MemoryRouter>,
  );
  fireEvent.change(screen.getByLabelText('Full name'), { target: { value: 'New User' } });
  fireEvent.change(screen.getByLabelText('Email'), { target: { value: 'new.user@example.test' } });
  fireEvent.change(screen.getByLabelText('Password'), { target: { value: 'StrongPass123!' } });
  fireEvent.change(screen.getByLabelText('Confirm password'), { target: { value: 'StrongPass123!' } });
  fireEvent.click(screen.getByRole('button', { name: 'Create account' }));
  return fetch;
}

it.each(['Trainer', 'Creator'])('signs in a newly registered %s when verification is not required', async (role) => {
  const fetch = renderRegistration(role);
  expect(await screen.findByText('Workspace home')).toBeTruthy();
  expect(fetch.mock.calls.filter(([url]) => url === '/api/auth/login')).toHaveLength(1);
});

it('does not sign in before required email verification', async () => {
  const fetch = renderRegistration('Trainer', { verificationRequired: true });
  expect(await screen.findByRole('heading', { name: 'Check your email' })).toBeTruthy();
  expect(fetch.mock.calls.some(([url]) => url === '/api/auth/login')).toBe(false);
  expect(screen.queryByText('Workspace home')).toBeNull();
});

it('explains that the account was created if automatic sign-in fails', async () => {
  renderRegistration('Creator', { loginFails: true });
  expect(await screen.findByRole('heading', { name: 'Account created' })).toBeTruthy();
  expect(screen.getByRole('link', { name: 'Sign in' }).getAttribute('href')).toBe('/login');
});
