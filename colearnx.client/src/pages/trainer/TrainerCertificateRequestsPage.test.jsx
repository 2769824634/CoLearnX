import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { AuthContext } from '../../auth/AuthContext';
import AppRouter from '../../routes/AppRouter';

vi.mock('../../components/notificationRealtime', () => ({ connectNotificationRealtime: () => ({ connected: false, stop: async () => {} }) }));

const auth = {
  user: { id: 4, email: 'trainer@colearnx.test', fullName: 'Gu Yincheng', activeRole: 'Trainer', roles: ['Trainer'] },
  booting: false,
  isAuthenticated: true,
  activeRole: 'trainer',
  roles: ['Trainer'],
  logout: vi.fn(),
};

const request = (id, status, extra = {}) => ({
  id, enrollmentId: id * 10, userId: id * 100, learnerName: `Learner ${id}`, courseCode: 'DES-1', courseTitle: 'Design basics',
  courseIntakeId: 10, courseId: 1, status, submittedAt: '2026-11-01T01:00:00Z', trainerReviewReason: null, trainerReviewedAt: null, ...extra,
});

function json(data, status = 200) {
  return new Response(JSON.stringify(data), { status, headers: { 'Content-Type': 'application/json' } });
}

function renderPage(items, respond = (id, body) => json(request(id, body.decision === 'Approve' ? 'TrainerApproved' : 'TrainerRejected',
  { trainerReviewReason: body.reason, trainerReviewedAt: '2026-11-02T01:00:00Z' }))) {
  const fetch = vi.fn(async (path, init = {}) => {
    const match = /^\/api\/trainer\/certificate-requests\/(\d+)\/review$/.exec(path);
    if (match && init.method === 'POST') return respond(Number(match[1]), JSON.parse(init.body));
    if (path === '/api/trainer/certificate-requests') return json(items);
    if (path === '/api/trainer/intakes') return json([{ id: 10, courseId: 1, startsAt: '2026-10-01T01:00:00Z', endsAt: '2026-10-20T01:00:00Z' }]);
    return json([]);
  });
  vi.stubGlobal('fetch', fetch);
  render(<MemoryRouter initialEntries={['/trainer/certificates']}><AuthContext.Provider value={{ ...auth, token: `trainer-token-${Math.random()}` }}><AppRouter /></AuthContext.Provider></MemoryRouter>);
  return fetch;
}

const reviewCalls = (fetch) => fetch.mock.calls.filter(([path]) => path.endsWith('/review'));

afterEach(() => { cleanup(); vi.unstubAllGlobals(); });

it('lists every Member waiting for review with the course and Intake, apart from reviewed requests', async () => {
  renderPage([request(1, 'Submitted'), request(2, 'TrainerApproved'), request(3, 'TrainerRejected', { trainerReviewReason: 'Missed the final session' })]);
  const waiting = await screen.findByRole('region', { name: 'Waiting for your review' });
  const row = within(waiting).getByText('Learner 1').closest('article');
  expect(within(row).getByText('DES-1')).toBeTruthy();
  expect(within(row).getByText('Design basics')).toBeTruthy();
  expect(within(row).getByRole('link', { name: /^Intake / }).getAttribute('href')).toBe('/trainer/courses/intakes/10');
  expect(within(waiting).queryByText('Learner 2')).toBeNull();
  const reviewed = screen.getByRole('region', { name: 'Reviewed' });
  expect(within(reviewed).getByText('Learner 2')).toBeTruthy();
  expect(within(reviewed).getByText('Your note: Missed the final session')).toBeTruthy();
  expect(within(reviewed).queryByRole('button')).toBeNull();
  expect(screen.getByRole('link', { name: 'Certificate requests' }).className).toContain('active');
});

it('approves a request and moves it to Reviewed', async () => {
  const fetch = renderPage([request(1, 'Submitted')]);
  const waiting = await screen.findByRole('region', { name: 'Waiting for your review' });
  fireEvent.click(within(waiting).getByRole('button', { name: 'Approve' }));
  expect(await within(screen.getByRole('region', { name: 'Reviewed' })).findByText('Learner 1')).toBeTruthy();
  expect(within(screen.getByRole('region', { name: 'Waiting for your review' })).getByText('No certificate requests are waiting for you.')).toBeTruthy();
  expect(JSON.parse(reviewCalls(fetch)[0][1].body)).toEqual({ decision: 'Approve', reason: null });
});

it('requires a review note before rejecting', async () => {
  const fetch = renderPage([request(1, 'Submitted')]);
  const waiting = await screen.findByRole('region', { name: 'Waiting for your review' });
  const reject = within(waiting).getByRole('button', { name: 'Reject' });
  expect(reject.disabled).toBe(true);
  fireEvent.change(within(waiting).getByLabelText('Review note (required to reject)'), { target: { value: 'Attendance too low' } });
  fireEvent.click(reject);
  expect(await within(screen.getByRole('region', { name: 'Reviewed' })).findByText('Your note: Attendance too low')).toBeTruthy();
  expect(JSON.parse(reviewCalls(fetch)[0][1].body)).toEqual({ decision: 'Reject', reason: 'Attendance too low' });
});

it('keeps a failed review in place and explains why', async () => {
  renderPage([request(1, 'Submitted')], () => json({ code: 'CERTIFICATE_REQUEST_ALREADY_REVIEWED', message: 'The certificate request already has a different Trainer decision.' }, 409));
  const waiting = await screen.findByRole('region', { name: 'Waiting for your review' });
  fireEvent.click(within(waiting).getByRole('button', { name: 'Approve' }));
  expect(await within(waiting).findByRole('alert')).toBeTruthy();
  expect(within(waiting).getByText('Learner 1')).toBeTruthy();
});
