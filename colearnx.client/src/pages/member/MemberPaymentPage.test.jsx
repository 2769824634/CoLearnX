import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, within } from '@testing-library/react';
import MemberPaymentPage from './MemberPaymentPage';

const packages = [
  { id: 'p20', credits: 20, price: '$20', note: '1:1' },
  { id: 'p240', credits: 240, price: '$200', note: '+40', best: true },
];
const ledger = [
  { id: 1, date: '14 Sept 2026, UTC', type: 'TopUp', desc: 'PayPal package', delta: 20, balance: 120, heldAfter: null },
  { id: 2, date: '13 Sept 2026, UTC', type: 'AdminAdjustment', desc: 'Correction', delta: -5, balance: 100, heldAfter: 0 },
];

vi.mock('../../components/MemberShell', () => ({ default: ({ title, children }) => <div><h1>{title}</h1>{children}</div> }));
vi.mock('../../components/PayPalPackageButtons', () => ({
  default: ({ packageId }) => <div data-testid="paypal">{packageId}</div>,
}));
vi.mock('../../api', () => ({ creditsApi: { capturePayPalOrder: vi.fn() } }));
vi.mock('./memberDataState', () => ({
  useMemberSlices: () => ({
    state: { credits: 120, heldCredits: 0, totalCredits: 120, packages, ledger },
    showToast: vi.fn(),
    applyLedgerTopUp: vi.fn(),
    reload: vi.fn(),
  }),
}));

afterEach(cleanup);

it('marks the chosen package and passes that package to the PayPal buttons', () => {
  render(<MemberPaymentPage />);
  const first = screen.getByRole('button', { name: /^20 Credits/ });
  const best = screen.getByRole('button', { name: /240 Credits/ });
  expect(first.getAttribute('aria-pressed')).toBe('true');
  expect(best.getAttribute('aria-pressed')).toBe('false');
  expect(screen.getByTestId('paypal').textContent).toBe('p20');

  fireEvent.click(best);

  expect(best.getAttribute('aria-pressed')).toBe('true');
  expect(first.getAttribute('aria-pressed')).toBe('false');
  expect(screen.getByTestId('paypal').textContent).toBe('p240');
  expect(screen.getByText('240 credits · $200')).toBeTruthy();
});

it('shows ledger rows with readable type chips and unchanged amounts', () => {
  render(<MemberPaymentPage />);
  fireEvent.click(screen.getByRole('button', { name: 'Credit Ledger' }));
  const [, topUp, adjustment] = screen.getAllByRole('row');
  expect(within(topUp).getByText('Top up').className).toContain('pill');
  expect(within(topUp).getByText('+20')).toBeTruthy();
  expect(within(adjustment).getByText('Admin adjustment')).toBeTruthy();
  expect(within(adjustment).getByText('-5')).toBeTruthy();
});
