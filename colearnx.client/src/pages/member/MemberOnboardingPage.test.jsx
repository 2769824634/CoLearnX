import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { AuthContext } from '../../auth/AuthContext';
import PublicHomePage from '../PublicHomePage';
import MemberOnboardingPage from './MemberOnboardingPage';

afterEach(() => { cleanup(); vi.unstubAllGlobals(); });

function mountOnboarding() {
  const refreshUser = vi.fn();
  vi.stubGlobal('fetch', vi.fn(async (path, options = {}) => {
    let body = {};
    if (path === '/api/interests') body = [{ id: 1, slug: 'art', name: 'Art', children: [{ id: 2, slug: 'sketching', name: 'Sketching', children: [] }] }];
    if (path === '/api/users/me/interests' && options.method === 'GET') body = { interestIds: [], learningGoals: null };
    if (path === '/api/users/me/interests' && options.method === 'PUT') body = { interestIds: [2], learningGoals: 'hobby', onboardingCompletedAt: '2026-09-29T00:00:00Z' };
    if (String(path).startsWith('/api/courses')) body = [];
    return new Response(JSON.stringify(body), { status: 200, headers: { 'Content-Type': 'application/json' } });
  }));
  render(
    <AuthContext.Provider value={{ booting: false, isAuthenticated: true, activeRole: 'member', user: { id: 1, fullName: 'Member', roles: ['Member'], activeRole: 'Member', creditBalance: 0, heldCredits: 0 }, token: 'token', refreshUser, logout: vi.fn() }}>
      <MemoryRouter initialEntries={['/member/onboarding']}>
        <Routes>
          <Route path="/member/onboarding" element={<MemberOnboardingPage />} />
          <Route path="/" element={<PublicHomePage />} />
        </Routes>
      </MemoryRouter>
    </AuthContext.Provider>,
  );
  return refreshUser;
}

it('runs interest selection in a stepped dialog over the site homepage', async () => {
  mountOnboarding();
  const dialog = await screen.findByRole('dialog', { name: 'Your learning interests' });
  expect(dialog).toBeTruthy();
  expect(screen.getByText('Step 1 of 3 · You can change this later in Account.')).toBeTruthy();
  expect(screen.getByRole('heading', { name: 'CoLearnX' })).toBeTruthy();
  expect(screen.getByRole('button', { name: 'Log out' })).toBeTruthy();
  expect(screen.queryByRole('link', { name: 'Log in' })).toBeNull();
  expect(screen.queryByRole('link', { name: 'Sign up' })).toBeNull();
  expect(screen.getByRole('navigation', { name: 'Member navigation' })).toBeTruthy();
  expect(screen.queryByRole('navigation', { name: 'Site navigation' })).toBeNull();
});

it('saves the selected leaf after the three onboarding steps and opens the site homepage', async () => {
  const refreshUser = mountOnboarding();

  expect(await screen.findByRole('heading', { name: 'What brings you here?' })).toBeTruthy();
  fireEvent.click(screen.getByLabelText('Hobby'));
  fireEvent.click(screen.getByRole('button', { name: 'Next' }));
  fireEvent.click(screen.getByLabelText('Art'));
  fireEvent.click(screen.getByRole('button', { name: 'Next' }));
  fireEvent.click(screen.getByLabelText('Sketching'));
  fireEvent.click(screen.getByRole('button', { name: 'Save interests' }));

  await waitFor(() => {
    expect(screen.queryByRole('dialog', { name: 'Your learning interests' })).toBeNull();
  });
  expect(screen.getByRole('heading', { name: 'CoLearnX' })).toBeTruthy();
  expect(screen.getByRole('button', { name: 'Log out' })).toBeTruthy();
  expect(screen.queryByRole('link', { name: 'Log in' })).toBeNull();
  const save = globalThis.fetch.mock.calls.find(([path, options]) => path === '/api/users/me/interests' && options.method === 'PUT');
  expect(JSON.parse(save[1].body)).toEqual({ interestIds: [2], learningGoals: 'hobby', skip: false });
  expect(refreshUser).toHaveBeenCalledWith(expect.objectContaining({ onboardingCompletedAt: '2026-09-29T00:00:00Z' }));
});
