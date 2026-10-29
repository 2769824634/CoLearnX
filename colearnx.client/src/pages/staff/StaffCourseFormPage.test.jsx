import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { staffCoursesApi } from '../../api';
import useStaffAuth from '../../auth/useStaffAuth';
import StaffCourseFormPage from './StaffCourseFormPage';

vi.mock('../../api', () => ({
  staffCoursesApi: {
    options: vi.fn(),
    interests: vi.fn(),
    get: vi.fn(),
  },
}));
vi.mock('../../auth/useStaffAuth');
vi.mock('../creator/CreatorCourseFormPage', () => ({
  InterestPicker: ({ tree }) => <div>Interest choices: {tree.length}</div>,
}));

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

it('loads course interests through the Staff-authenticated API', async () => {
  useStaffAuth.mockReturnValue({ token: 'staff-token' });
  staffCoursesApi.options.mockResolvedValue({
    courseLevels: [], learningPaths: [], courseSubjects: [], priceCells: [],
  });
  staffCoursesApi.interests.mockResolvedValue([{ id: 1, name: 'Technology', children: [] }]);

  render(<MemoryRouter initialEntries={['/staff/courses/new']}><Routes>
    <Route path="/staff/courses/new" element={<StaffCourseFormPage />} />
  </Routes></MemoryRouter>);

  expect(await screen.findByRole('heading', { name: 'Create course' })).toBeTruthy();
  await waitFor(() => expect(staffCoursesApi.interests).toHaveBeenCalledWith('staff-token', expect.any(AbortSignal)));
  expect(await screen.findByText('Interest choices: 1')).toBeTruthy();
});
