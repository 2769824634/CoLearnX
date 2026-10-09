import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { AuthContext } from '../../auth/AuthContext';
import TrainerAttendancePage from './TrainerAttendancePage';

const intake = {
  id: 10,
  courseId: 20,
  status: 'InProgress',
  sessions: [
    { id: 100, label: 'First session', startsAt: '2026-10-01T01:00:00Z', endsAt: '2026-10-01T02:00:00Z' },
    { id: 101, label: 'Second session', startsAt: '2026-10-02T01:00:00Z', endsAt: '2026-10-02T02:00:00Z' },
  ],
};

const learner = {
  enrollmentId: 30,
  courseSessionId: 100,
  userId: 40,
  fullName: 'Intake Learner',
  email: 'learner@example.com',
  enrollmentStatus: 'Active',
  progressPercent: 25,
  attendanceStatus: 'Present',
  sessionAttendances: [
    { courseSessionId: 100, status: 'Present', recordedAt: '2026-10-01T03:00:00Z' },
    { courseSessionId: 101, status: 'Absent', recordedAt: '2026-10-02T03:00:00Z' },
  ],
};

function json(value) {
  return new Response(JSON.stringify(value), { headers: { 'Content-Type': 'application/json' } });
}

function renderAttendance() {
  vi.stubGlobal('fetch', vi.fn(async (path) => {
    if (path === '/api/trainer/intakes') return json([{ id: 10 }]);
    if (path === '/api/courses') return json([{ id: 20, code: 'INT100', title: 'Intake course' }]);
    if (path === '/api/trainer/intakes/10') return json(intake);
    if (path === '/api/trainer/intakes/10/learners') return json([learner]);
    return json([]);
  }));
  render(<AuthContext.Provider value={{ token: 'trainer-token' }}><TrainerAttendancePage /></AuthContext.Provider>);
}

afterEach(() => { cleanup(); vi.unstubAllGlobals(); });

it('shows every Intake learner when the Trainer selects a later Session', async () => {
  renderAttendance();
  expect(await screen.findByText('Intake Learner')).toBeTruthy();

  fireEvent.change(screen.getByLabelText('Session'), { target: { value: '101' } });

  expect(screen.getByText('Intake Learner')).toBeTruthy();
});

it('shows the attendance already recorded for the selected Session', async () => {
  renderAttendance();
  await screen.findByText('Intake Learner');

  fireEvent.change(screen.getByLabelText('Session'), { target: { value: '101' } });

  expect(screen.getByLabelText('Attendance for Intake Learner').value).toBe('Absent');
});

it('saves the Intake learner attendance against the selected Session', async () => {
  renderAttendance();
  await screen.findByText('Intake Learner');
  fireEvent.change(screen.getByLabelText('Session'), { target: { value: '101' } });

  fireEvent.click(screen.getByRole('button', { name: 'Save attendance' }));

  await waitFor(() => expect(globalThis.fetch).toHaveBeenCalledWith(
    '/api/trainer/intakes/10/sessions/101/attendance',
    expect.objectContaining({
      method: 'PUT',
      body: JSON.stringify({ records: [{ enrollmentId: 30, status: 'Absent' }] }),
    }),
  ));
});
