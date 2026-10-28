import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, render, screen, within } from '@testing-library/react';
import AdminPricingPage from './AdminPricingPage';
import { clearReadCache } from '../../api/readCache';

const api = vi.hoisted(() => ({
  pool: vi.fn(),
}));

vi.mock('../../auth/useAdminAuth', () => ({ default: () => ({ token: 'admin-token', logout: vi.fn() }) }));
vi.mock('../../api', () => ({ adminPricingApi: api }));

const pool = [
  { id: 1, subject: 'Digital Technology', levelName: 'Beginner', minTotalCredits: 40, maxTotalCredits: 180 },
  { id: 2, subject: 'Business and Management', levelName: 'Advanced', minTotalCredits: 350, maxTotalCredits: 900 },
];

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
  clearReadCache();
});

it('shows the company price cells and no editor', async () => {
  clearReadCache();
  api.pool.mockResolvedValue(pool);
  render(<AdminPricingPage />);
  const table = await screen.findByRole('table', { name: 'Company price limits' });
  expect(within(table).getByRole('cell', { name: 'Digital Technology' })).toBeTruthy();
  expect(within(table).getByRole('cell', { name: '40–180' })).toBeTruthy();
  expect(screen.queryByRole('button', { name: 'Add domain' })).toBeNull();
  expect(screen.queryByRole('button', { name: 'Save domain' })).toBeNull();
});
