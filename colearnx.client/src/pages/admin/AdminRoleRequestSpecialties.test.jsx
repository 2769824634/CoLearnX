import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { adminRoleRequestsApi } from '../../api';
import AdminRoleRequestsPage from './AdminRoleRequestsPage';

vi.mock('../../auth/useAdminAuth', () => ({ default: () => ({ token: 'admin-token', logout: vi.fn() }) }));
vi.mock('../../api', () => ({ adminRoleRequestsApi: { list: vi.fn(), review: vi.fn(), specialties: vi.fn(), downloadResume: vi.fn(), downloadIdDocument: vi.fn() } }));

const options = [
  { id: 1, name: 'Software Development', area: 'DigitalTechnology' },
  { id: 2, name: 'Data and Artificial Intelligence', area: 'DigitalTechnology' },
  { id: 5, name: 'Management', area: 'BusinessManagement' },
  { id: 6, name: 'Accounting and Finance', area: 'BusinessManagement' },
  { id: 7, name: 'Marketing', area: 'BusinessManagement' },
  { id: 9, name: 'Languages', area: 'HumanitiesHealthCreative' },
  { id: 11, name: 'Health and Care', area: 'HumanitiesHealthCreative' },
];
const applicant = {
  id: 3, userId: 30, userFullName: 'Trainer Applicant', userEmail: 'applicant@example.test', requestedRole: 'Trainer', status: 'Pending',
  createdAt: '2026-11-12T03:00:00', degreeOrResumePath: 'private/resume.pdf', idDocumentPath: 'private/id.pdf',
  specialties: ['Software Development', 'Management'], specialtyIds: [1, 5],
};

beforeEach(() => {
  adminRoleRequestsApi.list.mockResolvedValue([applicant]);
  adminRoleRequestsApi.specialties.mockResolvedValue(options);
  adminRoleRequestsApi.review.mockResolvedValue({ roleRequest: { ...applicant, status: 'Approved' }, alreadyReviewed: false });
});
afterEach(() => { cleanup(); vi.resetAllMocks(); });

async function openReview() {
  render(<AdminRoleRequestsPage />);
  fireEvent.click(await screen.findByRole('button', { name: 'Review' }));
  return screen.findByRole('group', { name: /Specialties to certify/ });
}

it('shows the applicant\'s specialties ticked and lets the admin correct them before approving', async () => {
  const group = await openReview();
  expect(within(group).getByLabelText('Software Development').checked).toBe(true);
  expect(within(group).getByLabelText('Management').checked).toBe(true);
  expect(within(group).getByLabelText('Marketing').checked).toBe(false);

  fireEvent.click(within(group).getByLabelText('Management'));
  fireEvent.click(within(group).getByLabelText('Marketing'));
  expect(screen.getByText(/Applicant chose: Software Development, Management/)).toBeTruthy();
  fireEvent.click(screen.getByRole('button', { name: 'Approve request' }));

  await waitFor(() => expect(adminRoleRequestsApi.review).toHaveBeenCalledWith('admin-token', 3, 'Approve', '', [1, 7]));
});

it('sends no correction when the admin leaves the specialties as chosen', async () => {
  await openReview();
  fireEvent.click(screen.getByRole('button', { name: 'Approve request' }));
  await waitFor(() => expect(adminRoleRequestsApi.review).toHaveBeenCalledWith('admin-token', 3, 'Approve', '', null));
});

it('requires at least one specialty and allows at most five', async () => {
  const group = await openReview();
  fireEvent.click(within(group).getByLabelText('Software Development'));
  fireEvent.click(within(group).getByLabelText('Management'));
  fireEvent.click(screen.getByRole('button', { name: 'Approve request' }));
  expect((await screen.findByRole('alert')).textContent).toMatch(/Choose at least one specialty/);
  expect(adminRoleRequestsApi.review).not.toHaveBeenCalled();

  for (const name of ['Software Development', 'Data and Artificial Intelligence', 'Management', 'Accounting and Finance', 'Marketing']) {
    fireEvent.click(within(group).getByLabelText(name));
  }
  expect(within(group).getByLabelText('Languages').disabled).toBe(true);
  expect(within(group).getByLabelText('Marketing').disabled).toBe(false);
});

it('restores the applicant\'s choice on request', async () => {
  const group = await openReview();
  fireEvent.click(within(group).getByLabelText('Marketing'));
  fireEvent.click(screen.getByRole('button', { name: 'Restore applicant\'s choice' }));
  expect(within(group).getByLabelText('Marketing').checked).toBe(false);
  expect(screen.queryByRole('button', { name: 'Restore applicant\'s choice' })).toBeNull();
});

it('sends no correction when rejecting', async () => {
  const group = await openReview();
  fireEvent.click(within(group).getByLabelText('Marketing'));
  fireEvent.click(screen.getByRole('button', { name: 'Reject' }));
  fireEvent.change(screen.getByLabelText(/Review note/), { target: { value: 'Evidence is incomplete.' } });
  fireEvent.click(screen.getByRole('button', { name: 'Reject request' }));
  await waitFor(() => expect(adminRoleRequestsApi.review).toHaveBeenCalledWith('admin-token', 3, 'Reject', 'Evidence is incomplete.', null));
});

it('falls back to showing the chosen specialties when the list of specialties cannot be loaded', async () => {
  adminRoleRequestsApi.specialties.mockRejectedValue(new Error('offline'));
  render(<AdminRoleRequestsPage />);
  fireEvent.click(await screen.findByRole('button', { name: 'Review' }));
  expect(await screen.findByText('Software Development, Management')).toBeTruthy();
  expect(screen.queryByRole('group', { name: /Specialties to certify/ })).toBeNull();
});
