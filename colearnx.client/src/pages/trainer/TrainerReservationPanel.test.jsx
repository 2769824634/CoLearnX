import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { AuthContext } from '../../auth/AuthContext';
import { trainerLaterPhaseApi } from '../../api/trainerLaterPhase';
import TrainerReservationPanel from './TrainerReservationPanel';

vi.mock('../../api/trainerLaterPhase', () => ({ trainerLaterPhaseApi: { reservations: vi.fn() } }));
afterEach(() => { cleanup(); vi.clearAllMocks(); });
const intake = { id: 8, minEnrollment: 2, reservedEnrollmentCount: 1, activeEnrollmentCount: 0, remainingToMinimum: 1 };
function mount(value = intake) {
  render(<AuthContext.Provider value={{ token: 'trainer-token' }}><TrainerReservationPanel intake={value} /></AuthContext.Provider>);
}

it('shows the reservation progress and loads an authorised read-only list on demand', async () => {
  trainerLaterPhaseApi.reservations.mockResolvedValue([{ enrollmentId: 11, learnerName: 'QA Member', courseSessionId: 10, sessionLabel: 'Online Session', creditsHeld: 1, reservedAt: '2026-10-04T12:00:00Z' }]);
  mount();
  expect(screen.getByText('1 reserved')).toBeTruthy();
  expect(screen.getByText('2 learners minimum')).toBeTruthy();
  expect(screen.getByText('1 more learner needed')).toBeTruthy();
  expect(trainerLaterPhaseApi.reservations).not.toHaveBeenCalled();
  fireEvent.click(screen.getByRole('button', { name: 'View reserved places' }));
  expect(await screen.findByText('QA Member')).toBeTruthy();
  expect(screen.getByText('ENR-11')).toBeTruthy();
  expect(screen.getByText('1 credit on hold')).toBeTruthy();
  expect(screen.getByText('Online Session · Session #10')).toBeTruthy();
  expect(trainerLaterPhaseApi.reservations).toHaveBeenCalledWith('trainer-token', 8, expect.any(AbortSignal));
  expect(screen.queryByRole('button', { name: /Grade|Mark complete/ })).toBeNull();
});

it('keeps the progress visible and offers a retry when the reservation list cannot load', async () => {
  trainerLaterPhaseApi.reservations.mockRejectedValue({ code: 'NETWORK_ERROR', message: 'Connection lost.' });
  mount();
  fireEvent.click(screen.getByRole('button', { name: 'View reserved places' }));
  expect(await screen.findByRole('alert')).toBeTruthy();
  expect(screen.getByRole('button', { name: 'Retry loading' })).toBeTruthy();
  expect(screen.getByText('1 reserved')).toBeTruthy();
});

it('does not present a confirmed class as needing more learners', () => {
  mount({ ...intake, confirmedToRunAt: '2026-10-04T12:00:00Z', reservedEnrollmentCount: 0, remainingToMinimum: 2 });
  expect(screen.getByText('Class confirmed to run')).toBeTruthy();
  expect(screen.queryByText('2 more learners needed')).toBeNull();
});
