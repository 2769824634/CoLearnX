import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import AdminUserDetail from './AdminUserDetail';

const overview = vi.hoisted(() => ({ userActivity: vi.fn() }));
const later = vi.hoisted(() => ({ user: vi.fn() }));
vi.mock('../../auth/useAdminAuth', () => ({ default: () => ({ token: 'profile-test', logout: vi.fn() }) }));
vi.mock('../../api', () => ({ adminOverviewApi: overview }));
vi.mock('../../api/adminLaterPhase', () => ({ adminLaterPhaseApi: later }));
afterEach(() => { cleanup(); vi.resetAllMocks(); });

it('shows a trainer profile with certified specialties as tags', async () => {
  later.user.mockResolvedValue({ id: 139, fullName: 'Adrian Costa', displayName: 'Adrian', email: 'adrian@colearnx.test', roles: ['Trainer'],
    isActive: true, emailVerifiedAt: '2026-11-12T00:00:00Z', createdAt: '2026-11-12T00:00:00Z', creditBalance: 0, heldCredits: 0,
    trainerHeadline: 'Audio engineer', learningGoals: 'hidden for trainers' });
  overview.userActivity.mockResolvedValue({ roles: ['Trainer'], certified: true, specialties: ['Media Production', 'Music'], teachingClasses: [] });
  render(<MemoryRouter><AdminUserDetail userId={139} /></MemoryRouter>);

  fireEvent.click(await screen.findByRole('button', { name: 'Profile' }));
  expect(screen.getByText('Trainer profile')).toBeTruthy();
  expect(screen.getByText('Audio engineer')).toBeTruthy();
  expect(screen.getByText('Certified specialties')).toBeTruthy();
  expect(screen.getAllByText('Music').length).toBeGreaterThan(0);
  expect(screen.queryByText('Learning goals')).toBeNull();
  expect(screen.queryByText('Specialisations')).toBeNull();
});
