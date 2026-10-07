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

it('outlines the console areas and keeps the way back to user sign-in', () => {
  const view = render(<MemoryRouter><AdminLoginPage /></MemoryRouter>);
  const areas = [...view.container.querySelectorAll('.admin-console-scope li strong')].map((item) => item.textContent);
  expect(areas).toEqual(['Approvals', 'Users', 'Credit Ledger', 'Disputes', 'Audit Log']);
  expect(screen.getByRole('heading', { name: 'Operations console' })).toBeTruthy();
  expect(screen.getByRole('heading', { name: 'Sign in as administrator' })).toBeTruthy();
  expect(screen.getByRole('link', { name: 'Return to user sign-in' }).getAttribute('href')).toBe('/login');
  expect(screen.getByRole('button', { name: 'Show password' })).toBeTruthy();
});
