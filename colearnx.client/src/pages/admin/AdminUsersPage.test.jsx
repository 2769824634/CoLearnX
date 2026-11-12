import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, within, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import AdminUsersPage from './AdminUsersPage';

const api = vi.hoisted(() => ({ users: vi.fn(), user: vi.fn(), ledger: vi.fn(), adjustCredits: vi.fn() }));
vi.mock('../../auth/useAdminAuth', () => ({ default: () => ({ token: 'user-details-test' }) }));
const overview = vi.hoisted(() => ({ userActivity: vi.fn() }));
vi.mock('../../api/adminLaterPhase', () => ({ adminLaterPhaseApi: api }));
vi.mock('../../api', () => ({ adminOverviewApi: overview }));
afterEach(cleanup);
beforeEach(() => {
  vi.clearAllMocks();
  overview.userActivity.mockResolvedValue(activity());
});
const classRow = (overrides = {}) => ({ id: 7, courseId: 3, courseCode: 'INFT 2051', courseTitle: 'UI/UX Design', creatorId: 2, creatorName: 'Zou Ruiqi', trainerId: 5, trainerName: 'Gu Yincheng', startsAt: '2026-10-20T00:00:00Z', endsAt: '2026-12-10T00:00:00Z', status: 'InProgress', learners: 4, minEnrollment: 10, sessionCount: 4, sessionsHeld: 2, attendanceRate: 0.5, ...overrides });
const activity = (overrides = {}) => ({ userId: 42, roles: ['Member'], certified: true, specialties: [], teachingClasses: [], createdCourses: [], royaltyCredits: 0, learning: [], certificates: [], totalTopUpCredits: 0, totalSpentCredits: 0, ...overrides });
const user = { id: 42, fullName: 'Example Learner', email: 'learner@example.test', roles: ['Member'], creditBalance: 60, heldCredits: 5, isActive: true, phone: '555-0042', country: 'Singapore' };
it('opens details from the whole user row and locks ledger and adjustments to that user', async () => {
  api.users.mockResolvedValue([user]);
  api.user.mockResolvedValue(user);
  api.ledger.mockResolvedValue([{ id: 1, userId: 42, userName: user.fullName, userEmail: user.email, type: 'TopUp', description: 'Personal top up', delta: 60, balanceAfter: 60, heldAfter: 0 }]);
  api.adjustCredits.mockResolvedValue({ delta: 2, balanceAfter: 62 });
  render(<MemoryRouter><AdminUsersPage /></MemoryRouter>);
  fireEvent.click((await screen.findByText('Example Learner')).closest('tr'));
  const dialog = await screen.findByRole('dialog', { name: 'User details' });
  fireEvent.click(await within(dialog).findByRole('button', { name: 'Profile' }));
  expect(await within(dialog).findByText('555-0042')).toBeTruthy();
  fireEvent.click(within(dialog).getByRole('button', { name: 'Credits' }));
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

it('shows a Member their courses, attendance, certificates and totals on the Learning tab', async () => {
  api.users.mockResolvedValue([]);
  api.user.mockResolvedValue(user);
  overview.userActivity.mockResolvedValue(activity({
    learning: [{ enrollmentId: 9, courseId: 3, courseCode: 'INFT 2051', courseTitle: 'UI/UX Design', classId: 7, startsAt: '2026-10-20T00:00:00Z', endsAt: '2026-12-10T00:00:00Z', status: 'Active', creditsSpent: 30, attended: 1, sessionsHeld: 2, attendanceRate: 0.5 }],
    certificates: [{ id: 1, title: 'Foundation Stage Certificate', courseCode: 'INFT 2002', awardedAt: '2026-04-12T00:00:00Z' }],
    totalTopUpCredits: 120, totalSpentCredits: 30,
  }));
  render(<MemoryRouter initialEntries={['/admin/users?userId=42']}><AdminUsersPage /></MemoryRouter>);
  const dialog = await screen.findByRole('dialog', { name: 'User details' });
  expect(within(dialog).getByRole('button', { name: 'Learning' }).getAttribute('aria-pressed')).toBe('true');
  expect(await within(dialog).findByRole('button', { name: 'UI/UX Design' })).toBeTruthy();
  expect(within(dialog).getByText('120 credits')).toBeTruthy();
  expect(within(dialog).getByText('Foundation Stage Certificate')).toBeTruthy();
  expect(within(dialog).getByText('UI/UX Design').closest('tr').className).toBe('admin-class-low');
  expect(api.ledger).not.toHaveBeenCalled();
});

it('opens a Trainer on the Teaching tab with current classes and folds finished ones away', async () => {
  api.users.mockResolvedValue([]);
  api.user.mockResolvedValue({ ...user, roles: ['Trainer'] });
  overview.userActivity.mockResolvedValue(activity({
    roles: ['Trainer'], certified: false, specialties: ['UI/UX Design'],
    teachingClasses: [classRow(), classRow({ id: 8, courseTitle: 'Old class', status: 'Completed', attendanceRate: 0.9 })],
  }));
  render(<MemoryRouter initialEntries={['/admin/users?userId=42']}><AdminUsersPage /></MemoryRouter>);
  const dialog = await screen.findByRole('dialog', { name: 'User details' });
  expect(await within(dialog).findByRole('button', { name: 'Teaching' })).toBeTruthy();
  expect(within(dialog).queryByRole('button', { name: 'Learning' })).toBeNull();
  expect(within(dialog).getByText('Not certified')).toBeTruthy();
  expect(within(dialog).getByText('Specialties: UI/UX Design')).toBeTruthy();
  expect(within(dialog).getByText('Finished and cancelled intakes (1)')).toBeTruthy();
  expect(within(dialog).getByText('Old class').closest('details').open).toBe(false);
});

it.each(['Trainer', 'Creator'])('offers no credit adjustment for a %s account', async (role) => {
  const holder = { ...user, id: 43, fullName: `Example ${role}`, roles: [role], creditBalance: 0, heldCredits: 0 };
  api.users.mockResolvedValue([holder]);
  api.user.mockResolvedValue(holder);
  api.ledger.mockResolvedValue([]);
  render(<MemoryRouter><AdminUsersPage /></MemoryRouter>);
  fireEvent.click((await screen.findByText(`Example ${role}`)).closest('tr'));
  const dialog = await screen.findByRole('dialog', { name: 'User details' });
  fireEvent.click(await within(dialog).findByRole('button', { name: 'Credits' }));
  expect(await within(dialog).findByText(/Credit adjustments apply to Member accounts only/)).toBeTruthy();
  expect(within(dialog).queryByRole('button', { name: 'Apply adjustment' })).toBeNull();
  expect(within(dialog).queryByLabelText('Credit delta')).toBeNull();
});
