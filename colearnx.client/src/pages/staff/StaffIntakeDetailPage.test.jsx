import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { staffCoursesApi } from '../../api';
import StaffIntakeDetailPage from './StaffIntakeDetailPage';

vi.mock('../../api', () => ({ staffCoursesApi: { getIntake: vi.fn(), updateTeachingDates: vi.fn() } }));
vi.mock('../../auth/useStaffAuth', () => ({ default: () => ({ token: 'staff-token' }) }));
afterEach(() => { cleanup(); vi.clearAllMocks(); });

it('edits and refreshes dates on the staff class detail page', async () => {
  const intake = { id: 12, startsAt: '2027-01-10T00:00:00Z', endsAt: '2027-01-13T00:00:00Z', version: 'staff-version', status: 'Draft', sessions: [] };
  staffCoursesApi.getIntake.mockResolvedValue(intake);
  staffCoursesApi.updateTeachingDates.mockImplementation(async (_token, _id, body) => ({ ...intake, ...body, version: 'next' }));
  render(<MemoryRouter initialEntries={['/staff/intakes/12']}><Routes><Route path="/staff/intakes/:courseIntakeId" element={<StaffIntakeDetailPage />} /></Routes></MemoryRouter>);
  fireEvent.click(await screen.findByRole('button', { name: 'Edit teaching dates' }));
  fireEvent.change(screen.getByLabelText('Last teaching date'), { target: { value: '2027-01-15' } });
  fireEvent.click(screen.getByRole('button', { name: 'Save dates' }));
  await waitFor(() => expect(staffCoursesApi.updateTeachingDates).toHaveBeenCalledWith('staff-token', 12,
    expect.objectContaining({ version: 'staff-version', endsAt: new Date('2027-01-16T00:00:00').toISOString() })));
  expect(await screen.findByText('Teaching dates saved.')).toBeTruthy();
  expect(screen.getByText('2027-01-10 → 2027-01-15')).toBeTruthy();
});
