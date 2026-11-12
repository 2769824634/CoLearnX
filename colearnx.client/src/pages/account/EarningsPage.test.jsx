import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import EarningsPage from './EarningsPage';

const query = vi.hoisted(() => ({ data: null, loading: false, error: null, refresh: vi.fn() }));
const api = vi.hoisted(() => ({ requestPayout: vi.fn() }));
vi.mock('../trainer/useTrainerQuery', () => ({ default: () => query }));
vi.mock('../../api', () => ({ earningsApi: { my: vi.fn(), trainer: vi.fn(), creator: vi.fn(), requestPayout: api.requestPayout } }));
vi.mock('../../auth/AuthContext', () => ({ useAuth: () => ({ token: 'role-token' }) }));

afterEach(() => { cleanup(); vi.clearAllMocks(); });

it('shows balances, class earnings, payout history and submits the payout method', async () => {
  query.data = {
    totalCredits: 100, pendingCredits: 20, availableCredits: 60, requestedCredits: 0, paidCredits: 20,
    minimumPayoutCredits: 50, cashPerCredit: 1, classes: [{ courseIntakeId: 7, courseCode: 'UX1', courseTitle: 'UX',
      endsAt: '2026-11-02T00:00:00Z', learners: 2, credits: 50,
      status: 'Available', availableAt: null, sharePercent: 25 }],
    payouts: [{ id: 3, credits: 20, payoutMethod: 'PayPal', status: 'Paid', reference: 'OFF-1',
      requestedAt: '2026-11-03T00:00:00Z', reviewedAt: '2026-11-04T00:00:00Z' }], rows: [],
  };
  api.requestPayout.mockResolvedValue({});
  render(<EarningsPage role="trainer" />);

  expect(screen.getByText('60 credits')).toBeTruthy();
  expect(screen.getByText(/≈.*60/i)).toBeTruthy();
  expect(screen.getByText('UX1 · UX')).toBeTruthy();
  expect(screen.getByText('OFF-1')).toBeTruthy();
  fireEvent.change(screen.getByLabelText('Payout method'), { target: { value: 'PayPal trainer@example.test' } });
  fireEvent.click(screen.getByRole('button', { name: 'Request payout of 60 credits' }));
  await vi.waitFor(() => expect(api.requestPayout).toHaveBeenCalledWith('PayPal trainer@example.test', 'role-token'));
});

it('disables payout below the minimum and hides the form while a request is pending', () => {
  query.data = { totalCredits: 20, pendingCredits: 0, availableCredits: 20, requestedCredits: 0, paidCredits: 0,
    minimumPayoutCredits: 50, cashPerCredit: 1, classes: [], payouts: [], rows: [] };
  const view = render(<EarningsPage role="creator" />);
  expect(screen.getByText('The minimum payout is 50 credits.')).toBeTruthy();
  expect(screen.getByRole('button', { name: 'Request payout of 20 credits' }).disabled).toBe(true);
  view.unmount();
  query.data = { ...query.data, availableCredits: 0, requestedCredits: 60,
    payouts: [{ id: 8, credits: 60, payoutMethod: 'PayPal', status: 'Requested', requestedAt: '2026-11-04T00:00:00Z' }] };
  render(<EarningsPage role="creator" />);
  expect(screen.getByText(/Payout of 60 credits requested/)).toBeTruthy();
  expect(screen.queryByLabelText('Payout method')).toBeNull();
});
