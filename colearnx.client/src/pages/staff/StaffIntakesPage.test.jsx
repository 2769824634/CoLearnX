import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { staffCoursesApi } from '../../api';
import useStaffAuth from '../../auth/useStaffAuth';
import StaffIntakesPage from './StaffIntakesPage';

vi.mock('../../api', () => ({
  staffCoursesApi: {
    list: vi.fn(),
    listIntakes: vi.fn(),
  },
}));
vi.mock('../../auth/useStaffAuth');

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

it('shows platform classes with an entry point to open a class', async () => {
  useStaffAuth.mockReturnValue({ token: 'staff-token' });
  staffCoursesApi.list.mockResolvedValue([{ id: 7, code: 'PLT-7', title: 'Platform course' }]);
  staffCoursesApi.listIntakes.mockResolvedValue([{
    id: 12, courseId: 7, startsAt: '2026-11-10T00:00:00Z', endsAt: '2026-11-11T00:00:00Z', status: 'Draft',
  }]);

  render(<MemoryRouter><StaffIntakesPage /></MemoryRouter>);

  expect(await screen.findByText('Platform course')).toBeTruthy();
  expect(screen.getByRole('link', { name: 'Open a class' }).getAttribute('href')).toBe('/staff/intakes/new');
});
