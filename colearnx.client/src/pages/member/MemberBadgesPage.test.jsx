import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { AuthContext } from '../../auth/AuthContext';
import { certificatesApi } from '../../api';
import MemberBadgesPage from './MemberBadgesPage';

vi.mock('../../api', () => ({ certificatesApi: { my: vi.fn(), eligibility: vi.fn(), requests: vi.fn(), request: vi.fn() } }));
beforeEach(() => { certificatesApi.my.mockResolvedValue([]); certificatesApi.eligibility.mockResolvedValue([]); certificatesApi.requests.mockResolvedValue([]); });
afterEach(() => { cleanup(); vi.clearAllMocks(); });
function mount() { render(<MemoryRouter><AuthContext.Provider value={{ token: 'member', user: { fullName: 'Learner', roles: ['Member'] } }}><MemberBadgesPage /></AuthContext.Provider></MemoryRouter>); }

it('shows issued certificates and server-provided eligibility reasons without inventing badges', async () => {
  certificatesApi.my.mockResolvedValue([{ id: 1, title: 'UX certificate', stageName: 'Foundation', stageNumber: 1, courseCode: 'UX101', courseTitle: 'UX', awardedAt: '2026-09-18T12:00:00Z', verificationCode: 'VERIFY-123' }]);
  certificatesApi.eligibility.mockResolvedValue([{ enrollmentId: 3, courseTitle: 'UX', isEligible: false, reasons: ['Attendance must be at least 80%.'] }]);
  mount();
  expect(await screen.findByText('VERIFY-123')).toBeTruthy();
  expect(screen.getByText('Attendance must be at least 80%.')).toBeTruthy();
  expect(screen.getByRole('button', { name: 'Request certificate' }).disabled).toBe(true);
});

it('submits once and immediately shows the returned approval status', async () => {
  certificatesApi.eligibility.mockResolvedValue([{ enrollmentId: 3, courseTitle: 'UX', isEligible: true, reasons: [] }]);
  certificatesApi.request.mockResolvedValue({ id: 9, enrollmentId: 3, courseTitle: 'UX', status: 'Submitted', submittedAt: '2026-09-18T12:00:00Z' });
  mount();
  fireEvent.click(await screen.findByRole('button', { name: 'Request certificate' }));
  expect(await screen.findByText('Awaiting Trainer review')).toBeTruthy();
  expect(certificatesApi.request).toHaveBeenCalledTimes(1);
  expect(screen.getByRole('button', { name: 'Already requested' }).disabled).toBe(true);
});

it('shows a rejected reason and permits retry after a query failure', async () => {
  certificatesApi.requests.mockRejectedValueOnce(new Error('Offline')).mockResolvedValue([{ id: 9, enrollmentId: 3, status: 'TrainerRejected', trainerReviewReason: 'Assessment needs review', submittedAt: '2026-09-18T12:00:00Z' }]);
  mount();
  expect(await screen.findByRole('alert')).toBeTruthy();
  fireEvent.click(screen.getByRole('button', { name: 'Retry' }));
  expect(await screen.findByText('Assessment needs review')).toBeTruthy();
  expect(screen.getByText(/A rejected request cannot be resubmitted/)).toBeTruthy();
});
