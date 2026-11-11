import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { AuthContext } from '../../auth/AuthContext';
import { MemberDataContext } from './memberDataState';
import MemberCatalogPage from './MemberCatalogPage';

vi.mock('../../components/MemberShell', () => ({ default: ({ title, children }) => <main><h1>{title}</h1>{children}</main> }));
afterEach(cleanup);

const course = (id, title, matchesInterests) => ({ id, code: `C ${id}`, title, level: 'Beginner', credits: 20, trainerNames: [], ratingCount: 0, matchesInterests });

it('keeps the server order and labels courses that match the member interests', () => {
  const state = { loading: false, loadError: '', wishlist: [], courses: [course(2, 'Web basics', true), course(1, 'Accounting basics', false)] };
  render(
    <MemoryRouter>
      <AuthContext.Provider value={{ token: 't', user: { id: 1, roles: ['Member'] } }}>
        <MemberDataContext.Provider value={{ state, toggleWish: vi.fn(), reload: vi.fn() }}>
          <MemberCatalogPage />
        </MemberDataContext.Provider>
      </AuthContext.Provider>
    </MemoryRouter>,
  );
  const titles = screen.getAllByRole('heading', { level: 4 }).map((item) => item.textContent);
  expect(titles).toEqual(['Web basics', 'Accounting basics']);
  expect(screen.getAllByText('Matches your interests')).toHaveLength(1);
  expect(screen.getByText('Matches your interests').closest('article').textContent).toContain('Web basics');
});
