import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { AuthContext } from '../../auth/AuthContext';
import CreatorIntakeDetailPage from '../creator/CreatorIntakeDetailPage';
import TrainerIntakeDetailPage from './TrainerIntakeDetailPage';

afterEach(() => { cleanup(); vi.unstubAllGlobals(); });

function renderIntake(overrides = {}) {
  const intake = { id: 12, courseId: 1, trainerId: 2, status: 'Cancelled', minEnrollment: 10,
    registrationOpensAt: '2099-01-01T00:00:00Z', registrationClosesAt: '2099-01-10T00:00:00Z',
    startsAt: '2099-01-20T00:00:00Z', endsAt: '2099-01-21T00:00:00Z', sessions: [],
    cancellationReason: 'MinimumEnrollmentNotMet', postponementAvailableUntil: '2099-01-17T00:00:00Z', ...overrides };
  const fetch = vi.fn(async (path, options) => {
    const data = path === '/api/creator/courses' ? [{ id: 1, code: 'UX101', title: 'UX' }]
      : options?.method === 'POST' ? { ...intake, id: 20, status: 'Draft', replacementForIntakeId: 12 }
        : intake;
    return new Response(JSON.stringify(data), { headers: { 'Content-Type': 'application/json' } });
  });
  vi.stubGlobal('fetch', fetch);
  render(<AuthContext.Provider value={{ token: 'creator-token' }}><MemoryRouter initialEntries={['/creator/intakes/12']}>
    <Routes><Route path="/creator/intakes/12" element={<CreatorIntakeDetailPage />} /><Route path="/creator/intakes/20" element={<p>New replacement draft</p>} /></Routes>
  </MemoryRouter></AuthContext.Provider>);
  return fetch;
}

function renderTrainerIntake(overrides = {}) {
  const intake = { id: 12, courseId: 1, trainerId: 2, status: 'Draft', minEnrollment: 2,
    registrationOpensAt: '2099-01-01T00:00:00Z', registrationClosesAt: '2099-01-10T00:00:00Z',
    startsAt: '2099-01-20T00:00:00Z', endsAt: '2099-01-21T00:00:00Z', sessions: [], ...overrides };
  vi.stubGlobal('fetch', vi.fn(async (path) => new Response(JSON.stringify(
    path === '/api/courses' ? [{ id: 1, code: 'UX101', title: 'UX' }] : intake,
  ), { headers: { 'Content-Type': 'application/json' } })));
  render(<AuthContext.Provider value={{ token: 'trainer-token' }}><MemoryRouter initialEntries={['/trainer/courses/intakes/12']}>
    <Routes><Route path="/trainer/courses/intakes/12" element={<TrainerIntakeDetailPage />} /></Routes>
  </MemoryRouter></AuthContext.Provider>);
}

it('creates a replacement Draft from the cancelled class schedule and opens it', async () => {
  const fetch = renderIntake();
  fireEvent.click(await screen.findByRole('button', { name: 'Create replacement class' }));
  fireEvent.click(screen.getByRole('button', { name: 'Publish replacement' }));
  await screen.findByText('New replacement draft');
  const call = fetch.mock.calls.find(([path]) => path.endsWith('/postpone'));
  expect(call[1].method).toBe('POST');
  const body = JSON.parse(call[1].body);
  expect(body.version).toBeUndefined();
  expect(body.minEnrollment).toBe(10);
  expect(new Date(body.startsAt) > new Date('2099-01-20T00:00:00Z')).toBe(true);
});

it.each([{ cancellationReason: 'TrainerCancelled' }, { postponementAvailableUntil: '2000-01-01T00:00:00Z' }, { replacementIntakeId: 20 }])('does not offer a replacement for ineligible class %j', async (overrides) => {
  renderIntake(overrides);
  await waitFor(() => expect(screen.getByRole('heading', { name: 'Schedule' })).toBeTruthy());
  expect(screen.queryByRole('button', { name: 'Create replacement class' })).toBeNull();
});

it('shows the same reservation progress and UTC schedule on the Intake detail', async () => {
  renderTrainerIntake({ reservedEnrollmentCount: 1, activeEnrollmentCount: 0, remainingToMinimum: 1 });
  expect(await screen.findByText('1 reserved')).toBeTruthy();
  expect(screen.getByText('1 more learner needed')).toBeTruthy();
  expect(screen.getByRole('button', { name: 'View reserved places' })).toBeTruthy();
  expect(screen.getByText('20 Jan 2099, 00:00 UTC')).toBeTruthy();
});
