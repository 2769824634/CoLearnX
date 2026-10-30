import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { adminCourseReviewsApi } from '../../api';
import useAdminAuth from '../../auth/useAdminAuth';
import AdminIntakeDatesPage from './AdminIntakeDatesPage';

vi.mock('../../api', () => ({ adminCourseReviewsApi: { listIntakes: vi.fn(), getIntake: vi.fn(), updateTeachingDates: vi.fn() } }));
vi.mock('../../auth/useAdminAuth');
afterEach(() => { cleanup(); vi.clearAllMocks(); });

it('loads class details and saves dates using the admin token while closed classes stay read-only', async () => {
  useAdminAuth.mockReturnValue({ token: 'admin-dates-test' });
  const intake = { id: 9, courseId: 4, courseTitle: 'Workshop', courseCode: 'CO-004', trainerName: 'Trainer One',
    status: 'InProgress', startsAt: '2026-11-10T09:00:00Z', endsAt: '2026-11-12T11:00:00Z',
    registrationClosesAt: '2026-11-01T00:00:00Z', version: 'version-one', sessions: [] };
  adminCourseReviewsApi.listIntakes.mockResolvedValue([intake, { ...intake, id: 10, status: 'Completed' }]);
  adminCourseReviewsApi.getIntake.mockResolvedValue(intake);
  adminCourseReviewsApi.updateTeachingDates.mockResolvedValue({ ...intake, version: 'version-two' });
  render(<AdminIntakeDatesPage />);
  fireEvent.click(await screen.findByRole('button', { name: 'Edit teaching dates' }));
  expect(adminCourseReviewsApi.listIntakes).toHaveBeenCalledWith('admin-dates-test', expect.any(AbortSignal));
  expect(adminCourseReviewsApi.getIntake).toHaveBeenCalledWith('admin-dates-test', 9, expect.any(AbortSignal));
  fireEvent.change(await screen.findByLabelText('Last teaching date'), { target: { value: '2026-11-15' } });
  fireEvent.submit(screen.getByRole('button', { name: 'Save dates' }).closest('form'));
  await waitFor(() => expect(adminCourseReviewsApi.updateTeachingDates).toHaveBeenCalledWith('admin-dates-test', 9,
    expect.objectContaining({ version: 'version-one', endsAt: new Date(2026, 10, 16).toISOString() })));
  expect(await screen.findByRole('status')).toHaveProperty('textContent', 'Teaching dates saved.');
  expect(screen.queryByRole('dialog')).toBeNull();
});
