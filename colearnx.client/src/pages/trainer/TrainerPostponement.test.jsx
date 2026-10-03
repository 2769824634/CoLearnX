import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { AuthContext } from '../../auth/AuthContext';
import TrainerIntakeDetailPage from './TrainerIntakeDetailPage';

afterEach(() => { cleanup(); vi.unstubAllGlobals(); });

function renderIntake(overrides = {}) {
  const intake = { id: 12, courseId: 1, trainerId: 2, status: 'Cancelled', minEnrollment: 10,
    registrationOpensAt: '2099-01-01T00:00:00Z', registrationClosesAt: '2099-01-10T00:00:00Z',
    startsAt: '2099-01-20T00:00:00Z', endsAt: '2099-01-21T00:00:00Z', sessions: [],
    cancellationReason: 'MinimumEnrollmentNotMet', postponementAvailableUntil: '2099-01-17T00:00:00Z', ...overrides };
  const fetch = vi.fn(async (path, options) => {
    const data = path === '/api/courses' ? [{ id: 1, code: 'UX101', title: 'UX' }]
      : options?.method === 'POST' ? { ...intake, id: 20, status: 'Draft', replacementForIntakeId: 12 }
        : intake;
    return new Response(JSON.stringify(data), { headers: { 'Content-Type': 'application/json' } });
  });
  vi.stubGlobal('fetch', fetch);
  render(<AuthContext.Provider value={{ token: 'trainer-token' }}><MemoryRouter initialEntries={['/trainer/courses/intakes/12']}>
    <Routes><Route path="/trainer/courses/intakes/12" element={<TrainerIntakeDetailPage />} /><Route path="/trainer/courses/intakes/20" element={<p>New replacement draft</p>} /></Routes>
  </MemoryRouter></AuthContext.Provider>);
  return fetch;
}

it('creates a replacement Draft from the cancelled class schedule and opens it', async () => {
  const fetch = renderIntake();
  fireEvent.click(await screen.findByRole('button', { name: 'Create postponed Intake' }));
  fireEvent.click(screen.getByRole('button', { name: 'Create replacement Draft' }));
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
  await waitFor(() => expect(screen.getByText('Minimum enrollment')).toBeTruthy());
  expect(screen.queryByRole('button', { name: 'Create postponed Intake' })).toBeNull();
});
