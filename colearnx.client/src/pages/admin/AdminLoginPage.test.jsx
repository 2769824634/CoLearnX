import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import AdminLoginPage from './AdminLoginPage';

const mocks = vi.hoisted(() => ({ login: vi.fn() }));
vi.mock('../../auth/useAdminAuth', () => ({ default: () => ({ login: mocks.login, isAuthenticated: false, booting: false }) }));

afterEach(() => { cleanup(); mocks.login.mockReset(); });

it('keeps the independent administrator sign-in empty and does not publish credentials', () => {
  render(<MemoryRouter><AdminLoginPage /></MemoryRouter>);
  expect(screen.getByLabelText('Administrator email').value).toBe('');
  expect(screen.getByLabelText('Password').value).toBe('');
  expect(screen.queryByText(/Demo:/i)).toBeNull();
  expect(screen.getByText(/separate identity boundary/i)).toBeTruthy();
});
