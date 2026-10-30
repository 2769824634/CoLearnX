import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { staffCoursesApi } from '../../api';
import useStaffAuth from '../../auth/useStaffAuth';
import StaffIntakeDetailPage from './StaffIntakeDetailPage';

vi.mock('../../api', () => ({
  staffCoursesApi: {
    getIntake: vi.fn(),
    createSession: vi.fn(),
    publishIntake: vi.fn(),
    updateTeachingDates: vi.fn(),
  },
}));
vi.mock('../../auth/useStaffAuth');
vi.mock('../../api/trainerLaterPhase', () => ({ staffLaterPhaseApi: {} }));

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

it('saves teaching dates on a running class using its current version', async () => {
  useStaffAuth.mockReturnValue({ token: 'staff-token' });
  const intake = { id: 9, courseId: 4, status: 'InProgress', version: 'version-one',
    registrationClosesAt: '2026-11-01T00:00:00Z', startsAt: '2026-11-10T00:00:00Z',
    endsAt: '2026-11-12T00:00:00Z', sessions: [] };
  staffCoursesApi.getIntake.mockResolvedValue(intake);
  staffCoursesApi.updateTeachingDates.mockResolvedValue({ ...intake, endsAt: '2026-11-15T16:00:00Z', version: 'version-two' });
  render(<MemoryRouter><StaffIntakeDetailPage /></MemoryRouter>);
  fireEvent.click(await screen.findByRole('button', { name: 'Edit teaching dates' }));
  fireEvent.change(screen.getByLabelText('Last teaching date'), { target: { value: '2026-11-15' } });
  fireEvent.submit(screen.getByRole('button', { name: 'Save dates' }).closest('form'));
  await waitFor(() => expect(staffCoursesApi.updateTeachingDates).toHaveBeenCalledWith('staff-token', 9,
    expect.objectContaining({ version: 'version-one', startsAt: new Date(intake.startsAt).toISOString() })));
  expect(await screen.findByRole('status')).toHaveProperty('textContent', 'Teaching dates saved.');
  expect(screen.queryByRole('dialog')).toBeNull();
});

it('opens each Session in its own staff delivery workspace', async () => {
  useStaffAuth.mockReturnValue({ token: 'staff-token' });
  staffCoursesApi.getIntake.mockResolvedValue({
    id: 9,
    courseId: 4,
    startsAt: '2026-11-10T00:00:00Z',
    endsAt: '2026-11-11T00:00:00Z',
    status: 'Published',
    sessions: [{
      id: 17,
      label: 'Workshop',
      startsAt: '2026-11-10T01:00:00Z',
      endsAt: '2026-11-10T02:00:00Z',
      meetingLink: 'https://meet.example.test/staff',
    }],
  });

  render(<MemoryRouter><StaffIntakeDetailPage /></MemoryRouter>);

  const link = await screen.findByRole('link', { name: 'Open Session' });
  expect(link.getAttribute('href')).toBe('/staff/intakes/9/sessions/17');
  expect(screen.queryByText('Approved learning materials')).toBeNull();
});
