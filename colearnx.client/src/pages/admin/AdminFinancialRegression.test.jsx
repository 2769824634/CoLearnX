import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import AdminDisputesPage from './AdminDisputesPage';

const api = vi.hoisted(() => ({ disputes: vi.fn(), reviewDispute: vi.fn() }));
vi.mock('../../auth/useAdminAuth', () => ({ default: () => ({ token: 'admin', logout: vi.fn() }) }));
vi.mock('../../api/adminLaterPhase', () => ({ adminLaterPhaseApi: api }));
afterEach(cleanup);
beforeEach(() => vi.clearAllMocks());

it('never carries an unknown refund operation into a different case after refresh', async () => {
  const first = { id: 1, userName: 'First learner', status: 'Open', enrollmentId: 11, creditsSpent: 25 };
  const second = { id: 2, userName: 'Second learner', status: 'Open', enrollmentId: 22, creditsSpent: 40 };
  let rows = [first, second];
  api.disputes.mockImplementation(async () => rows);
  api.reviewDispute.mockImplementation(async () => {
    rows = [second];
    throw Object.assign(new Error('Response was lost'), { status: 500 });
  });
  render(<AdminDisputesPage />);
  await screen.findByRole('heading', { name: 'DSP-1 · First learner' });
  fireEvent.change(screen.getByLabelText('Resolution note'), { target: { value: 'Refund the first case' } });
  fireEvent.click(screen.getByRole('button', { name: 'Process refund' }));
  await screen.findByText('Dispute review result is unconfirmed');
  fireEvent.click(screen.getByRole('button', { name: 'Check current records' }));
  expect(await screen.findByText('Select a dispute to inspect its evidence.')).toBeTruthy();
  expect(screen.queryByRole('button', { name: 'Process refund' })).toBeNull();
  fireEvent.click(screen.getByRole('button', { name: /Second learner/ }));
  expect(screen.getByLabelText('Resolution note').value).toBe('');
  expect(screen.getByLabelText('Credits to restore').value).toBe('40');
});
