import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import AdminPricingPage from './AdminPricingPage';
import { ApiError } from '../../api/client';
import { clearReadCache } from '../../api/readCache';

const api = vi.hoisted(() => ({
  tiers: vi.fn(),
  domains: vi.fn(),
  createDomain: vi.fn(),
  updateDomain: vi.fn(),
  deactivateDomain: vi.fn(),
  pool: vi.fn(),
}));

vi.mock('../../auth/useAdminAuth', () => ({ default: () => ({ token: 'admin-token', logout: vi.fn() }) }));
vi.mock('../../api', () => ({ adminPricingApi: api }));

const tiers = [
  { id: 1, code: 'A', theoryMinHourlyCredits: 40, theoryMaxHourlyCredits: 120, practiceMinHourlyCredits: 60, practiceMaxHourlyCredits: 150, sortOrder: 1 },
  { id: 2, code: 'B', theoryMinHourlyCredits: 30, theoryMaxHourlyCredits: 100, practiceMinHourlyCredits: 40, practiceMaxHourlyCredits: 120, sortOrder: 2 },
  { id: 3, code: 'C', theoryMinHourlyCredits: 20, theoryMaxHourlyCredits: 60, practiceMinHourlyCredits: 30, practiceMaxHourlyCredits: 100, sortOrder: 3 },
  { id: 4, code: 'D', theoryMinHourlyCredits: 15, theoryMaxHourlyCredits: 40, practiceMinHourlyCredits: 25, practiceMaxHourlyCredits: 70, sortOrder: 4 },
  { id: 5, code: 'E', theoryMinHourlyCredits: 15, theoryMaxHourlyCredits: 50, practiceMinHourlyCredits: 25, practiceMaxHourlyCredits: 80, sortOrder: 5 },
];

const stamp = '2026-10-19T00:00:00Z';
let domains;

function intakeRows() {
  const rows = [];
  let id = 1;
  for (const tier of ['A', 'B', 'C', 'D', 'E']) {
    for (const courseType of ['Theory', 'Practice']) {
      for (const levelName of ['Beginner', 'Intermediate', 'Advanced']) {
        rows.push({
          id: id++,
          saleMode: 'Intake',
          tierCode: tier,
          courseType,
          levelName,
          minHourlyCredits: 40,
          maxHourlyCredits: 80,
          minHours: 2,
          maxHours: 12,
          minTotalCredits: null,
          maxTotalCredits: null,
          contentSharePercent: courseType === 'Theory' ? 25 : 10,
          deliverySharePercent: courseType === 'Theory' ? 25 : 45,
          platformSharePercent: courseType === 'Theory' ? 50 : 45,
        });
      }
    }
  }
  return rows;
}

const pool = [
  ...intakeRows(),
  { id: 31, saleMode: 'CoursePack', tierCode: null, courseType: 'Theory', levelName: 'Beginner', minHourlyCredits: null, maxHourlyCredits: null, minHours: null, maxHours: null, minTotalCredits: 10, maxTotalCredits: 100, contentSharePercent: 50, deliverySharePercent: 0, platformSharePercent: 50 },
  { id: 32, saleMode: 'CoursePack', tierCode: null, courseType: 'Theory', levelName: 'Intermediate', minHourlyCredits: null, maxHourlyCredits: null, minHours: null, maxHours: null, minTotalCredits: 50, maxTotalCredits: 400, contentSharePercent: 50, deliverySharePercent: 0, platformSharePercent: 50 },
  { id: 33, saleMode: 'CoursePack', tierCode: null, courseType: 'Theory', levelName: 'Advanced', minHourlyCredits: null, maxHourlyCredits: null, minHours: null, maxHours: null, minTotalCredits: 150, maxTotalCredits: 1000, contentSharePercent: 50, deliverySharePercent: 0, platformSharePercent: 50 },
];

beforeEach(() => {
  clearReadCache();
  domains = [{ id: 1, name: 'IT and Data', priceTierId: 2, tierCode: 'B', isActive: true, createdAt: stamp, updatedAt: stamp }];
  api.tiers.mockImplementation(async () => tiers);
  api.domains.mockImplementation(async () => domains);
  api.pool.mockResolvedValue(pool);
  api.createDomain.mockImplementation(async (_token, body) => {
    const tier = tiers.find((item) => item.id === body.priceTierId);
    const created = { id: 9, name: body.name, priceTierId: body.priceTierId, tierCode: tier.code, isActive: true, createdAt: stamp, updatedAt: stamp };
    domains = [...domains, created];
    return created;
  });
  api.deactivateDomain.mockImplementation(async (_token, id) => {
    domains = domains.map((domain) => (domain.id === id ? { ...domain, isActive: false } : domain));
    return domains.find((domain) => domain.id === id);
  });
});

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

it('lists domains with tier and status', async () => {
  render(<AdminPricingPage />);
  expect(await screen.findByRole('cell', { name: 'IT and Data' })).toBeTruthy();
  const row = screen.getByRole('row', { name: /IT and Data/ });
  expect(within(row).getByRole('cell', { name: 'B' })).toBeTruthy();
  expect(within(row).getByRole('cell', { name: 'Active' })).toBeTruthy();
});

it('posts a new domain and shows the row', async () => {
  render(<AdminPricingPage />);
  await screen.findByRole('cell', { name: 'IT and Data' });
  fireEvent.click(screen.getByRole('button', { name: 'Add domain' }));
  fireEvent.change(screen.getByLabelText('Name'), { target: { value: 'Household Finance' } });
  fireEvent.change(screen.getByLabelText('Tier'), { target: { value: '3' } });
  expect(screen.getByRole('option', { name: /C · theory 20–60 · practice 30–100/ })).toBeTruthy();
  fireEvent.click(screen.getByRole('button', { name: 'Save domain' }));

  await waitFor(() => expect(api.createDomain).toHaveBeenCalledWith('admin-token', { name: 'Household Finance', priceTierId: 3 }));
  expect(await screen.findByRole('cell', { name: 'Household Finance' })).toBeTruthy();
});

it('shows a duplicate name error next to Name', async () => {
  api.createDomain.mockRejectedValue(new ApiError('DOMAIN_NAME_TAKEN', 'A domain with this name already exists.', 409, { name: ['A domain with this name already exists.'] }));
  render(<AdminPricingPage />);
  await screen.findByRole('cell', { name: 'IT and Data' });
  fireEvent.click(screen.getByRole('button', { name: 'Add domain' }));
  fireEvent.change(screen.getByLabelText('Name'), { target: { value: 'health and care' } });
  fireEvent.change(screen.getByLabelText('Tier'), { target: { value: '3' } });
  fireEvent.click(screen.getByRole('button', { name: 'Save domain' }));

  await waitFor(() => {
    const field = screen.getByLabelText('Name').closest('.form-group');
    expect(within(field).getByRole('alert').textContent).toMatch(/already exists/);
  });
});

it('asks before deactivating and then removes the actions', async () => {
  render(<AdminPricingPage />);
  const row = await screen.findByRole('row', { name: /IT and Data/ });
  fireEvent.click(within(row).getByRole('button', { name: 'Deactivate' }));
  const dialog = screen.getByRole('dialog');
  expect(within(dialog).getByText('Deactivate IT and Data? New courses can no longer choose it. Courses that already use it keep it.')).toBeTruthy();
  fireEvent.click(within(dialog).getByRole('button', { name: 'Deactivate' }));

  await waitFor(() => {
    const updated = screen.getByRole('row', { name: /IT and Data/ });
    expect(within(updated).getByRole('cell', { name: 'Inactive' })).toBeTruthy();
    expect(within(updated).queryByRole('button', { name: 'Edit' })).toBeNull();
    expect(within(updated).queryByRole('button', { name: 'Deactivate' })).toBeNull();
  });
});

it('shows 30 intake rows, filters by tier, and shows 3 course-pack rows', async () => {
  render(<AdminPricingPage />);
  fireEvent.click(await screen.findByRole('tab', { name: 'Price pool' }));
  const intake = await screen.findByRole('table', { name: 'Intake prices' });
  expect(within(intake).getAllByRole('row')).toHaveLength(31);
  fireEvent.change(screen.getByLabelText('Tier filter'), { target: { value: 'A' } });
  expect(within(intake).getAllByRole('row')).toHaveLength(7);
  const packs = screen.getByRole('table', { name: 'Course pack prices' });
  expect(within(packs).getAllByRole('row')).toHaveLength(4);
  expect(within(packs).getByRole('cell', { name: '10–100' })).toBeTruthy();
});
