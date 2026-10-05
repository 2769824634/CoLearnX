import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { AuthContext } from '../auth/AuthContext';
import AdminAuthContext from '../auth/AdminAuthContext';
import AppRouter from './AppRouter';

afterEach(() => { cleanup(); vi.unstubAllGlobals(); });

function mount(path, auth = { booting: false, isAuthenticated: false, activeRole: null }) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <AuthContext.Provider value={auth}>
        <AdminAuthContext.Provider value={{ booting: false, isAuthenticated: false }}>
          <AppRouter />
        </AdminAuthContext.Provider>
      </AuthContext.Provider>
    </MemoryRouter>,
  );
}

it('opens the public home at the site root instead of the login form', () => {
  vi.stubGlobal('fetch', vi.fn(async () => new Response(JSON.stringify([]), { status: 200, headers: { 'Content-Type': 'application/json' } })));
  mount('/');
  expect(screen.getByRole('heading', { name: 'CoLearnX' })).toBeTruthy();
  expect(screen.getByRole('link', { name: 'Log in' }).getAttribute('href')).toBe('/login');
  expect(screen.getByRole('link', { name: 'Sign up' }).getAttribute('href')).toBe('/register');
  expect(screen.getByRole('link', { name: 'Courses' }).getAttribute('href')).toBe('/courses');
  expect(screen.queryByRole('heading', { name: 'Login to your account' })).toBeNull();
});

it('does not show a guest catalog while restoring a member session', () => {
  vi.stubGlobal('fetch', vi.fn(async () => new Response(JSON.stringify([]), { status: 200, headers: { 'Content-Type': 'application/json' } })));
  mount('/courses', {
    booting: true,
    isAuthenticated: false,
    activeRole: null,
    token: 'token',
    user: null,
  });
  expect(screen.getByText('Loading session…')).toBeTruthy();
  expect(screen.queryByRole('link', { name: 'Log in' })).toBeNull();
  expect(screen.queryByRole('heading', { name: 'Program Catalog' })).toBeNull();
});

it('lets a guest browse the public catalog without signing in', async () => {
  vi.stubGlobal('fetch', vi.fn(async () => new Response(JSON.stringify([{
    id: 3, code: 'INFT 2002', title: 'Frontend React Bootcamp', trainerName: 'Ada', creditCost: 20,
    level: 'Beginner', category: 'Programming', isFeatured: false, inWishlist: false,
  }]), { status: 200, headers: { 'Content-Type': 'application/json' } })));
  mount('/courses');
  expect(await screen.findByRole('heading', { name: 'Frontend React Bootcamp' })).toBeTruthy();
  expect(screen.queryByRole('heading', { name: 'Login to your account' })).toBeNull();
});

it('sends an unfinished member from workspace home to the site homepage', () => {
  vi.stubGlobal('fetch', vi.fn(async () => new Response(JSON.stringify([]), { status: 200, headers: { 'Content-Type': 'application/json' } })));
  mount('/member/home', {
    booting: false,
    isAuthenticated: true,
    activeRole: 'member',
    user: { fullName: 'Ada Member', roles: ['Member'], activeRole: 'Member', onboardingCompletedAt: null, onboardingSkippedAt: null },
    token: 'token',
    logout: () => {},
  });
  expect(screen.getByRole('heading', { name: 'CoLearnX' })).toBeTruthy();
  expect(screen.getByRole('button', { name: 'Log out' })).toBeTruthy();
  expect(screen.getByRole('link', { name: 'Courses' }).getAttribute('href')).toBe('/member/courses');
  expect(screen.queryByRole('link', { name: 'Log in' })).toBeNull();
  expect(screen.queryByText('Member Dashboard')).toBeNull();
});

it('keeps an explicit login URL on the sign-in page', () => {
  mount('/login');
  expect(screen.getByRole('heading', { name: 'Login to your account' })).toBeTruthy();
});
