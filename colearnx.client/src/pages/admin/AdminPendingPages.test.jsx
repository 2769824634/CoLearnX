import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, render, screen, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import AdminHomePage from './AdminHomePage';
import AdminApprovalsPage from './AdminApprovalsPage';
import AdminAuditLogsPage from './AdminAuditLogsPage';
import { AdminPendingContext } from './adminPending';

const overviewApi = vi.hoisted(() => ({ pendingCounts: vi.fn() }));
const auditApi = vi.hoisted(() => ({ list: vi.fn(), query: vi.fn(), filterOptions: vi.fn() }));

vi.mock('../../auth/useAdminAuth', () => ({ default: () => ({ token: 'admin-token', logout: vi.fn() }) }));
vi.mock('../../api', () => ({ adminOverviewApi: overviewApi, adminAuditApi: auditApi }));
vi.mock('./AdminRoleRequestsPage', () => ({ default: () => <div>Role queue</div> }));
vi.mock('./AdminCourseReviewsPage', () => ({ default: () => <div>Course queue</div> }));
vi.mock('./AdminLaterApprovalsPage', () => ({ default: () => <div>Later queue</div> }));

const counts = (overrides = {}) => ({ roleRequests: 2, courses: 1, materialVersions: 0, certificates: 3, disputes: 4, ...overrides });

function renderPage(page, pending = null) {
  return render(
    <MemoryRouter>
      <AdminPendingContext.Provider value={pending}>{page}</AdminPendingContext.Provider>
    </MemoryRouter>,
  );
}

afterEach(() => {
  cleanup();
  vi.resetAllMocks();
});

it('summarises every admin queue on the homepage with links to each queue', async () => {
  overviewApi.pendingCounts.mockResolvedValue(counts());
  auditApi.list.mockResolvedValue([]);
  renderPage(<AdminHomePage />);

  const queues = await screen.findByRole('region', { name: 'Pending review queues' });
  const cards = within(queues).getAllByRole('link').map((card) => [
    card.querySelector('h2').textContent,
    card.querySelector('.admin-queue-total').textContent,
    card.getAttribute('href'),
  ]);
  expect(cards).toEqual([
    ['Role requests', '2', '/admin/approvals?queue=roles'],
    ['Courses to review', '1', '/admin/approvals?queue=courses'],
    ['Material versions', '0', '/admin/approvals?queue=materials'],
    ['Certificates', '3', '/admin/approvals?queue=certificates'],
    ['Disputes', '4', '/admin/disputes'],
  ]);
  expect(screen.queryByText(/All caught up/)).toBeNull();
  expect(auditApi.list).toHaveBeenCalledWith('admin-token', 5, expect.anything());
});

it('tells the admin when nothing is waiting', async () => {
  overviewApi.pendingCounts.mockResolvedValue(counts({ roleRequests: 0, courses: 0, certificates: 0, disputes: 0 }));
  auditApi.list.mockResolvedValue([]);
  renderPage(<AdminHomePage />);

  expect((await screen.findByText('All caught up. Nothing is waiting for review.')).getAttribute('role')).toBe('status');
});

it('shows how many items wait in each approval queue tab', () => {
  renderPage(<AdminApprovalsPage />, { counts: counts(), refresh: vi.fn() });

  const tabs = within(screen.getByRole('group', { name: 'Approval queues' })).getAllByRole('button');
  expect(tabs.map((tab) => tab.getAttribute('aria-label'))).toEqual([
    'Role requests, 2 pending',
    'Course reviews, 1 pending',
    null,
    'Certificates, 3 pending',
  ]);
  expect(tabs[2].querySelector('.admin-approval-count')).toBeNull();
  expect(tabs[3].querySelector('.admin-approval-count').textContent).toBe('3');
});

it('offers the resources and outcomes that actually appear in the audit log', async () => {
  auditApi.query.mockResolvedValue([]);
  auditApi.filterOptions.mockResolvedValue({ entityTypes: ['CourseTrainer', 'Dispute', 'NewThing'], results: ['Accepted', 'Resolved'] });
  renderPage(<AdminAuditLogsPage />);

  const resource = screen.getByRole('combobox', { name: 'Resource' });
  await within(resource).findByRole('option', { name: 'Course trainers' });
  expect(within(resource).getAllByRole('option').map((option) => [option.value, option.textContent])).toEqual([
    ['', 'All resources'],
    ['CourseTrainer', 'Course trainers'],
    ['Dispute', 'Disputes'],
    ['NewThing', 'NewThing'],
  ]);
  const outcome = screen.getByRole('combobox', { name: 'Outcome' });
  expect(within(outcome).getAllByRole('option').map((option) => option.textContent)).toEqual(['All outcomes', 'Accepted', 'Resolved']);
});

it('still lets the admin browse everything when filter options fail to load', async () => {
  auditApi.query.mockResolvedValue([]);
  auditApi.filterOptions.mockRejectedValue(new Error('offline'));
  renderPage(<AdminAuditLogsPage />);

  await screen.findByText('No matching activity');
  expect(within(screen.getByRole('combobox', { name: 'Resource' })).getAllByRole('option').map((option) => option.textContent)).toEqual(['All resources']);
  expect(within(screen.getByRole('combobox', { name: 'Outcome' })).getAllByRole('option').map((option) => option.textContent)).toEqual(['All outcomes']);
});
