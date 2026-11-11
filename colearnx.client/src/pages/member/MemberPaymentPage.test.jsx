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
  { id: 3, date: '12 Sept 2026, UTC', type: 'Hold', desc: 'Reserved', delta: -30, balance: 70, heldAfter: 30, courseId: 7, courseCode: 'INFT 2051', courseTitle: 'UI/UX Design Fundamentals' },
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

it('shows readable ledger summaries, labels, filters and related courses', () => {
  render(<MemberPaymentPage />);
  fireEvent.click(screen.getByRole('button', { name: 'Credit Ledger' }));
  expect(screen.getByText('20 credits added')).toBeTruthy();
  expect(screen.getByText('35 credits used')).toBeTruthy();
  const [, topUp, adjustment, courseHold] = screen.getAllByRole('row');
  expect(within(topUp).getByText('Top up').className).toContain('pill');
  expect(within(topUp).getByText('+20')).toBeTruthy();
  expect(within(adjustment).getByText('Manual adjustment')).toBeTruthy();
  expect(within(adjustment).getByText('-5')).toBeTruthy();
  expect(within(courseHold).getByText('Course reservation')).toBeTruthy();
  expect(within(courseHold).getByRole('link', { name: /INFT 2051.*UI\/UX Design Fundamentals/ }).getAttribute('href')).toBe('/member/courses/7');

  fireEvent.change(screen.getByRole('combobox', { name: 'Transaction type' }), { target: { value: 'Hold' } });
  expect(screen.getAllByRole('row')).toHaveLength(2);
  expect(screen.queryByText('PayPal package')).toBeNull();

  fireEvent.change(screen.getByRole('combobox', { name: 'Transaction type' }), { target: { value: '' } });
  fireEvent.change(screen.getByRole('combobox', { name: 'Related course' }), { target: { value: '7' } });
  expect(screen.getAllByRole('row')).toHaveLength(2);
  expect(screen.getByText('Reserved')).toBeTruthy();
});
