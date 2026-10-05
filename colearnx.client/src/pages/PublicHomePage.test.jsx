import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, within } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { AuthContext } from '../auth/AuthContext';
import PublicHomePage from './PublicHomePage';
import PublicCatalogPage from './PublicCatalogPage';
import PublicCourseDetailPage from './PublicCourseDetailPage';

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

const guestAuth = { booting: false, isAuthenticated: false, activeRole: null, user: null, token: null, logout: () => {} };

function courseList() {
  return [{
    id: 15,
    code: 'INFT 2002',
    title: 'Frontend React Bootcamp',
    trainerName: 'Gu Yincheng',
    trainerNames: ['Gu Yincheng'],
    creatorName: 'Zou Ruiqi',
    creditCost: 20,
    level: 'Beginner',
    category: 'Programming',
    isFeatured: true,
    inWishlist: false,
    averageStars: 4.3,
    ratingCount: 3,
  }];
}

function stubCourses(detail) {
  vi.stubGlobal('fetch', vi.fn(async (path) => {
    const url = String(path);
    if (url.startsWith('/api/notifications')) {
      return new Response(JSON.stringify({ error: 'should not fetch notifications' }), { status: 500 });
    }
    if (url === '/api/courses/15' || url.startsWith('/api/courses/15?')) {
      return new Response(JSON.stringify(detail || {
        ...courseList()[0],
        description: 'Build interfaces with React.',
        learningOutcomes: ['Hooks'],
        sessions: [],
        alreadyEnrolled: false,
        status: 'Published',
      }), { status: 200, headers: { 'Content-Type': 'application/json' } });
    }
    if (url.startsWith('/api/courses')) {
      return new Response(JSON.stringify(courseList()), { status: 200, headers: { 'Content-Type': 'application/json' } });
    }
    return new Response(JSON.stringify({}), { status: 200, headers: { 'Content-Type': 'application/json' } });
  }));
}

function mountHome(auth = guestAuth) {
  stubCourses();
  return render(
    <MemoryRouter>
      <AuthContext.Provider value={auth}>
        <PublicHomePage />
      </AuthContext.Provider>
    </MemoryRouter>,
  );
}

it('does not treat a restoring session as logged out on the homepage', () => {
  mountHome({
    booting: true,
    isAuthenticated: false,
    activeRole: null,
    token: 'token',
    user: null,
    logout: () => {},
  });
  expect(screen.getByRole('status').textContent).toBe('Loading session…');
  expect(screen.queryByRole('link', { name: 'Log in' })).toBeNull();
  expect(screen.queryByRole('link', { name: 'Sign up' })).toBeNull();
  expect(screen.queryByRole('button', { name: 'Log out' })).toBeNull();
});

it('shows the public homepage courses and Log in / Sign up without member APIs', async () => {
  mountHome();
  expect(screen.getByRole('heading', { name: 'CoLearnX' })).toBeTruthy();
  expect(screen.getByRole('link', { name: 'Log in' }).getAttribute('href')).toBe('/login');
  expect(screen.getByRole('link', { name: 'Sign up' }).getAttribute('href')).toBe('/register');
  expect(screen.getByRole('link', { name: 'Courses' }).getAttribute('href')).toBe('/courses');
  expect(screen.queryByRole('button', { name: 'Log out' })).toBeNull();
  expect(screen.queryByRole('button', { name: /Notifications/ })).toBeNull();
  expect(await screen.findByRole('heading', { name: 'Frontend React Bootcamp' })).toBeTruthy();
  const paths = globalThis.fetch.mock.calls.map(([path]) => String(path));
  expect(paths.some((path) => path.includes('/api/notifications'))).toBe(false);
});

it('shows a signed-in member as signed in on the homepage, including Courses', async () => {
  mountHome({
    booting: false,
    isAuthenticated: true,
    activeRole: 'member',
    token: 'token',
    logout: () => {},
    user: { fullName: 'Huang Yousheng', roles: ['Member'], activeRole: 'Member', creditBalance: 0, heldCredits: 0 },
  });
  expect(screen.getByRole('button', { name: 'Log out' })).toBeTruthy();
  expect(screen.getByText('Huang Yousheng')).toBeTruthy();
  expect(screen.getByRole('link', { name: 'Courses' }).getAttribute('href')).toBe('/member/courses');
  expect(screen.getByRole('link', { name: 'Payment' }).getAttribute('href')).toBe('/member/payment');
  expect(screen.queryByRole('link', { name: 'Log in' })).toBeNull();
  expect(screen.queryByRole('link', { name: 'Sign up' })).toBeNull();
  expect(await screen.findByRole('heading', { name: 'Frontend React Bootcamp' })).toBeTruthy();
});

it('asks guests to sign in when they open member-only navigation', async () => {
  mountHome();
  fireEvent.click(screen.getByRole('button', { name: 'Payment' }));
  const dialog = await screen.findByRole('dialog', { name: 'Sign in to continue' });
  expect(dialog).toBeTruthy();
  expect(within(dialog).getByRole('link', { name: 'Log in' }).getAttribute('href')).toBe('/login');
  expect(within(dialog).getByRole('link', { name: 'Sign up' }).getAttribute('href')).toBe('/register');
});

it('lets guests open the public catalog and course details, but not wishlist', async () => {
  stubCourses();
  render(
    <MemoryRouter initialEntries={['/courses']}>
      <AuthContext.Provider value={guestAuth}>
        <Routes>
          <Route path="/courses" element={<PublicCatalogPage />} />
          <Route path="/courses/:courseId" element={<PublicCourseDetailPage />} />
        </Routes>
      </AuthContext.Provider>
    </MemoryRouter>,
  );
  expect(await screen.findByRole('heading', { name: 'Frontend React Bootcamp' })).toBeTruthy();
  fireEvent.click(screen.getByRole('button', { name: '+ Wishlist' }));
  expect(await screen.findByRole('dialog', { name: 'Sign in to continue' })).toBeTruthy();
  fireEvent.click(screen.getByRole('button', { name: 'Close' }));
  fireEvent.click(screen.getByRole('button', { name: 'View Details' }));
  expect(await screen.findByText('Build interfaces with React.')).toBeTruthy();
});
