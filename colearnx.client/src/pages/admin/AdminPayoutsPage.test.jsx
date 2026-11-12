import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import AdminPayoutsPage from './AdminPayoutsPage';
import { AdminPendingContext } from './adminPending';

const api = vi.hoisted(() => ({ review: vi.fn(), list: vi.fn() }));
const query = vi.hoisted(() => ({ data: [{ id: 4, userId: 2, userName: 'Trainer One', userEmail: 'trainer@example.test',
  role: 'Trainer', credits: 60, payoutMethod: 'PayPal trainer@example.test', status: 'Requested',
  requestedAt: '2026-11-04T00:00:00Z' }], loading: false, error: null, refresh: vi.fn() }));
vi.mock('../../api', () => ({ adminPayoutsApi: api }));
vi.mock('../../auth/useAdminAuth', () => ({ default: () => ({ token: 'admin-token', logout: vi.fn() }) }));
vi.mock('./useAdminQuery', () => ({ default: () => query }));

afterEach(() => { cleanup(); vi.clearAllMocks(); });

it('requires a reference and refreshes the payout queue after marking paid', async () => {
  const refreshPending = vi.fn();
  api.review.mockResolvedValue({});
  render(<AdminPendingContext.Provider value={{ counts: {}, refresh: refreshPending }}><AdminPayoutsPage /></AdminPendingContext.Provider>);
  fireEvent.click(screen.getByRole('button', { name: /Trainer One/ }));
  expect(screen.getByRole('button', { name: 'Mark as paid' }).disabled).toBe(true);
  expect(screen.getByRole('button', { name: 'Reject' }).disabled).toBe(true);
  fireEvent.change(screen.getByLabelText('Transfer reference / rejection reason'), { target: { value: 'OFF-900' } });
  fireEvent.click(screen.getByRole('button', { name: 'Mark as paid' }));
  await vi.waitFor(() => expect(api.review).toHaveBeenCalledWith('admin-token', 4, 'Paid', 'OFF-900'));
  expect(query.refresh).toHaveBeenCalled();
  expect(refreshPending).toHaveBeenCalled();
});
