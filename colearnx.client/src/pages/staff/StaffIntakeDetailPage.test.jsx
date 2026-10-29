import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { staffCoursesApi } from '../../api';
import useStaffAuth from '../../auth/useStaffAuth';
import StaffIntakeDetailPage from './StaffIntakeDetailPage';

vi.mock('../../api', () => ({
  staffCoursesApi: {
    getIntake: vi.fn(),
    createSession: vi.fn(),
    publishIntake: vi.fn(),
  },
}));
vi.mock('../../auth/useStaffAuth');
vi.mock('../../api/trainerLaterPhase', () => ({ staffLaterPhaseApi: {} }));

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
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
