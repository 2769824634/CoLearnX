import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import AdminClassesPage from './AdminClassesPage';
import { sortEndingSoon, timeLeftText } from './adminClasses';

const overview = vi.hoisted(() => ({ classes: vi.fn(), classDetail: vi.fn() }));
vi.mock('../../auth/useAdminAuth', () => ({ default: () => ({ token: 'classes-test', logout: vi.fn() }) }));
vi.mock('../../api', () => ({ adminOverviewApi: overview, adminCourseReviewsApi: {} }));
afterEach(() => { cleanup(); vi.resetAllMocks(); });

const row = (overrides = {}) => ({ id: 1, courseId: 1, courseCode: 'INFT 2051', courseTitle: 'UI/UX Design', creatorId: 3, creatorName: 'Zou Ruiqi', trainerId: 2, trainerName: 'Gu Yincheng', startsAt: '2026-10-20T00:00:00Z', endsAt: '2026-12-10T00:00:00Z', status: 'InProgress', learners: 4, minEnrollment: 10, sessionCount: 4, sessionsHeld: 2, attendanceRate: 0.88, ...overrides });

it('lists every class with readable status, attendance and a red row below 70%', async () => {
  overview.classes.mockResolvedValue([
    row(),
    row({ id: 2, courseTitle: 'Cybersecurity', attendanceRate: 0.5, trainerId: 9, trainerName: 'Other Trainer' }),
    row({ id: 3, courseTitle: 'React', status: 'Published', attendanceRate: null, sessionsHeld: 0 }),
  ]);
  render(<MemoryRouter><AdminClassesPage /></MemoryRouter>);

  const low = (await screen.findByRole('button', { name: 'Cybersecurity' })).closest('tr');
  expect(low.className).toBe('admin-class-low');
  expect(within(low).getByText('50%')).toBeTruthy();
  expect(screen.getByRole('button', { name: 'UI/UX Design' }).closest('tr').className).toBe('');
  expect(screen.getAllByText('Open for registration').length).toBeGreaterThan(0);

  fireEvent.change(screen.getByLabelText('Trainer'), { target: { value: '9' } });
  expect(screen.queryByRole('button', { name: 'UI/UX Design' })).toBeNull();
  expect(screen.getByRole('button', { name: 'Cybersecurity' })).toBeTruthy();
});

it('opens a class with its sessions, learners, certificates and materials', async () => {
  overview.classes.mockResolvedValue([row()]);
  overview.classDetail.mockResolvedValue({
    class: row({ status: 'Completed' }),
    sessions: [{ id: 1, label: 'Kick-off', startsAt: '2026-10-22T09:30:00Z', endsAt: '2026-10-22T11:30:00Z', held: true, attended: 3, learners: 4 }],
    learners: [{ userId: 5, fullName: 'Ada Learner', email: 'ada@example.test', enrollmentStatus: 'Active', attended: 1, sessionsHeld: 1, attendanceRate: 1, certificateStatus: 'TrainerApproved' }],
    materials: [{ title: 'Week 1 slides', scope: 'Kick-off' }],
  });
  render(<MemoryRouter><AdminClassesPage /></MemoryRouter>);
  fireEvent.click(await screen.findByRole('button', { name: 'UI/UX Design' }));

  const dialog = await screen.findByRole('dialog', { name: 'Intake details' });
  expect(await within(dialog).findByText('Kick-off', { selector: 'strong' })).toBeTruthy();
  expect(within(dialog).getByText('3 of 4')).toBeTruthy();
  expect(within(dialog).getByText('Waiting for admin')).toBeTruthy();
  expect(within(dialog).getByText('Week 1 slides')).toBeTruthy();
  expect(within(dialog).queryByRole('button', { name: 'Edit teaching dates' })).toBeNull();
  expect(overview.classDetail).toHaveBeenCalledWith('classes-test', '1', expect.anything());
});

it('describes time left in days and sorts running classes by the soonest end', () => {
  const now = new Date('2026-11-11T00:00:00Z');
  expect(timeLeftText(row(), now)).toBe('29 days left');
  expect(timeLeftText(row({ startsAt: '2026-12-01T00:00:00Z' }), now)).toBe('Starts in 20 days');
  expect(timeLeftText(row({ endsAt: '2026-11-01T00:00:00Z' }), now)).toBe('Ended');
  expect(timeLeftText(row({ status: 'Cancelled' }), now)).toBe('—');
  const sorted = sortEndingSoon([
    row({ id: 1, endsAt: '2026-12-10T00:00:00Z' }),
    row({ id: 2, endsAt: '2026-10-01T00:00:00Z' }),
    row({ id: 3, endsAt: '2026-11-25T00:00:00Z' }),
  ], now);
  expect(sorted.map((item) => item.id)).toEqual([3, 1, 2]);
});
