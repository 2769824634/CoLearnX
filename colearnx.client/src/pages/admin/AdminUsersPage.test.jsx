import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, within, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import AdminUsersPage from './AdminUsersPage';

const api = vi.hoisted(() => ({ users: vi.fn(), user: vi.fn(), ledger: vi.fn(), adjustCredits: vi.fn() }));
vi.mock('../../auth/useAdminAuth', () => ({ default: () => ({ token: 'user-details-test' }) }));
vi.mock('../../api/adminLaterPhase', () => ({ adminLaterPhaseApi: api }));
afterEach(cleanup);
beforeEach(() => vi.clearAllMocks());
const user = { id: 42, fullName: 'Example Learner', email: 'learner@example.test', roles: ['Member'], creditBalance: 60, heldCredits: 5, isActive: true, phone: '555-0042', country: 'Singapore' };
it('opens details from the whole user row and locks ledger and adjustments to that user', async () => {
  api.users.mockResolvedValue([user]);
  api.user.mockResolvedValue(user);
  api.ledger.mockResolvedValue([{ id: 1, userId: 42, userName: user.fullName, userEmail: user.email, type: 'TopUp', description: 'Personal top up', delta: 60, balanceAfter: 60, heldAfter: 0 }]);
  api.adjustCredits.mockResolvedValue({ delta: 2, balanceAfter: 62 });
  render(<MemoryRouter><AdminUsersPage /></MemoryRouter>);
  fireEvent.click((await screen.findByText('Example Learner')).closest('tr'));
  const dialog = await screen.findByRole('dialog', { name: 'User details' });
  expect(await within(dialog).findByText('555-0042')).toBeTruthy();
  expect(await within(dialog).findByText('Personal top up')).toBeTruthy();
  expect(api.ledger).toHaveBeenCalledWith('user-details-test', expect.objectContaining({ userId: 42 }), expect.any(AbortSignal));
  const account = within(dialog).getByLabelText('Account');
  await waitFor(() => expect(account.value).toBe('42'));
  expect(account.disabled).toBe(true);
  fireEvent.change(within(dialog).getByLabelText('Credit delta'), { target: { value: '2' } });
  fireEvent.change(within(dialog).getByLabelText('Reason'), { target: { value: 'Verified correction' } });
  fireEvent.click(within(dialog).getByRole('button', { name: 'Apply adjustment' }));
  await waitFor(() => expect(api.adjustCredits).toHaveBeenCalledWith('user-details-test', expect.objectContaining({ userId: 42, delta: 2, reason: 'Verified correction' })));
  expect(await within(dialog).findByText('+2 credits applied. New balance: 62 credits.')).toBeTruthy();
});
it('opens linked users directly and shows missing-user errors without displaying a ledger', async () => {
  api.users.mockResolvedValue([]);
  api.user.mockRejectedValue(Object.assign(new Error('User was not found.'), { status: 404 }));
  render(<MemoryRouter initialEntries={['/admin/users?userId=999']}><AdminUsersPage /></MemoryRouter>);
  const dialog = await screen.findByRole('dialog', { name: 'User details' });
  expect(await within(dialog).findByText('User was not found.')).toBeTruthy();
  expect(within(dialog).queryByRole('heading', { name: 'Credit ledger' })).toBeNull();
});
