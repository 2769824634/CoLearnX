import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
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

it('introduces the learning path and workspaces beside the unchanged sign-in form', () => {
  const auth = { booting: false, isAuthenticated: false, activeRole: null, login: vi.fn() };
  const view = render(<MemoryRouter><AuthContext.Provider value={auth}><LoginPage /></AuthContext.Provider></MemoryRouter>);
  const steps = [...view.container.querySelectorAll('.login-path li strong')].map((item) => item.textContent);
  expect(steps).toEqual(['Choose a course', 'Learn with your trainer', 'Earn your certificate']);
  const workspaces = [...view.container.querySelectorAll('.login-workspaces [data-role]')];
  expect(workspaces.map((item) => item.dataset.role)).toEqual(['member', 'trainer', 'creator']);
  expect(screen.getByRole('button', { name: 'Show password' })).toBeTruthy();
  expect(screen.getByRole('heading', { name: 'Login to your account' })).toBeTruthy();
  expect(screen.getByRole('link', { name: 'Forgot password?' }).getAttribute('href')).toBe('/forgot-password');
  expect(screen.getByRole('link', { name: 'Create an account' }).getAttribute('href')).toBe('/register');
  expect(screen.getByRole('link', { name: 'Back to home' }).getAttribute('href')).toBe('/');
});

it('tags each identity option with its role so the selection follows the role colour', async () => {
  const auth = { booting: false, isAuthenticated: false, activeRole: null, login: vi.fn() };
  authApi.availableRoles.mockResolvedValue({ roles: ['Creator', 'Member', 'Trainer'] });
  const view = render(<MemoryRouter><AuthContext.Provider value={auth}><LoginPage /></AuthContext.Provider></MemoryRouter>);
  fireEvent.change(screen.getAllByRole('textbox')[0], { target: { value: 'multi@example.test' } });
  fireEvent.change(view.container.querySelector('input[type="password"]'), { target: { value: 'StrongPass123!' } });
  fireEvent.click(screen.getByRole('button', { name: 'Sign in' }));
  const dialog = await screen.findByRole('dialog', { name: 'Continue as' });
  const options = [...dialog.querySelectorAll('.role-option')];
  expect(options.map((option) => option.dataset.role)).toEqual(['creator', 'member', 'trainer']);
});
