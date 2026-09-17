import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { AuthContext } from '../../auth/AuthContext';
import { MemberDataContext } from './memberDataState';
import MemberProgramsPage from './MemberProgramsPage';

afterEach(() => { cleanup(); vi.unstubAllGlobals(); });

it('loads the selected enrollment learning hub and shows its materials and recordings', async () => {
  vi.stubGlobal('fetch', vi.fn(async (path) => new Response(JSON.stringify(path.endsWith('/materials')
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
