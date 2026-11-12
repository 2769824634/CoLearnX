import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
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

it('keeps an unfinished member on their dashboard and asks for interests there', async () => {
  vi.stubGlobal('fetch', vi.fn(async () => new Response(JSON.stringify([]), { status: 200, headers: { 'Content-Type': 'application/json' } })));
  mount('/member/home', {
    booting: false,
    isAuthenticated: true,
    activeRole: 'member',
    user: { fullName: 'Ada Member', roles: ['Member'], activeRole: 'Member', onboardingCompletedAt: null, onboardingSkippedAt: null },
    token: 'token',
    logout: () => {},
  });
  expect(await screen.findByRole('dialog', { name: 'Your learning interests' })).toBeTruthy();
  expect(screen.getByRole('heading', { name: /Welcome back/ })).toBeTruthy();
  expect(screen.getByRole('link', { name: 'Home' }).getAttribute('href')).toBe('/member/home');
});

it('sends a signed-in member from the site root to their dashboard', async () => {
  vi.stubGlobal('fetch', vi.fn(async (path) => new Response(JSON.stringify(path === '/api/recommendations' ? { items: [] } : []),
    { status: 200, headers: { 'Content-Type': 'application/json' } })));
  mount('/', {
    booting: false, isAuthenticated: true, activeRole: 'member', token: 'token', logout: () => {},
    user: { id: 1, fullName: 'Ada Member', roles: ['Member'], activeRole: 'Member', onboardingCompletedAt: '2026-01-01' },
  });
  expect(await screen.findByRole('heading', { name: /Welcome back/ })).toBeTruthy();
  expect(screen.queryByRole('dialog', { name: 'Your learning interests' })).toBeNull();
});

it('keeps an explicit login URL on the sign-in page', () => {
  mount('/login');
  expect(screen.getByRole('heading', { name: 'Login to your account' })).toBeTruthy();
});

it('opens the learner path and displays its stages and courses', async () => {
  vi.stubGlobal('fetch', vi.fn(async (path) => {
    const body = path === '/api/learning-paths/my' ? [{ id: 4, subjectId: 1, name: 'Software Development', graduated: false,
      stages: [
        { number: 1, name: 'Foundation', requiredCourseCount: 1, completedCourseCount: 0, completed: false,
          courses: [{ id: 42, code: 'DEV-1', title: 'Web Basics', credits: 40, hasClass: true, completed: false }] },
        { number: 2, name: 'Intermediate', requiredCourseCount: 1, completedCourseCount: 0, completed: false, suggestedPrerequisite: 'Foundation', courses: [] },
        { number: 3, name: 'Advanced', requiredCourseCount: 1, completedCourseCount: 0, completed: false, suggestedPrerequisite: 'Foundation, Intermediate', courses: [] },
      ] }] : path === '/api/notifications/my' ? { unreadCount: 0, items: [] } : [];
    return new Response(JSON.stringify(body), { status: 200, headers: { 'Content-Type': 'application/json' } });
  }));
  mount('/member/learning-paths', {
    booting: false, isAuthenticated: true, activeRole: 'member', token: 'token', logout: () => {},
    user: { id: 1, fullName: 'Ada Member', roles: ['Member'], activeRole: 'Member', onboardingCompletedAt: '2026-01-01' },
  });
  expect(await screen.findByRole('heading', { name: 'My Learning Paths' })).toBeTruthy();
  expect(await screen.findByRole('heading', { name: 'Software Development' })).toBeTruthy();
  expect(screen.getByRole('link', { name: /Web Basics/ }).getAttribute('href')).toBe('/member/courses/42');
  expect(screen.getByText(/Suggested first: Foundation$/)).toBeTruthy();
});
