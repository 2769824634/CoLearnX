import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter, useLocation } from 'react-router-dom';
import { AuthContext } from '../../auth/AuthContext';
import { MemberDataContext } from './memberDataState';
import MemberProgramsPage from './MemberProgramsPage';

afterEach(() => { cleanup(); vi.unstubAllGlobals(); });

function LocationProbe() {
  const location = useLocation();
  return <output aria-label="Current URL">{location.search}</output>;
}

function renderPrograms(enrollment, overrides = {}) {
  vi.stubGlobal('fetch', vi.fn(async () => new Response('[]', { headers: { 'Content-Type': 'application/json' } })));
  const auth = { user: { id: 1, fullName: 'Learner', roles: ['Member'] }, token: 'member-token', logout: vi.fn() };
  const member = { state: { enrolled: [{ enrollmentId: 3, courseCode: 'UX101', courseTitle: 'UX', trainer: 'Teacher', progress: 0, ...enrollment }] }, showToast: vi.fn(), changeEnrollment: vi.fn(), acceptPostponement: vi.fn(), ...overrides };
  render(<MemoryRouter><AuthContext.Provider value={auth}><MemberDataContext.Provider value={member}><MemberProgramsPage /></MemberDataContext.Provider></AuthContext.Provider></MemoryRouter>);
  return member;
}

it('disables withdrawal when the server does not offer a refund, even for a distant class', async () => {
  renderPrograms({ status: 'active', startsAt: '2099-01-01T00:00:00Z', withdrawalRefundCredits: null });
  expect(screen.getByRole('button', { name: 'Withdraw from class' }).disabled).toBe(true);
  await screen.findByText('No materials attached yet.');
});

it('shows the exact offered refund before withdrawing', async () => {
  const member = renderPrograms({ status: 'active', withdrawalRefundCredits: 7 });
  fireEvent.click(screen.getByRole('button', { name: 'Withdraw from class' }));
  expect(screen.getByText(/7 credits will be refunded/)).toBeTruthy();
  fireEvent.click(screen.getByRole('button', { name: 'Confirm' }));
  await waitFor(() => expect(member.changeEnrollment).toHaveBeenCalledWith(3, 'withdraw'));
});

it('lets a cancelled learner select a replacement Session and explicitly re-hold credits', async () => {
  const member = renderPrograms({ status: 'cancelled', postponementOptions: [
    { intakeId: 20, courseSessionId: 21, label: 'Morning', startsAt: '2099-01-21T01:00:00Z', endsAt: '2099-01-21T02:00:00Z', registrationClosesAt: '2099-01-11T01:00:00Z', creditsRequired: 10, seatsLeft: null },
    { intakeId: 20, courseSessionId: 22, label: 'Afternoon', startsAt: '2099-01-21T06:00:00Z', endsAt: '2099-01-21T07:00:00Z', registrationClosesAt: '2099-01-11T01:00:00Z', creditsRequired: 10, seatsLeft: 5 },
  ] });
  fireEvent.click(screen.getByRole('button', { name: 'History' }));
  fireEvent.change(screen.getByLabelText('Replacement Session'), { target: { value: '22' } });
  fireEvent.click(screen.getByRole('button', { name: 'Reserve replacement place' }));
  expect(screen.getByText(/10 credits will be placed on hold again/)).toBeTruthy();
  fireEvent.click(screen.getByRole('button', { name: 'Confirm' }));
  await waitFor(() => expect(member.acceptPostponement).toHaveBeenCalledWith(3, 22));
});

it('loads the selected enrollment learning hub and shows its materials and recordings', async () => {
  vi.stubGlobal('fetch', vi.fn(async (path) => new Response(JSON.stringify(path.endsWith('/session-materials')
    ? [{ id: 10, courseSessionId: 12, sessionLabel: 'Session 1', title: 'Session handout', format: 'PDF' }]
    : path.endsWith('/materials')
      ? [{ id: 8, title: 'Workshop guide', format: 'PDF' }]
      : [{ id: 9, title: 'Session replay', recordingUrl: 'https://example.com/replay' }]),
  { headers: { 'Content-Type': 'application/json' } })));
  const auth = { user: { id: 1, fullName: 'Learner', roles: ['Member'] }, token: 'member-token', logout: vi.fn() };
  const member = { state: { enrolled: [{ enrollmentId: 3, courseCode: 'UX101', courseTitle: 'UX', trainer: 'Teacher', progress: 30, status: 'active' }] }, showToast: vi.fn() };

  render(<MemoryRouter initialEntries={['/member/programs']}><AuthContext.Provider value={auth}><MemberDataContext.Provider value={member}><MemberProgramsPage /></MemberDataContext.Provider></AuthContext.Provider></MemoryRouter>);

  expect(await screen.findByText(/Workshop guide/)).toBeTruthy();
  expect(screen.getByText('Session replay')).toBeTruthy();
  expect(screen.getByRole('button', { name: 'Refresh resources' })).toBeTruthy();
  fireEvent.click(screen.getByRole('button', { name: 'Refresh resources' }));
  expect(globalThis.fetch).toHaveBeenCalledWith('/api/enrollments/3/materials', expect.anything());
});

it('shows uploaded materials grouped by CourseSession in the active Learning Hub', async () => {
  vi.stubGlobal('fetch', vi.fn(async (path) => new Response(JSON.stringify(path.endsWith('/session-materials')
    ? [{ id: 10, courseSessionId: 12, sessionLabel: 'Session 1', title: 'Session handout', format: 'PDF' }]
    : []), { headers: { 'Content-Type': 'application/json' } })));
  const auth = { user: { id: 1, fullName: 'Learner', roles: ['Member'] }, token: 'member-token', logout: vi.fn() };
  const member = { state: { enrolled: [{ enrollmentId: 3, courseCode: 'UX101', courseTitle: 'UX', trainer: 'Teacher', progress: 30, status: 'active' }] }, showToast: vi.fn() };

  render(<MemoryRouter initialEntries={['/member/programs']}><AuthContext.Provider value={auth}><MemberDataContext.Provider value={member}><MemberProgramsPage /></MemberDataContext.Provider></AuthContext.Provider></MemoryRouter>);

  expect(await screen.findByText(/Session handout/)).toBeTruthy();
  expect(screen.getByText('Session 1')).toBeTruthy();
});

it('opens the Reserved tab from a notification query without moving confirmed classes', () => {
  const fetch = vi.fn(async () => new Response('[]', { headers: { 'Content-Type': 'application/json' } }));
  vi.stubGlobal('fetch', fetch);
  const auth = { user: { id: 1, fullName: 'Learner', roles: ['Member'] }, token: 'member-token', logout: vi.fn() };
  const member = { state: { enrolled: [{ enrollmentId: 11, courseCode: 'CLXL1004R1', courseTitle: 'Local', trainer: 'Teacher', progress: 0, status: 'reserved', heldCredits: 1, registrationClosesAt: '2099-01-01T00:00:00Z' }] }, showToast: vi.fn() };
  render(<MemoryRouter initialEntries={['/member/programs?tab=reserved']}><AuthContext.Provider value={auth}><MemberDataContext.Provider value={member}><MemberProgramsPage /></MemberDataContext.Provider></AuthContext.Provider></MemoryRouter>);
  expect(screen.getByText(/1 credit is on hold until this class is confirmed/)).toBeTruthy();
  expect(fetch).not.toHaveBeenCalled();
});

it('selects the enrollment named by the notification query when several reservations exist', () => {
  vi.stubGlobal('fetch', vi.fn(async () => new Response('[]', { headers: { 'Content-Type': 'application/json' } })));
  const auth = { user: { id: 1, fullName: 'Learner', roles: ['Member'] }, token: 'member-token', logout: vi.fn() };
  const member = {
    state: { enrolled: [
      { enrollmentId: 11, courseCode: 'FIRST', courseTitle: 'First', trainer: 'Teacher', progress: 0, status: 'reserved', heldCredits: 1, registrationClosesAt: '2099-01-01T00:00:00Z' },
      { enrollmentId: 12, courseCode: 'SECOND', courseTitle: 'Second', trainer: 'Teacher', progress: 0, status: 'reserved', heldCredits: 2, registrationClosesAt: '2099-01-02T00:00:00Z' },
    ] },
    showToast: vi.fn(),
  };
  render(<MemoryRouter initialEntries={['/member/programs?tab=reserved&enrollmentId=12']}><AuthContext.Provider value={auth}><MemberDataContext.Provider value={member}><MemberProgramsPage /></MemberDataContext.Provider></AuthContext.Provider></MemoryRouter>);

  expect(screen.getByText('SECOND — Learning Hub')).toBeTruthy();
  expect(screen.getByText(/2 credits are on hold/)).toBeTruthy();
  expect(screen.queryByText('1 credit are on hold')).toBeNull();
});

it('follows a named enrollment when it moves from Reserved to Active and preserves its query identity', async () => {
  vi.stubGlobal('fetch', vi.fn(async () => new Response('[]', { headers: { 'Content-Type': 'application/json' } })));
  const auth = { user: { id: 1, fullName: 'Learner', roles: ['Member'] }, token: 'member-token', logout: vi.fn() };
  const member = {
    state: { enrolled: [
      { enrollmentId: 11, courseCode: 'FIRST', courseTitle: 'First', trainer: 'Teacher', progress: 0, status: 'reserved', heldCredits: 1, registrationClosesAt: '2099-01-01T00:00:00Z' },
      { enrollmentId: 12, courseCode: 'SECOND', courseTitle: 'Second', trainer: 'Teacher', progress: 20, status: 'active', id: 2 },
    ] },
    showToast: vi.fn(),
  };
  render(<MemoryRouter initialEntries={['/member/programs?tab=reserved&enrollmentId=12&courseId=2']}><LocationProbe /><AuthContext.Provider value={auth}><MemberDataContext.Provider value={member}><MemberProgramsPage /></MemberDataContext.Provider></AuthContext.Provider></MemoryRouter>);

  expect(screen.getByRole('button', { name: 'Active (1)' }).className).toContain('active');
  expect(screen.getByText('SECOND — Learning Hub')).toBeTruthy();
  expect(screen.queryByText('FIRST — Learning Hub')).toBeNull();
  expect(await screen.findByText(/This program is now in the active tab/)).toBeTruthy();
  expect(screen.getByLabelText('Current URL').textContent).toMatch(/tab=active.*enrollmentId=12/);
});

it('does not silently replace a missing requested enrollment with another hub', () => {
  vi.stubGlobal('fetch', vi.fn(async () => new Response('[]', { headers: { 'Content-Type': 'application/json' } })));
  const auth = { user: { id: 1, fullName: 'Learner', roles: ['Member'] }, token: 'member-token', logout: vi.fn() };
  const member = {
    state: { enrolled: [{ enrollmentId: 11, courseCode: 'FIRST', courseTitle: 'First', trainer: 'Teacher', progress: 0, status: 'reserved', heldCredits: 1, registrationClosesAt: '2099-01-01T00:00:00Z' }] },
    showToast: vi.fn(),
  };
  render(<MemoryRouter initialEntries={['/member/programs?tab=reserved&enrollmentId=999']}><AuthContext.Provider value={auth}><MemberDataContext.Provider value={member}><MemberProgramsPage /></MemberDataContext.Provider></AuthContext.Provider></MemoryRouter>);

  expect(screen.getByRole('status').textContent).toMatch(/requested program is not in your current records/i);
  expect(screen.getByText('Learning Hub')).toBeTruthy();
  expect(screen.queryByText('FIRST — Learning Hub')).toBeNull();
});
