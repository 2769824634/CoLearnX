import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import useStaffAuth from '../../auth/useStaffAuth';
import StaffLoginPage from './StaffLoginPage';

vi.mock('../../auth/useStaffAuth');

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

it('uses the separate staff sign-in flow', async () => {
  const login = vi.fn().mockResolvedValue({ displayName: 'CoLearnX 讲师 001' });
  useStaffAuth.mockReturnValue({ login, isAuthenticated: false, booting: false });
  render(<MemoryRouter><StaffLoginPage /></MemoryRouter>);

  fireEvent.change(screen.getByLabelText('Staff email'), { target: { value: 'staff@example.com' } });
  fireEvent.change(screen.getByLabelText('Password'), { target: { value: 'Password123!' } });
  fireEvent.click(screen.getByRole('button', { name: 'Enter staff workspace' }));

  await waitFor(() => expect(login).toHaveBeenCalledWith('staff@example.com', 'Password123!'));
});

it('shows a clear login failure when the password is incorrect', async () => {
  const login = vi.fn().mockRejectedValue(new Error('Invalid email or password.'));
  useStaffAuth.mockReturnValue({ login, isAuthenticated: false, booting: false });
  render(<MemoryRouter><StaffLoginPage /></MemoryRouter>);

  fireEvent.change(screen.getByLabelText('Staff email'), { target: { value: 'staff@example.com' } });
  fireEvent.change(screen.getByLabelText('Password'), { target: { value: 'WrongPassword123!' } });
  fireEvent.click(screen.getByRole('button', { name: 'Enter staff workspace' }));

  const alert = await screen.findByRole('alert');
  expect(screen.getByRole('button', { name: 'Enter staff workspace' }).closest('form').classList.contains('staff-login-form')).toBe(true);
  expect(alert.textContent).toContain('Login failed');
  expect(alert.textContent).toContain('Invalid email or password.');
});
