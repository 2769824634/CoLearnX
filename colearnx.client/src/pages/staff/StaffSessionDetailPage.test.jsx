import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { staffCoursesApi } from '../../api';
import { staffLaterPhaseApi } from '../../api/trainerLaterPhase';
import useStaffAuth from '../../auth/useStaffAuth';
import StaffSessionDetailPage from './StaffSessionDetailPage';

vi.mock('../../api', () => ({
  staffCoursesApi: {
    get: vi.fn(),
    getIntake: vi.fn(),
  },
}));
vi.mock('../../api/trainerLaterPhase', () => ({
  staffLaterPhaseApi: {
    learners: vi.fn(),
    recordings: vi.fn(),
    sessionMaterials: vi.fn(),
    saveAttendance: vi.fn(),
    uploadRecording: vi.fn(),
    downloadRecording: vi.fn(),
    uploadSessionMaterial: vi.fn(),
    downloadSessionMaterial: vi.fn(),
  },
  trainerLaterPhaseApi: {},
}));
vi.mock('../../auth/useStaffAuth');
vi.mock('../../auth/AuthContext', () => ({ useAuth: () => ({ token: 'member-token' }) }));

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

it('loads the Session workspace with the separate staff token', async () => {
  useStaffAuth.mockReturnValue({ token: 'staff-token' });
  staffCoursesApi.getIntake.mockResolvedValue({
    id: 9,
    courseId: 4,
    version: '9d73be61-e3b2-49ca-8312-22cd42c3065b',
    sessions: [{ id: 17, label: 'Workshop', startsAt: '2026-11-10T01:00:00Z', endsAt: '2026-11-10T02:00:00Z' }],
  });
  staffCoursesApi.get.mockResolvedValue({ id: 4, code: 'PLT-4', title: 'Platform course' });
  staffLaterPhaseApi.learners.mockResolvedValue([]);
  staffLaterPhaseApi.recordings.mockResolvedValue([]);
  staffLaterPhaseApi.sessionMaterials.mockResolvedValue([]);

  render(<MemoryRouter initialEntries={['/staff/intakes/9/sessions/17']}><Routes>
    <Route path="/staff/intakes/:courseIntakeId/sessions/:courseSessionId" element={<StaffSessionDetailPage />} />
  </Routes></MemoryRouter>);

  expect(await screen.findByRole('heading', { name: 'Workshop' })).toBeTruthy();
  expect(screen.getByRole('heading', { name: 'Attendance' })).toBeTruthy();
  expect(staffCoursesApi.getIntake).toHaveBeenCalledWith('staff-token', 9, expect.any(AbortSignal));
  expect(staffLaterPhaseApi.learners).toHaveBeenCalledWith('staff-token', 9, expect.any(AbortSignal));
});
