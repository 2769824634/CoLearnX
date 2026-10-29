import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { AuthContext } from '../../auth/AuthContext';
import AppRouter from '../../routes/AppRouter';

const auth = {
  token: 'trainer-token',
  user: { id: 4, fullName: 'Trainer User', activeRole: 'Trainer', roles: ['Trainer'] },
  booting: false,
  isAuthenticated: true,
  activeRole: 'trainer',
  roles: ['Trainer'],
  logout: vi.fn(),
  switchRole: vi.fn(),
};

const json = (value) => new Response(JSON.stringify(value), {
  status: 200,
  headers: { 'Content-Type': 'application/json' },
});

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

it('lists accepted teaching courses before their Intakes', async () => {
  vi.stubGlobal('fetch', vi.fn(async (path) => {
    if (path === '/api/trainer/course-invitations') return json([
      { id: 1, courseId: 20, courseCode: 'UX200', courseTitle: 'Accepted course', creatorName: 'Creator', status: 'Accepted' },
      { id: 2, courseId: 21, courseCode: 'SEC300', courseTitle: 'Waiting course', creatorName: 'Creator', status: 'Invited' },
    ]);
    if (path === '/api/trainer/intakes') return json([
      { id: 10, courseId: 20, status: 'Published', startsAt: '2026-11-01T01:00:00Z', endsAt: '2026-11-02T01:00:00Z' },
    ]);
    if (path === '/api/courses') return json([{ id: 20, code: 'UX200', title: 'Accepted course' }]);
    return json([]);
  }));

  render(<MemoryRouter initialEntries={['/trainer/courses']}>
    <AuthContext.Provider value={auth}><AppRouter /></AuthContext.Provider>
  </MemoryRouter>);

  expect(await screen.findByRole('heading', { name: 'My courses' })).toBeTruthy();
  expect(screen.getByRole('link', { name: 'Accepted course' }).getAttribute('href')).toBe('/trainer/courses/20');
  expect(screen.queryByText('Waiting course')).toBeNull();
  expect(screen.getByText('1 class')).toBeTruthy();
  expect(screen.queryByRole('link', { name: 'Attendance' })).toBeNull();
  expect(screen.queryByRole('link', { name: 'Learner List' })).toBeNull();
});

it('puts Intakes, Sessions, learners and assessment work inside a Course', async () => {
  vi.stubGlobal('fetch', vi.fn(async (path) => {
    if (path === '/api/trainer/course-invitations') return json([
      { id: 1, courseId: 20, courseCode: 'UX200', courseTitle: 'Accepted course', creatorName: 'Creator', status: 'Accepted' },
    ]);
    if (path === '/api/trainer/intakes') return json([
      { id: 10, courseId: 20, status: 'Published' },
      { id: 11, courseId: 99, status: 'Published' },
    ]);
    if (path === '/api/trainer/intakes/10') return json({
      id: 10, courseId: 20, status: 'Published', sessions: [
        { id: 100, label: 'Session one', startsAt: '2026-11-01T01:00:00Z', endsAt: '2026-11-01T03:00:00Z' },
      ],
    });
    if (path === '/api/trainer/intakes/10/learners' || path === '/api/trainer/intakes/10/assessments' || path.startsWith('/api/trainer/certificate-requests')) return json([]);
    return json([]);
  }));

  render(<MemoryRouter initialEntries={['/trainer/courses/20']}>
    <AuthContext.Provider value={auth}><AppRouter /></AuthContext.Provider>
  </MemoryRouter>);

  expect(await screen.findByRole('heading', { name: 'Accepted course' })).toBeTruthy();
  expect(screen.queryByRole('heading', { name: 'Learner list' })).toBeNull();
  fireEvent.click(screen.getByRole('button', { name: 'Show learner list' }));
  expect(screen.getByRole('heading', { name: 'Learner list' })).toBeTruthy();
  expect(await screen.findByRole('button', { name: 'Create assessment' })).toBeTruthy();
  fireEvent.click(screen.getByRole('button', { name: 'Hide learner list' }));
  expect(screen.queryByRole('heading', { name: 'Learner list' })).toBeNull();
  expect(screen.getByRole('link', { name: 'Session one' }).getAttribute('href')).toBe('/trainer/courses/intakes/10/sessions/100');
  expect(screen.queryByText('Course #99')).toBeNull();
});

it('puts delivery, Attendance, one material and recording upload inside a Session', async () => {
  vi.stubGlobal('fetch', vi.fn(async (path) => {
    if (path === '/api/trainer/intakes/10') return json({
      id: 10, courseId: 20, status: 'Published', version: 1, sessions: [
        { id: 100, label: 'Session one', startsAt: '2026-11-01T01:00:00Z', endsAt: '2026-11-01T03:00:00Z', meetingLink: 'https://example.com/class' },
      ],
    });
    if (path === '/api/trainer/intakes/10/learners') return json([]);
    if (path === '/api/trainer/intakes/10/sessions/100/materials' || path === '/api/trainer/intakes/10/sessions/100/recordings') return json([]);
    if (path === '/api/courses') return json([{ id: 20, code: 'UX200', title: 'Accepted course' }]);
    return json([]);
  }));

  render(<MemoryRouter initialEntries={['/trainer/courses/intakes/10/sessions/100']}>
    <AuthContext.Provider value={auth}><AppRouter /></AuthContext.Provider>
  </MemoryRouter>);

  expect(await screen.findByRole('heading', { name: 'Session one' })).toBeTruthy();
  expect(screen.getByRole('heading', { name: 'Attendance' })).toBeTruthy();
  expect(screen.getByRole('heading', { name: 'Session material' })).toBeTruthy();
  expect(screen.getByRole('heading', { name: 'Recording video' })).toBeTruthy();
  expect(screen.getByRole('button', { name: 'Update meeting link' })).toBeTruthy();
  expect(screen.getByRole('button', { name: 'Upload recording' })).toBeTruthy();
});
