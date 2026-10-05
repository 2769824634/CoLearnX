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
