import { afterEach, expect, it } from 'vitest';
import { cleanup, render, screen } from '@testing-library/react';
import { MemoryRouter, Outlet, Route, Routes, useLocation } from 'react-router-dom';
import { AuthContext } from './AuthContext';
import { RequireAuth } from './RequireAuth';

function Notice() {
  const location = useLocation();
  return <div>{location.state?.accessNotice || 'missing notice'}</div>;
}

afterEach(() => cleanup());

it('explains the role boundary when redirecting an authenticated user', () => {
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
  expect(screen.getByText(/requires the Trainer workspace/i)).toBeTruthy();
});
