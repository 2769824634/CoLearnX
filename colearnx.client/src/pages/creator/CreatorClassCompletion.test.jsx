import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { IntakeWorkspace } from './CreatorIntakeDetailPage';

const intakesApi = vi.hoisted(() => ({ complete: vi.fn(), cancel: vi.fn(), get: vi.fn() }));
vi.mock('../../api/creatorIntakes', () => ({ creatorIntakesApi: intakesApi }));
vi.mock('../../api', () => ({ creatorCoursesApi: { list: vi.fn() } }));

const hour = 3600000;
function intake(overrides = {}) {
  const lastEnd = Date.now() - 2 * hour;
  return {
    id: 9, courseId: 4, trainerId: 2, trainerName: 'Gu Yincheng', status: 'InProgress', version: 'v1', minEnrollment: 3,
    registrationOpensAt: new Date(lastEnd - 40 * 24 * hour).toISOString(),
    registrationClosesAt: new Date(lastEnd - 20 * 24 * hour).toISOString(),
    startsAt: new Date(lastEnd - 7 * 24 * hour).toISOString(), endsAt: new Date(lastEnd).toISOString(),
    confirmedToRunAt: new Date(lastEnd - 20 * 24 * hour).toISOString(),
    sessions: [
      { id: 1, label: 'Session 1', startsAt: new Date(lastEnd - 7 * 24 * hour - 2 * hour).toISOString(), endsAt: new Date(lastEnd - 7 * 24 * hour).toISOString(), meetingLink: 'https://meet.example.com/a' },
      { id: 2, label: 'Session 2', startsAt: new Date(lastEnd - 2 * hour).toISOString(), endsAt: new Date(lastEnd).toISOString(), meetingLink: 'https://meet.example.com/a' },
    ],
    ...overrides,
  };
}

function renderWorkspace(value) {
  const query = { token: 'creator-token', data: { intake: value, courses: [{ id: 4, code: 'INFT 4010', title: 'UX Research Methods' }] }, setData: vi.fn() };
  render(<MemoryRouter><IntakeWorkspace query={query} /></MemoryRouter>);
  return query;
}

beforeEach(() => vi.clearAllMocks());
afterEach(cleanup);

it('completes a running class after its last session, with a confirmation step', async () => {
  intakesApi.complete.mockResolvedValue(intake({ status: 'Completed', version: 'v2' }));
  const query = renderWorkspace(intake());

  fireEvent.click(screen.getByRole('button', { name: 'Complete intake' }));
  expect(intakesApi.complete).not.toHaveBeenCalled();
  fireEvent.click(screen.getByRole('button', { name: 'Confirm completion' }));

  await waitFor(() => expect(intakesApi.complete).toHaveBeenCalledWith('creator-token', 9));
  await waitFor(() => expect(query.setData).toHaveBeenCalledWith(expect.objectContaining({ intake: expect.objectContaining({ status: 'Completed' }) })));
  expect((await screen.findByRole('status')).textContent).toMatch(/Intake completed/);
});

it('keeps the button disabled and says why while a session is still to come', () => {
  const upcoming = intake();
  upcoming.sessions[1].startsAt = new Date(Date.now() + 24 * hour).toISOString();
  upcoming.sessions[1].endsAt = new Date(Date.now() + 26 * hour).toISOString();
  renderWorkspace(upcoming);

  expect(screen.getByRole('button', { name: 'Complete intake' }).disabled).toBe(true);
  expect(screen.getByText(/You can complete the intake after the last session ends/)).toBeTruthy();
});

it.each(['Draft', 'Completed', 'Cancelled'])('offers no completion for a %s class', (status) => {
  renderWorkspace(intake({ status, confirmedToRunAt: status === 'Draft' ? null : intake().confirmedToRunAt }));
  expect(screen.queryByRole('button', { name: 'Complete intake' })).toBeNull();
});

it('tells the Creator what a completed class means', () => {
  renderWorkspace(intake({ status: 'Completed' }));
  expect(screen.getByText(/This intake is completed/)).toBeTruthy();
  expect(screen.queryByRole('button', { name: 'Cancel class' })).toBeNull();
});
