import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { AuthContext } from '../auth/AuthContext';
import { authApi } from '../api';
import LoginPage from './LoginPage';

vi.mock('../api', () => ({ authApi: { availableRoles: vi.fn(), resendVerification: vi.fn() } }));

afterEach(() => { cleanup(); vi.clearAllMocks(); });

it('keeps the public login empty and does not ask the browser to autofill credentials', () => {
  render(<MemoryRouter><AuthContext.Provider value={{ booting: false, isAuthenticated: false, activeRole: null, login: vi.fn() }}><LoginPage /></AuthContext.Provider></MemoryRouter>);
  const email = screen.getByLabelText('Email');
  const password = document.querySelector('#login-password');
  expect(email.value).toBe('');
  expect(password.value).toBe('');
  expect(email.getAttribute('autocomplete')).toBe('off');
  expect(password.getAttribute('autocomplete')).toBe('off');
  expect(email.form.getAttribute('autocomplete')).toBe('off');
  expect(email.readOnly).toBe(true);
  expect(password.readOnly).toBe(true);
  expect(screen.queryByDisplayValue(/huang\.yousheng/i)).toBeNull();
  fireEvent.focus(email);
  fireEvent.focus(password);
  expect(email.readOnly).toBe(false);
  expect(password.readOnly).toBe(false);
});

it('logs a single-role account in directly without an identity chooser', async () => {
  const auth = {
    booting: false,
    isAuthenticated: false,
    activeRole: null,
    login: vi.fn().mockResolvedValue({ activeRole: 'Member' }),
  };
  authApi.availableRoles.mockResolvedValue({ roles: ['Member'] });
  const view = render(<MemoryRouter><AuthContext.Provider value={auth}><LoginPage /></AuthContext.Provider></MemoryRouter>);
  const inputs = screen.getAllByRole('textbox');
  fireEvent.change(inputs[0], { target: { value: 'member@example.test' } });
  fireEvent.change(view.container.querySelector('input[type="password"]'), { target: { value: 'StrongPass123!' } });
  fireEvent.click(screen.getByRole('button', { name: 'Sign in' }));
  await waitFor(() => expect(auth.login).toHaveBeenCalledWith('member@example.test', 'StrongPass123!', 'Member'));
  expect(screen.queryByRole('dialog', { name: 'Continue as' })).toBeNull();
});

it('introduces the learning path and one-role policy beside the sign-in form', () => {
  const auth = { booting: false, isAuthenticated: false, activeRole: null, login: vi.fn() };
  const view = render(<MemoryRouter><AuthContext.Provider value={auth}><LoginPage /></AuthContext.Provider></MemoryRouter>);
  const steps = [...view.container.querySelectorAll('.login-path li strong')].map((item) => item.textContent);
  expect(steps).toEqual(['Choose a course', 'Learn with your trainer', 'Earn your certificate']);
  expect(screen.getByText('Each account has one role: Member, Trainer or Creator.')).toBeTruthy();
  expect(screen.getByRole('button', { name: 'Show password' })).toBeTruthy();
  expect(screen.getByRole('heading', { name: 'Login to your account' })).toBeTruthy();
  expect(screen.getByRole('link', { name: 'Forgot password?' }).getAttribute('href')).toBe('/forgot-password');
  expect(screen.getByRole('link', { name: 'Create an account' }).getAttribute('href')).toBe('/register');
  expect(screen.getByRole('link', { name: 'Back to home' }).getAttribute('href')).toBe('/');
});

it('signs in with the first returned role without showing a chooser', async () => {
  const auth = { booting: false, isAuthenticated: false, activeRole: null, login: vi.fn().mockResolvedValue({ activeRole: 'Creator' }) };
  authApi.availableRoles.mockResolvedValue({ roles: ['Creator', 'Member', 'Trainer'] });
  const view = render(<MemoryRouter><AuthContext.Provider value={auth}><LoginPage /></AuthContext.Provider></MemoryRouter>);
  fireEvent.change(screen.getAllByRole('textbox')[0], { target: { value: 'multi@example.test' } });
  fireEvent.change(view.container.querySelector('input[type="password"]'), { target: { value: 'StrongPass123!' } });
  fireEvent.click(screen.getByRole('button', { name: 'Sign in' }));
  await waitFor(() => expect(auth.login).toHaveBeenCalledWith('multi@example.test', 'StrongPass123!', 'Creator'));
  expect(screen.queryByRole('dialog', { name: 'Continue as' })).toBeNull();
});

it('ignores a return path that belongs to another role and opens the account\'s own home', async () => {
  const auth = { booting: false, isAuthenticated: false, activeRole: null, login: vi.fn().mockResolvedValue({ activeRole: 'Creator' }) };
  authApi.availableRoles.mockResolvedValue({ roles: ['Creator'] });
  const view = render(
    <MemoryRouter initialEntries={[{ pathname: '/login', state: { from: { pathname: '/member/programs', search: '' } } }]}>
      <AuthContext.Provider value={auth}>
        <Routes>
          <Route path="/login" element={<LoginPage />} />
          <Route path="/creator/home" element={<div>Creator home</div>} />
          <Route path="/member/programs" element={<div>Member programs</div>} />
        </Routes>
      </AuthContext.Provider>
    </MemoryRouter>,
  );
  fireEvent.change(screen.getAllByRole('textbox')[0], { target: { value: 'creator@example.test' } });
  fireEvent.change(view.container.querySelector('input[type="password"]'), { target: { value: 'StrongPass123!' } });
  fireEvent.click(screen.getByRole('button', { name: 'Sign in' }));
  expect(await screen.findByText('Creator home')).toBeTruthy();
  expect(screen.queryByText('Member programs')).toBeNull();
});

it('still returns a Member to the page they were on before signing in', async () => {
  const auth = { booting: false, isAuthenticated: false, activeRole: null, login: vi.fn().mockResolvedValue({ activeRole: 'Member' }) };
  authApi.availableRoles.mockResolvedValue({ roles: ['Member'] });
  const view = render(
    <MemoryRouter initialEntries={[{ pathname: '/login', state: { from: { pathname: '/member/programs', search: '' } } }]}>
      <AuthContext.Provider value={auth}>
        <Routes>
          <Route path="/login" element={<LoginPage />} />
          <Route path="/member/home" element={<div>Member home</div>} />
          <Route path="/member/programs" element={<div>Member programs</div>} />
        </Routes>
      </AuthContext.Provider>
    </MemoryRouter>,
  );
  fireEvent.change(screen.getAllByRole('textbox')[0], { target: { value: 'member@example.test' } });
  fireEvent.change(view.container.querySelector('input[type="password"]'), { target: { value: 'StrongPass123!' } });
  fireEvent.click(screen.getByRole('button', { name: 'Sign in' }));
  expect(await screen.findByText('Member programs')).toBeTruthy();
});
