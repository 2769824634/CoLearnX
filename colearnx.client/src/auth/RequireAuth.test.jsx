import { afterEach, expect, it } from 'vitest';
import { cleanup, render, screen } from '@testing-library/react';
import { MemoryRouter, Outlet, Route, Routes, useLocation } from 'react-router-dom';
import { AuthContext } from './AuthContext';
import { RequireAuth } from './RequireAuth';

function Notice() {
  const location = useLocation();
  return <div>{location.state ? 'unexpected state' : 'own home, no state'}</div>;
}

afterEach(() => cleanup());

it('takes an account that opens another role\'s page to its own home without a notice', () => {
  render(
    <MemoryRouter initialEntries={['/trainer/home']}>
      <AuthContext.Provider value={{ booting: false, isAuthenticated: true, activeRole: 'member' }}>
        <Routes>
          <Route element={<RequireAuth role="trainer" />}><Route path="/trainer/home" element={<Outlet />} /></Route>
          <Route path="/member/home" element={<Notice />} />
        </Routes>
      </AuthContext.Provider>
    </MemoryRouter>,
  );
  expect(screen.getByText('own home, no state')).toBeTruthy();
});

it('keeps an uncertified Trainer on the certification page', () => {
  render(
    <MemoryRouter initialEntries={['/trainer/home']}>
      <AuthContext.Provider value={{ booting: false, isAuthenticated: true, activeRole: 'trainer', user: { certifiedAt: null } }}>
        <Routes>
          <Route element={<RequireAuth role="trainer" />}>
            <Route path="/trainer/home" element={<div>Trainer home</div>} />
            <Route path="/trainer/certification" element={<div>Certification page</div>} />
          </Route>
        </Routes>
      </AuthContext.Provider>
    </MemoryRouter>,
  );
  expect(screen.getByText('Certification page')).toBeTruthy();
  expect(screen.queryByText('Trainer home')).toBeNull();
});

it('lets a certified Trainer into the workspace', () => {
  render(
    <MemoryRouter initialEntries={['/trainer/home']}>
      <AuthContext.Provider value={{ booting: false, isAuthenticated: true, activeRole: 'trainer', user: { certifiedAt: '2026-11-11T00:00:00Z' } }}>
        <Routes>
          <Route element={<RequireAuth role="trainer" />}>
            <Route path="/trainer/home" element={<div>Trainer home</div>} />
          </Route>
        </Routes>
      </AuthContext.Provider>
    </MemoryRouter>,
  );
  expect(screen.getByText('Trainer home')).toBeTruthy();
});
