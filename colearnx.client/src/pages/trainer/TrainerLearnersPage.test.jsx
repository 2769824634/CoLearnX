import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, render, screen } from '@testing-library/react';
import { AuthContext } from '../../auth/AuthContext';
import TrainerLearnersPage from './TrainerLearnersPage';

function json(value) {
  return new Response(JSON.stringify(value), { headers: { 'Content-Type': 'application/json' } });
}

afterEach(() => { cleanup(); vi.unstubAllGlobals(); });

it('shows each Session attendance state in the Intake learner list', async () => {
  vi.stubGlobal('fetch', vi.fn(async (path) => {
    if (path === '/api/trainer/intakes') return json([{ id: 10 }]);
    if (path === '/api/trainer/intakes/10') return json({
      id: 10,
      status: 'InProgress',
      minEnrollment: 2,
      sessions: [
        { id: 100, label: 'First session' },
        { id: 101, label: 'Second session' },
      ],
    });
    if (path === '/api/trainer/intakes/10/learners') return json([{
      enrollmentId: 30,
      userId: 40,
      fullName: 'Intake Learner',
      email: 'learner@example.com',
      enrollmentStatus: 'Active',
      progressPercent: 25,
      attendanceRate: 50,
      assessmentsPassed: 0,
      assessmentsGraded: 0,
      assessmentsTotal: 0,
      sessionAttendances: [
        { courseSessionId: 100, status: 'Present' },
        { courseSessionId: 101, status: null },
      ],
    }]);
    if (path === '/api/trainer/intakes/10/assessments') return json([]);
    if (path === '/api/trainer/certificate-requests') return json([]);
    return json([]);
  }));

  render(<AuthContext.Provider value={{ token: 'trainer-token' }}><TrainerLearnersPage /></AuthContext.Provider>);

  expect(await screen.findByText('First session: Present')).toBeTruthy();
  expect(screen.getByText('Second session: Not recorded')).toBeTruthy();
});
