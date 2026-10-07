import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, render, screen, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { AuthContext } from '../../auth/AuthContext';
import { MemberDataContext } from './memberDataState';
import MemberHomePage from './MemberHomePage';

vi.mock('../../components/MemberShell', () => ({ default: ({ title, subtitle, children }) => <main><h1>{title}</h1><p>{subtitle}</p>{children}</main> }));
afterEach(() => { cleanup(); vi.unstubAllGlobals(); });

const baseState = {
  loading: false,
  loadError: '',
  credits: 120,
  heldCredits: 10,
  user: { displayName: 'Yousheng' },
  enrolled: [],
  wishlist: [],
  certificates: [{ id: 1 }],
  courses: [],
};

const enrolled = [
  { enrollmentId: 7, status: 'active', courseCode: 'INFT 2002', courseTitle: 'Frontend React Bootcamp', trainer: 'Gu Yincheng', progress: 40 },
  { enrollmentId: 8, status: 'active', courseCode: 'INFT 3030', courseTitle: 'Cybersecurity Essentials', trainer: 'Gu Yincheng', progress: 12 },
  { enrollmentId: 9, status: 'completed', courseCode: 'INFT 2051', courseTitle: 'UI/UX Design Fundamentals', progress: 100 },
];

const recommendation = { courseId: 31, code: 'DES 1001', title: 'Visual Storytelling', interests: [{ id: 1, name: 'Design' }], creditCost: 20, level: 'Beginner', ratingCount: 0 };

function mount(state) {
  vi.stubGlobal('fetch', vi.fn(async () => new Response(JSON.stringify({ items: [recommendation] }), { headers: { 'Content-Type': 'application/json' } })));
  render(
    <MemoryRouter>
      <AuthContext.Provider value={{ token: 't', user: { id: 1, roles: ['Member'] } }}>
        <MemberDataContext.Provider value={{ state: { ...baseState, ...state }, toggleWish: vi.fn(), reload: vi.fn() }}>
          <MemberHomePage />
        </MemberDataContext.Provider>
      </AuthContext.Provider>
    </MemoryRouter>,
  );
}

it('leads with the next course to continue, naming its trainer and progress', () => {
  mount({ enrolled });
  expect(screen.getByRole('heading', { level: 1, name: 'Welcome back, Yousheng' })).toBeTruthy();
  const upNext = screen.getByRole('region', { name: 'Frontend React Bootcamp' });
  expect(within(upNext).getByText('with Gu Yincheng')).toBeTruthy();
  expect(within(upNext).getByRole('progressbar', { name: 'Frontend React Bootcamp progress' }).getAttribute('aria-valuenow')).toBe('40');
  expect(within(upNext).getByRole('link', { name: 'Continue learning' }).getAttribute('href')).toBe('/member/programs?tab=active&enrollmentId=7');
  const others = screen.getByRole('list', { name: 'Also in progress' });
  expect(within(others).getByText('Cybersecurity Essentials')).toBeTruthy();
});

it('leaves credits to the top-bar wallet and summarises learning instead', () => {
  mount({ enrolled });
  expect(screen.queryByText(/Available credits/i)).toBeNull();
  const summary = screen.getByRole('complementary', { name: 'Your learning summary' });
  expect(within(summary).getByText('Active programs').nextElementSibling.textContent).toBe('2');
  expect(within(summary).getByText('Completed').nextElementSibling.textContent).toBe('1');
  expect(within(summary).getByText('Certificates').nextElementSibling.textContent).toBe('1');
});

it('invites a learner without active programs to browse the catalog', () => {
  mount({ enrolled: [] });
  expect(screen.getByRole('heading', { name: 'No active programs' })).toBeTruthy();
  expect(screen.getByRole('link', { name: 'Browse Catalog' }).getAttribute('href')).toBe('/member/courses');
});

it('gives each recommended course a cover and keeps its detail link', async () => {
  mount({ enrolled });
  const card = (await screen.findByRole('heading', { name: 'Visual Storytelling' })).closest('article');
  expect(card.querySelector('.course-cover')).toBeTruthy();
  expect(within(card).getByRole('button', { name: 'View Details' })).toBeTruthy();
});
