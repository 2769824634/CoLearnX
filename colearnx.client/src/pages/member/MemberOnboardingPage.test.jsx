import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { AuthContext } from '../../auth/AuthContext';
import MemberOnboardingPage from './MemberOnboardingPage';

afterEach(() => { cleanup(); vi.unstubAllGlobals(); });

it('saves the selected leaf after the three onboarding steps', async () => {
  const refreshUser = vi.fn();
  vi.stubGlobal('fetch', vi.fn(async (path, options = {}) => {
    let body = {};
    if (path === '/api/interests') body = [{ id: 1, slug: 'art', name: 'Art', children: [{ id: 2, slug: 'sketching', name: 'Sketching', children: [] }] }];
    if (path === '/api/users/me/interests' && options.method === 'GET') body = { interestIds: [], learningGoals: null };
    if (path === '/api/users/me/interests' && options.method === 'PUT') body = { interestIds: [2], learningGoals: 'hobby', onboardingCompletedAt: '2026-09-29T00:00:00Z' };
    return new Response(JSON.stringify(body), { status: 200, headers: { 'Content-Type': 'application/json' } });
  }));
  render(<AuthContext.Provider value={{ user: { id: 1, fullName: 'Member', roles: ['Member'], activeRole: 'Member' }, token: 'token', refreshUser, logout: vi.fn() }}>
    <MemoryRouter initialEntries={['/member/onboarding']}><Routes>
      <Route path="/member/onboarding" element={<MemberOnboardingPage />} />
      <Route path="/member/home" element={<h1>Member home</h1>} />
    </Routes></MemoryRouter>
  </AuthContext.Provider>);

  expect(await screen.findByRole('heading', { name: 'What brings you here?' })).toBeTruthy();
  fireEvent.click(screen.getByLabelText('Hobby'));
  fireEvent.click(screen.getByRole('button', { name: 'Next' }));
  fireEvent.click(screen.getByLabelText('Art'));
  fireEvent.click(screen.getByRole('button', { name: 'Next' }));
  fireEvent.click(screen.getByLabelText('Sketching'));
  fireEvent.click(screen.getByRole('button', { name: 'Save interests' }));

  expect(await screen.findByRole('heading', { name: 'Member home' })).toBeTruthy();
  const save = globalThis.fetch.mock.calls.find(([path, options]) => path === '/api/users/me/interests' && options.method === 'PUT');
  expect(JSON.parse(save[1].body)).toEqual({ interestIds: [2], learningGoals: 'hobby', skip: false });
  expect(refreshUser).toHaveBeenCalledWith(expect.objectContaining({ onboardingCompletedAt: '2026-09-29T00:00:00Z' }));
});
