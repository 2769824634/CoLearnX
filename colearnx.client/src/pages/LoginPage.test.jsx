import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { AuthContext } from '../auth/AuthContext';
import { authApi } from '../api';
import LoginPage from './LoginPage';

vi.mock('../api', () => ({ authApi: { availableRoles: vi.fn(), resendVerification: vi.fn() } }));

afterEach(() => { cleanup(); vi.clearAllMocks(); });

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

it('lets the user reveal and hide the login password without changing focus', () => {
  const auth = {
    booting: false,
    isAuthenticated: false,
    activeRole: null,
    login: vi.fn(),
  };
  render(<MemoryRouter><AuthContext.Provider value={auth}><LoginPage /></AuthContext.Provider></MemoryRouter>);
  const password = screen.getByLabelText('Password');
  expect(password.type).toBe('password');
  fireEvent.change(password, { target: { value: 'StrongPass123!' } });
  fireEvent.click(screen.getByRole('button', { name: 'Show password' }));
  expect(password.type).toBe('text');
  expect(password.value).toBe('StrongPass123!');
  fireEvent.click(screen.getByRole('button', { name: 'Hide password' }));
  expect(password.type).toBe('password');
});
