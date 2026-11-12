import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { adminRoleRequestsApi } from '../../api';
import { adminLaterPhaseApi } from '../../api/adminLaterPhase';
import AdminRoleRequestsPage from './AdminRoleRequestsPage';
import AdminLaterApprovalsPage from './AdminLaterApprovalsPage';

vi.mock('../../auth/useAdminAuth', () => ({ default: () => ({ token: 'admin-token', logout: vi.fn() }) }));
vi.mock('../../api', () => ({ adminRoleRequestsApi: { list: vi.fn(), review: vi.fn(), specialties: vi.fn(), downloadResume: vi.fn(), downloadIdDocument: vi.fn() } }));
vi.mock('../../api/adminLaterPhase', () => ({ adminLaterPhaseApi: { certificateRequests: vi.fn(), reviewCertificate: vi.fn(), issueCertificate: vi.fn() } }));

const applicant = { id: 3, userFullName: 'Trainer Applicant', userEmail: 'applicant@example.test', requestedRole: 'Trainer', status: 'Pending', createdAt: '2026-09-30T17:30:00', degreeOrResumePath: 'private/resume.pdf', idDocumentPath: null };
beforeEach(() => {
  adminRoleRequestsApi.list.mockResolvedValue([applicant]);
  adminRoleRequestsApi.specialties.mockResolvedValue([]);
  adminRoleRequestsApi.downloadResume.mockResolvedValue(undefined);
  adminRoleRequestsApi.downloadIdDocument.mockResolvedValue(undefined);
  adminLaterPhaseApi.certificateRequests.mockResolvedValue([]);
});
afterEach(() => { cleanup(); vi.resetAllMocks(); });

it('labels role submission timestamps explicitly in UTC, including unqualified server timestamps', async () => {
  render(<AdminRoleRequestsPage />);
  expect(await screen.findByText('30 Sept 2026, 17:30 UTC')).toBeTruthy();
  expect(screen.getByRole('columnheader', { name: 'Submitted (UTC)' })).toBeTruthy();
});

it('offers only the role evidence file that the request actually declares', async () => {
  render(<AdminRoleRequestsPage />);
  fireEvent.click(await screen.findByRole('button', { name: 'Review' }));
  expect(screen.getByRole('button', { name: 'Download resume' })).toBeTruthy();
  expect(screen.queryByRole('button', { name: 'Download ID document' })).toBeNull();
});

it('awaits a role download, prevents repeat requests and reports only browser download startup', async () => {
  let finish;
  adminRoleRequestsApi.downloadResume.mockImplementation(() => new Promise((resolve) => { finish = resolve; }));
  render(<AdminRoleRequestsPage />);
  fireEvent.click(await screen.findByRole('button', { name: 'Review' }));
  const button = screen.getByRole('button', { name: 'Download resume' });
  fireEvent.click(button); fireEvent.click(button);
  expect(adminRoleRequestsApi.downloadResume).toHaveBeenCalledTimes(1);
  expect(screen.getByText('Downloading resume…')).toBeTruthy();
  finish();
  expect(await screen.findByText('Resume download started. Check your browser downloads.')).toBeTruthy();
});

it('handles an evidence download failure with a friendly message and reference', async () => {
  adminRoleRequestsApi.downloadResume.mockRejectedValue({ status: 500, message: 'System.IO.IOException private storage unavailable', traceId: 'role-download-1' });
  render(<AdminRoleRequestsPage />);
  fireEvent.click(await screen.findByRole('button', { name: 'Review' }));
  fireEvent.click(screen.getByRole('button', { name: 'Download resume' }));
  expect(await screen.findByText(/Reference: role-download-1/)).toBeTruthy();
  expect(screen.queryByText(/IOException/)).toBeNull();
});

it('issues a trainer-approved certificate only with a PDF and rejects only with a reason', async () => {
  adminLaterPhaseApi.certificateRequests.mockResolvedValue([{
    id: 4, learnerName: 'Learner', courseCode: 'UX101', courseTitle: 'Inclusive UX', status: 'TrainerApproved',
    trainerReviewReason: 'Ready', submittedAt: '2026-09-30T17:30:00', trainerReviewedAt: '2026-09-30T18:00:00',
  }]);
  adminLaterPhaseApi.issueCertificate.mockResolvedValue({ id: 4, status: 'Issued' });
  adminLaterPhaseApi.reviewCertificate.mockResolvedValue({ id: 4, status: 'AdminRejected' });
  render(<AdminLaterApprovalsPage />);
  const issue = await screen.findByRole('button', { name: 'Issue certificate' });
  expect(issue.disabled).toBe(true);
  const reject = screen.getByRole('button', { name: 'Reject' });
  expect(reject.disabled).toBe(true);
  const file = new File(['%PDF-1.4'], 'award.pdf', { type: 'application/pdf' });
  fireEvent.change(screen.getByLabelText('Certificate PDF'), { target: { files: [file] } });
  expect(issue.disabled).toBe(false);
  fireEvent.click(issue);
  await waitFor(() => expect(adminLaterPhaseApi.issueCertificate).toHaveBeenCalledWith('admin-token', 4, file, undefined));
  fireEvent.change(await screen.findByLabelText('Optional note / required rejection reason'), { target: { value: 'Attendance evidence is incomplete.' } });
  fireEvent.click(screen.getByRole('button', { name: 'Reject' }));
  await waitFor(() => expect(adminLaterPhaseApi.reviewCertificate).toHaveBeenCalledWith('admin-token', 4, 'Reject', 'Attendance evidence is incomplete.'));
});
