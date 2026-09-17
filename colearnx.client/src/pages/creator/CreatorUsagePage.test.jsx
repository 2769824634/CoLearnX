import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { AuthContext } from '../../auth/AuthContext';
import AppRouter from '../../routes/AppRouter';

const auth = {
  token: 'creator-token',
  user: { id: 3, fullName: 'Creator', activeRole: 'Creator', roles: ['Creator'] },
  booting: false,
  isAuthenticated: true,
  activeRole: 'creator',
  roles: ['Creator'],
  logout: vi.fn(),
  switchRole: vi.fn(),
};

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

it('shows recorded material uses and filters the visible records by course', async () => {
  vi.stubGlobal('fetch', vi.fn(async () => new Response(JSON.stringify([
    { id: 1, materialId: 11, materialTitle: 'Safety guide', courseId: 21, courseCode: 'SAFE', courseTitle: 'Safety', trainerId: 31, trainerName: 'Trainer A', usedAt: '2026-09-16T10:00:00Z' },
    { id: 2, materialId: 12, materialTitle: 'Design deck', courseId: 22, courseCode: 'DES', courseTitle: 'Design', trainerId: 32, trainerName: 'Trainer B', usedAt: '2026-09-17T10:00:00Z' },
  ]), { status: 200, headers: { 'Content-Type': 'application/json' } })));

  render(<MemoryRouter initialEntries={['/creator/usage']}><AuthContext.Provider value={auth}><AppRouter /></AuthContext.Provider></MemoryRouter>);

  expect(await screen.findByRole('heading', { name: 'Usage Records' })).toBeTruthy();
  expect(screen.getByText('Safety guide')).toBeTruthy();
  expect(screen.getByText('Design deck')).toBeTruthy();
  fireEvent.change(screen.getByLabelText('Course'), { target: { value: '21' } });
  expect(screen.getByText('Safety guide')).toBeTruthy();
  expect(screen.queryByText('Design deck')).toBeNull();
  expect(screen.getByText('1 recorded use')).toBeTruthy();
});
