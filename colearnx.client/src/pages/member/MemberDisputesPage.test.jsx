import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { AuthContext } from '../../auth/AuthContext';
import { MemberDataContext } from './memberDataState';
import MemberDisputesPage from './MemberDisputesPage';

afterEach(() => { cleanup(); vi.unstubAllGlobals(); });

it('submits a reason for the selected enrollment and displays the resulting status', async () => {
  const fetch = vi.fn(async (_path, options) => new Response(JSON.stringify(options.method === 'POST'
    ? { id: 5, enrollmentId: 3, courseCode: 'UX101', status: 'Open', reason: 'Session cancelled' }
    : []), { status: options.method === 'POST' ? 201 : 200, headers: { 'Content-Type': 'application/json' } }));
  vi.stubGlobal('fetch', fetch);
  const auth = { user: { id: 1, fullName: 'Learner', roles: ['Member'] }, token: 'member-token', logout: vi.fn() };
  const member = { state: { enrolled: [{ enrollmentId: 3, courseCode: 'UX101', courseTitle: 'UX', status: 'active' }] }, showToast: vi.fn() };

  render(<MemoryRouter initialEntries={['/member/disputes?enrollmentId=3']}><AuthContext.Provider value={auth}><MemberDataContext.Provider value={member}><MemberDisputesPage /></MemberDataContext.Provider></AuthContext.Provider></MemoryRouter>);

  await screen.findByText('No disputes yet.');
  fireEvent.change(screen.getByLabelText('Reason'), { target: { value: 'Session cancelled' } });
  fireEvent.click(screen.getByRole('button', { name: 'Submit dispute' }));
  expect(await screen.findByText('Open')).toBeTruthy();
  expect(fetch).toHaveBeenCalledWith('/api/disputes', expect.objectContaining({ method: 'POST' }));
  expect(JSON.parse(fetch.mock.calls.find(([path]) => path === '/api/disputes')[1].body)).toEqual({ enrollmentId: 3, reason: 'Session cancelled' });
});
