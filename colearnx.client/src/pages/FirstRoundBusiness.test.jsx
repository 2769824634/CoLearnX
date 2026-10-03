import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { AuthContext } from '../auth/AuthContext';
import { MemberDataContext } from './member/memberDataState';
import MemberHomePage from './member/MemberHomePage';
import MemberCatalogPage from './member/MemberCatalogPage';
import MemberCourseDetailPage from './member/MemberCourseDetailPage';
import MemberProgramsPage from './member/MemberProgramsPage';
import MemberBadgesPage from './member/MemberBadgesPage';
import MemberDisputesPage from './member/MemberDisputesPage';
import CreatorCourseFormPage from './creator/CreatorCourseFormPage';
import CreatorUsagePage from './creator/CreatorUsagePage';
import CreatorIntakeApplicationsPage from './creator/CreatorIntakeApplicationsPage';
import CreatorUploadPage from './creator/CreatorUploadPage';
import TrainerAttendancePage from './trainer/TrainerAttendancePage';
import TrainerLearnersPage from './trainer/TrainerLearnersPage';
import { TrainerError } from './trainer/TrainerUi';
import { CreatorError } from './creator/CreatorUi';

vi.mock('../components/MemberShell', () => ({ default: ({ title, children }) => <main><h1>{title}</h1>{children}</main> }));
afterEach(() => { cleanup(); vi.unstubAllGlobals(); });
const auth = { token: 'test-token', user: { id: 1, roles: ['Member'], fullName: 'Learner' } };
const course = { id: 1, code: 'LONG-CODE-123', title: 'Design', credits: 20, level: 'Beginner', description: 'Learn useful skills.', outcomes: [], creatorName: 'Creator Jane', trainerNames: ['Trainer John'], learningPath: 'Creative studies', sessions: [] };
const state = { credits: 0, heldCredits: 0, user: { displayName: 'Learner' }, enrolled: [], wishlist: [], certificates: [], courses: [course] };
const json = (value) => new Response(JSON.stringify(value), { headers: { 'Content-Type': 'application/json' } });
function Location() { const location = useLocation(); return <output aria-label="Current URL">{location.search}</output>; }
function mount(page, member = {}, path = '/', fetchHandler = async (url) => json(url === '/api/recommendations' ? { items: [] } : [])) {
  vi.stubGlobal('fetch', vi.fn(fetchHandler));
  const value = { state, showToast: vi.fn(), toggleWish: vi.fn(), loadCourseDetail: vi.fn().mockResolvedValue(course), ...member };
  render(<MemoryRouter initialEntries={[path]}><AuthContext.Provider value={auth}><MemberDataContext.Provider value={value}><Routes><Route path="*" element={<>{page}<Location /></>} /><Route path="/member/courses/:courseId" element={<>{page}<Location /></>} /><Route path="/creator/courses/:courseId" element={<>{page}<Location /></>} /></Routes></MemberDataContext.Provider></AuthContext.Provider></MemoryRouter>);
  return value;
}

it('shows loading rather than empty programs and false zero totals on the dashboard', async () => {
  mount(<MemberHomePage />, { state: { ...state, loading: true } });
  expect(screen.queryByText('No active programs')).toBeNull();
  expect(screen.queryByText('Available Credits')).toBeNull();
  await screen.findByText(/No matching courses/);
});

it('explains a filtered empty catalog and restores the results after clearing', () => {
  mount(<MemberCatalogPage />);
  fireEvent.change(screen.getByPlaceholderText(/Search by title/), { target: { value: 'absent' } });
  expect(screen.getByText('No programs match your filters.')).toBeTruthy();
  fireEvent.click(screen.getByRole('button', { name: 'Clear filters' }));
  expect(screen.getByRole('heading', { name: 'Design' })).toBeTruthy();
});

it('shows course definition and actual roles without invented credentials or certificate claims', async () => {
  mount(<MemberCourseDetailPage />, {}, '/member/courses/1');
  expect(await screen.findByText('Learn useful skills.')).toBeTruthy();
  expect(screen.getByText('Creator Jane')).toBeTruthy();
  expect(screen.getByText('Trainer John')).toBeTruthy();
  expect(screen.getByText('Creative studies')).toBeTruthy();
  expect(screen.getByText('Beginner')).toBeTruthy();
  expect(screen.queryByText('Senior Trainer')).toBeNull();
  expect(screen.queryByText('Certificate included')).toBeNull();
});

it('blocks a legacy session with unknown dates before offering a balance top-up', async () => {
  mount(<MemberCourseDetailPage />, { loadCourseDetail: vi.fn().mockResolvedValue({ ...course, sessions: [{ id: 9, label: 'Legacy', capacity: 0, seats: 0 }] }) }, '/member/courses/1');
  const button = await screen.findByRole('button', { name: 'Schedule unavailable' });
  expect(button.disabled).toBe(true);
  expect(screen.queryByRole('heading', { name: 'Insufficient Credits' })).toBeNull();
});

it('retains completed tab and selected enrollment in the URL and confirms a resource refresh', async () => {
  const memberState = { ...state, enrolled: [{ enrollmentId: 3, id: 1, courseCode: 'ABC', courseTitle: 'ABC', status: 'completed', progress: 100 }, { enrollmentId: 4, id: 2, courseCode: 'XYZ', courseTitle: 'XYZ', status: 'completed', progress: 100 }] };
  mount(<MemberProgramsPage />, { state: memberState }, '/member/programs?tab=completed&enrollmentId=4');
  expect(await screen.findByText('Session materials for XYZ')).toBeTruthy();
  fireEvent.click(screen.getByRole('button', { name: 'Refresh resources' }));
  expect(await screen.findByText('Resources refreshed.')).toBeTruthy();
  fireEvent.click(screen.getByRole('button', { name: /ABC — ABC/ }));
  expect(screen.getByLabelText('Current URL').textContent).toContain('enrollmentId=3');
  expect(screen.getByLabelText('Current URL').textContent).toContain('tab=completed');
});

it('shows current certificate progress and ungraded assessments as outstanding requirements', async () => {
  mount(<MemberBadgesPage />, {}, '/', async (url) => json(url.endsWith('/eligibility') ? [{ enrollmentId: 3, courseTitle: 'UX', courseCode: 'UX', progressPercent: 65, attendanceRate: 50, assessmentCount: 1, passedAssessmentCount: 0, reasons: ['Complete the course.'], assessments: [{ id: 9, title: 'Final task', score: null, passScore: 60, status: 'Not graded' }] }] : []));
  expect(await screen.findByText('Progress 65% · Required 100%')).toBeTruthy();
  expect(screen.getByText('Final task')).toBeTruthy();
  expect(screen.getByText('Not graded')).toBeTruthy();
  expect(screen.getByText(/Pass score: 60/)).toBeTruthy();
});

it('explains the dispute prerequisite and excludes reserved enrollments', async () => {
  mount(<MemberDisputesPage />, { state: { ...state, enrolled: [{ enrollmentId: 3, courseCode: 'RES', courseTitle: 'Reserved', status: 'reserved' }] } });
  await screen.findByText('No disputes yet.');
  expect(screen.queryByRole('option', { name: /RES/ })).toBeNull();
  expect(screen.getByText(/Disputes require an active or completed enrollment/)).toBeTruthy();
  expect(screen.getByRole('button', { name: 'Submit dispute' }).disabled).toBe(true);
});

it('keeps a submitted Course definition visible as read-only', async () => {
  mount(<CreatorCourseFormPage />, {}, '/creator/courses/1', async (url) => json(url.endsWith('/options') ? { courseLevels: [{ id: 1, name: 'Beginner' }], learningPaths: [{ id: 2, name: 'Design' }] } : url === '/api/interests' || url.startsWith('/api/materials') ? [] : { ...course, creditCost: 20, courseLevelId: 1, learningPathId: 2, learningOutcomes: ['Build a portfolio'], status: 'PendingApproval' }));
  await waitFor(() => expect(screen.getByLabelText('Description').value).toBe('Learn useful skills.'));
  expect(screen.getByLabelText('Description').matches(':disabled')).toBe(true);
  expect(screen.getByLabelText('Learning outcomes').value).toBe('Build a portfolio');
  expect(screen.queryByRole('button', { name: 'Save changes' })).toBeNull();
});

it('removes action-required language from an empty Creator approval queue', async () => {
  mount(<CreatorIntakeApplicationsPage />);
  await screen.findByText('No session approvals waiting');
  expect(screen.queryByText('Action required')).toBeNull();
});

it('labels filtered usage separately and offers to clear filters', async () => {
  mount(<CreatorUsagePage />);
  await screen.findByText('No usage records found');
  fireEvent.change(screen.getByLabelText('Material'), { target: { value: 'missing' } });
  expect(screen.getByText('No usage records match your filters.')).toBeTruthy();
  expect(screen.getByRole('button', { name: 'Clear filters' })).toBeTruthy();
});

it('shows friendly field labels and keeps database details out of workspace errors', () => {
  render(<><TrainerError error={{ status: 400, code: 'INVALID_INPUT', message: 'Check the form.', fieldErrors: { StartsAt: ['Must precede end.'] } }} /><CreatorError error={{ status: 500, code: 'UPLOAD_FAILED', message: 'SqlServerRetryingExecutionStrategy failed' }} /></>);
  expect(screen.queryByText(/StartsAt:/)).toBeNull();
  expect(screen.getByText('Start date and time: Must precede end.')).toBeTruthy();
  expect(screen.queryByText('INVALID_INPUT')).toBeNull();
  expect(screen.queryByText(/SqlServerRetrying/)).toBeNull();
});

it('keeps the upload prerequisite hidden until course loading finishes', () => {
  mount(<CreatorUploadPage />, {}, '/', () => new Promise(() => {}));
  expect(screen.queryByText(/Create a Course first/)).toBeNull();
  expect(screen.getByText('Loading your courses…')).toBeTruthy();
});

it('shows a support reference for an unconfirmed upload without leaking database details', async () => {
  mount(<CreatorUploadPage />, {}, '/', async (url, options = {}) => {
    if (url === '/api/creator/courses') return json([{ id: 1, code: 'UX', title: 'UX' }]);
    if (url === '/api/materials' && options.method === 'POST') return new Response(JSON.stringify({ code: 'UPLOAD_FAILED', message: 'SqlServerRetryingExecutionStrategy failed', traceId: 'trace-upload-1' }), { status: 500, headers: { 'Content-Type': 'application/json' } });
    return json(url.endsWith('/storage') ? { cloudLinks: false } : []);
  });
  await screen.findByLabelText('Course');
  fireEvent.change(screen.getByLabelText('Course'), { target: { value: '1' } });
  fireEvent.change(screen.getByLabelText('Title'), { target: { value: 'Guide' } });
  fireEvent.change(screen.getByLabelText('File'), { target: { files: [new File(['guide'], 'guide.pdf', { type: 'application/pdf' })] } });
  fireEvent.submit(screen.getByRole('button', { name: 'Upload' }).closest('form'));
  expect(await screen.findByText(/Reference: trace-upload-1/)).toBeTruthy();
  expect(screen.queryByText(/SqlServerRetrying/)).toBeNull();
});

it('shows both calendar dates and UTC for a cross-day session', async () => {
  mount(<MemberCourseDetailPage />, { loadCourseDetail: vi.fn().mockResolvedValue({ ...course, sessions: [{ id: 9, label: 'Overnight', startsAt: '2099-02-01T17:30:00Z', endsAt: '2099-02-02T02:30:00Z', capacity: 0 }] }) }, '/member/courses/1');
  expect(await screen.findByRole('button', { name: /01 Feb 2099, 17:30 UTC → 02 Feb 2099, 02:30 UTC/ })).toBeTruthy();
});

it('sends only one reservation while confirmation is in flight', async () => {
  const enrol = vi.fn(() => new Promise(() => {}));
  mount(<MemberCourseDetailPage />, { state: { ...state, credits: 200 }, enrol, loadCourseDetail: vi.fn().mockResolvedValue({ ...course, sessions: [{ id: 9, label: 'Future class', startsAt: '2099-02-01T17:30:00Z', endsAt: '2099-02-02T02:30:00Z', capacity: 0 }] }) }, '/member/courses/1');
  fireEvent.click(await screen.findByRole('button', { name: 'Reserve place' }));
  const confirm = screen.getByRole('button', { name: 'Confirm Enrolment' });
  fireEvent.click(confirm); fireEvent.click(confirm);
  expect(enrol).toHaveBeenCalledTimes(1);
});

it('identifies the attendance course and full session schedule', async () => {
  mount(<TrainerAttendancePage />, {}, '/', async (url) => json(url === '/api/trainer/intakes' ? [{ id: 7 }] : url === '/api/courses' ? [{ id: 1, code: 'UX101', title: 'Inclusive UX' }] : url.endsWith('/learners') ? [] : { id: 7, courseId: 1, status: 'Published', sessions: [{ id: 9, label: 'Night class', startsAt: '2099-02-01T17:30:00Z', endsAt: '2099-02-02T02:30:00Z' }] }));
  expect(await screen.findByRole('option', { name: /UX101 — Inclusive UX · Intake #7/ })).toBeTruthy();
  expect(screen.getByRole('option', { name: /Night class · 01 Feb 2099, 17:30 UTC → 02 Feb 2099, 02:30 UTC/ })).toBeTruthy();
});

it('counts every required assessment while distinguishing the ungraded count', async () => {
  mount(<TrainerLearnersPage />, {}, '/', async (url) => json(url === '/api/trainer/intakes' ? [{ id: 7 }] : url.endsWith('/learners') ? [{ enrollmentId: 3, fullName: 'Learner', enrollmentStatus: 'Active', progressPercent: 65, attendanceRate: 50, assessmentsPassed: 0, assessmentsGraded: 0, assessmentsTotal: 1 }] : url.endsWith('/assessments') ? [{ id: 9, title: 'Task', maxScore: 100 }] : url.includes('/certificate-requests') ? [] : { id: 7, status: 'Published' }));
  expect(await screen.findByText('0/1 passed')).toBeTruthy();
  expect(screen.getByText('0 graded')).toBeTruthy();
});
