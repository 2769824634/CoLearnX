import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { authApi } from '../api';
import ForgotPasswordPage from './ForgotPasswordPage';
import ResetPasswordPage from './ResetPasswordPage';

vi.mock('../api', () => ({ authApi: { forgotPassword: vi.fn(), resetPassword: vi.fn() } }));
afterEach(() => { cleanup(); vi.clearAllMocks(); window.history.replaceState({}, '', '/'); });
it('requests reset and shows the same privacy-preserving response', async () => {
  authApi.forgotPassword.mockResolvedValue({ message: 'If the account is eligible, a password reset link will be sent.' });
  render(<MemoryRouter><ForgotPasswordPage /></MemoryRouter>);
  fireEvent.change(screen.getByLabelText('Email'), { target: { value: 'learner@example.test' } });
  fireEvent.click(screen.getByRole('button', { name: 'Send reset link' }));
  expect(await screen.findByText('If the account is eligible, a password reset link will be sent.')).toBeTruthy();
});
it('removes the token from the address and consumes it only after matching passwords', async () => {
  window.history.replaceState({}, '', '/reset-password#token=one-time-token');
  authApi.resetPassword.mockResolvedValue({ message: 'Password updated. Sign in with your new password.' });
  render(<MemoryRouter><ResetPasswordPage /></MemoryRouter>);
  expect(window.location.hash).toBe('');
  fireEvent.change(screen.getByLabelText('New password'), { target: { value: 'StrongPass123!' } });
  fireEvent.change(screen.getByLabelText('Confirm password'), { target: { value: 'WrongPass123!' } });
  fireEvent.click(screen.getByRole('button', { name: 'Reset password' }));
  expect(await screen.findByText('Passwords do not match.')).toBeTruthy();
  expect(authApi.resetPassword).not.toHaveBeenCalled();
  fireEvent.change(screen.getByLabelText('Confirm password'), { target: { value: 'StrongPass123!' } });
  fireEvent.click(screen.getByRole('button', { name: 'Reset password' }));
  expect(await screen.findByText('Password updated. Sign in with your new password.')).toBeTruthy();
  expect(authApi.resetPassword).toHaveBeenCalledWith('one-time-token', 'StrongPass123!');
});
it('explains missing credentials and provides a fresh-link route', () => {
  render(<MemoryRouter><ResetPasswordPage /></MemoryRouter>);
  expect(screen.getByText(/This reset link is missing or invalid/)).toBeTruthy();
  expect(screen.getByRole('link', { name: 'Request a new reset link' }).getAttribute('href')).toBe('/forgot-password');
});
