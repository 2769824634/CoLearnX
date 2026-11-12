import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { AuthContext } from '../../auth/AuthContext';
import AppRouter from '../../routes/AppRouter';

vi.mock('../../components/notificationRealtime', () => ({ connectNotificationRealtime: () => ({ connected: false, stop: async () => {} }) }));

const auth = {
  user: { id: 4, email: 'trainer@colearnx.test', fullName: 'Gu Yincheng', activeRole: 'Trainer', roles: ['Trainer'] },
  booting: false, isAuthenticated: true, activeRole: 'trainer', roles: ['Trainer'], logout: vi.fn(),
};

const at = (day, hour) => new Date(2027, 2, day, hour).toISOString();
const session = (id, day, clashes = []) => ({ id, label: `Session ${id}`, startsAt: at(day, 18), endsAt: at(day, 20), meetingLink: 'https://example.com/room', physicalAddress: null, clashes });
const invitation = (intakeId, status, extra = {}) => ({
  intakeId, courseId: 10, courseCode: 'INFT 2051', courseTitle: 'UI/UX Design Fundamentals', creatorName: 'Zou Ruiqi',
  status, intakeStatus: 'Draft', invitedAt: '2027-02-01T01:00:00Z', respondedAt: null, declineReason: null,
  registrationClosesAt: at(2, 18), startsAt: at(4, 18), endsAt: at(11, 20), minEnrollment: 3,
  sessions: [session(1, 4), session(2, 11)], ...extra,
});

const json = (data, status = 200) => new Response(JSON.stringify(data), { status, headers: { 'Content-Type': 'application/json' } });

function renderPage(items, respond = (id, action, body) => json(invitation(id, action === 'accept' ? 'Accepted' : 'Declined',
  { intakeStatus: action === 'accept' ? 'Published' : 'Draft', respondedAt: '2027-02-02T01:00:00Z', declineReason: body?.reason ?? null }))) {
  const fetch = vi.fn(async (path, init = {}) => {
    const match = /^\/api\/trainer\/intake-invitations\/(\d+)\/(accept|decline)$/.exec(path);
    if (match && init.method === 'POST') return respond(Number(match[1]), match[2], init.body ? JSON.parse(init.body) : null);
    if (path === '/api/trainer/intake-invitations') return json(items);
    return json([]);
  });
  vi.stubGlobal('fetch', fetch);
  render(<MemoryRouter initialEntries={['/trainer/invitations']}><AuthContext.Provider value={{ ...auth, token: `trainer-token-${Math.random()}` }}><AppRouter /></AuthContext.Provider></MemoryRouter>);
  return fetch;
}

afterEach(() => { cleanup(); vi.unstubAllGlobals(); });

it('shows each intake schedule on a calendar with the creator and the session count', async () => {
  renderPage([invitation(7, 'Invited')]);

  const waiting = await screen.findByRole('region', { name: 'Intake schedules to review' });
  const card = within(waiting).getByText('UI/UX Design Fundamentals').closest('article');
  expect(within(card).getByText(/from Zou Ruiqi/)).toBeTruthy();
  expect(within(card).getByRole('heading', { name: 'March 2027' })).toBeTruthy();
  expect(within(within(card).getByRole('gridcell', { name: /^4 March 2027/ })).getByText('Session 1')).toBeTruthy();
  expect(within(card).getByText(/2 sessions · 4 hours in total/)).toBeTruthy();
});

it('warns about sessions that clash with teaching the trainer already has', async () => {
  const clash = { intakeId: 3, courseCode: 'INFT 3030', sessionLabel: 'Session 5', startsAt: at(4, 17), endsAt: at(4, 19) };
  renderPage([invitation(7, 'Invited', { sessions: [session(1, 4, [clash]), session(2, 11)] })]);

  const card = (await screen.findByText('UI/UX Design Fundamentals')).closest('article');
  expect(within(card).getByRole('alert').textContent).toContain('1 session clashes');
  expect(within(card).getByText(/INFT 3030 Session 5/)).toBeTruthy();
});

it('accepts a schedule, which publishes the intake and moves it off this page', async () => {
  const fetch = renderPage([invitation(7, 'Invited')]);
  const waiting = await screen.findByRole('region', { name: 'Intake schedules to review' });

  fireEvent.click(within(waiting).getByRole('button', { name: 'Accept schedule' }));

  expect(await screen.findByText('You accepted UI/UX Design Fundamentals (Intake #7). It is now published and listed under Courses.')).toBeTruthy();
  expect(within(waiting).getByText('No intake schedules are waiting for you.')).toBeTruthy();
  expect(screen.queryByRole('region', { name: 'Answered intake schedules' })).toBeNull();
  expect(fetch.mock.calls.filter(([, init = {}]) => init.method === 'POST').map(([path]) => path)).toEqual(['/api/trainer/intake-invitations/7/accept']);
});

it('keeps an accepted schedule in view when the intake could not be published automatically', async () => {
  renderPage([invitation(7, 'Invited')], (id) => json(invitation(id, 'Accepted', { respondedAt: '2027-02-02T01:00:00Z' })));
  const waiting = await screen.findByRole('region', { name: 'Intake schedules to review' });

  fireEvent.click(within(waiting).getByRole('button', { name: 'Accept schedule' }));

  const answered = await screen.findByRole('region', { name: 'Answered intake schedules' });
  expect(await within(answered).findByText('UI/UX Design Fundamentals')).toBeTruthy();
  expect(within(answered).getByText('Accepted', { selector: '.trainer-status' })).toBeTruthy();
  expect(within(answered).getByText(/could not be published automatically, so the creator still has to publish it\./)).toBeTruthy();
  expect(within(waiting).getByText('No intake schedules are waiting for you.')).toBeTruthy();
  expect(screen.queryByText(/It is now published/)).toBeNull();
});

it('declines with a reason the creator will see', async () => {
  const fetch = renderPage([invitation(7, 'Invited')]);
  const waiting = await screen.findByRole('region', { name: 'Intake schedules to review' });

  fireEvent.click(within(waiting).getByRole('button', { name: 'Decline' }));
  fireEvent.change(within(waiting).getByLabelText('Reason for the creator (optional)'), { target: { value: 'I teach on Thursdays.' } });
  fireEvent.click(within(waiting).getByRole('button', { name: 'Confirm decline' }));

  const answered = await screen.findByRole('region', { name: 'Answered intake schedules' });
  expect(await within(answered).findByText(/I teach on Thursdays\./)).toBeTruthy();
  const [, init] = fetch.mock.calls.find(([path]) => path === '/api/trainer/intake-invitations/7/decline');
  expect(JSON.parse(init.body)).toEqual({ reason: 'I teach on Thursdays.' });
});

it('keeps a failed reply in place and explains why', async () => {
  renderPage([invitation(7, 'Invited')], () => json({ code: 'INVITATION_NOT_PENDING', message: 'This invitation is no longer waiting for a reply.' }, 409));
  const waiting = await screen.findByRole('region', { name: 'Intake schedules to review' });

  fireEvent.click(within(waiting).getByRole('button', { name: 'Accept schedule' }));

  expect(await within(waiting).findByText(/no longer waiting for a reply/)).toBeTruthy();
  expect(within(waiting).getByText('UI/UX Design Fundamentals')).toBeTruthy();
});

it('hides published intakes the trainer already accepted', async () => {
  renderPage([invitation(7, 'Accepted', { intakeStatus: 'Published' })]);
  expect(await screen.findByText('No intake schedules are waiting for you.')).toBeTruthy();
  expect(screen.queryByText('UI/UX Design Fundamentals')).toBeNull();
});
