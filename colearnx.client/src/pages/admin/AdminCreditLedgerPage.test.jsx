import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { cleanup, render, screen, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import AdminCreditLedgerPage from './AdminCreditLedgerPage';

const api = vi.hoisted(() => ({ users: vi.fn(), user: vi.fn(), ledger: vi.fn(), adjustCredits: vi.fn() }));
vi.mock('../../auth/useAdminAuth', () => ({ default: () => ({ token: 'ledger-test' }) }));
vi.mock('../../api/adminLaterPhase', () => ({ adminLaterPhaseApi: api }));
afterEach(cleanup);
beforeEach(() => vi.clearAllMocks());

it('lists only Member accounts for a manual credit adjustment', async () => {
  api.ledger.mockResolvedValue([]);
  api.users.mockResolvedValue([
    { id: 1, fullName: 'Mia Member', email: 'mia@example.test', roles: ['Member'], creditBalance: 10, heldCredits: 0 },
    { id: 2, fullName: 'Tom Trainer', email: 'tom@example.test', roles: ['Trainer'], creditBalance: 0, heldCredits: 0 },
    { id: 3, fullName: 'Cara Creator', email: 'cara@example.test', roles: ['Creator'], creditBalance: 0, heldCredits: 0 },
  ]);
  render(<MemoryRouter><AdminCreditLedgerPage /></MemoryRouter>);

  const account = await screen.findByLabelText('Account');
  await within(account).findByRole('option', { name: /Mia Member/ });
  expect(within(account).queryByRole('option', { name: /Tom Trainer/ })).toBeNull();
  expect(within(account).queryByRole('option', { name: /Cara Creator/ })).toBeNull();
  expect(screen.getByText(/Only Member accounts hold credits/)).toBeTruthy();
});
