import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { adminRoleRequestsApi } from '../../api';
import { adminLaterPhaseApi } from '../../api/adminLaterPhase';
import AdminRoleRequestsPage from './AdminRoleRequestsPage';
import AdminLaterApprovalsPage from './AdminLaterApprovalsPage';

vi.mock('../../auth/useAdminAuth', () => ({ default: () => ({ token: 'admin-token', logout: vi.fn() }) }));
vi.mock('../../api', () => ({ adminRoleRequestsApi: { list: vi.fn(), review: vi.fn(), downloadResume: vi.fn(), downloadIdDocument: vi.fn() } }));
vi.mock('../../api/adminLaterPhase', () => ({ adminLaterPhaseApi: { materialVersions: vi.fn(), certificateRequests: vi.fn(), reviewMaterial: vi.fn(), reviewCertificate: vi.fn(), downloadMaterial: vi.fn() } }));

const applicant = { id: 3, userFullName: 'Trainer Applicant', userEmail: 'applicant@example.test', requestedRole: 'Trainer', status: 'Pending', createdAt: '2026-09-30T17:30:00', degreeOrResumePath: 'private/resume.pdf', idDocumentPath: null };
const material = { versionId: 8, title: 'Workshop guide', status: 'PendingApproval', creatorName: 'Creator Jane', format: 'PNG', versionNumber: 1, courseCode: 'UX101', courseTitle: 'Inclusive UX', courseStatus: 'Published', fileName: 'workshop-guide.png', fileSizeBytes: 2560, filePath: 'private/material.png', submittedAt: '2026-09-30T17:30:00' };
beforeEach(() => {
  adminRoleRequestsApi.list.mockResolvedValue([applicant]);
  adminRoleRequestsApi.downloadResume.mockResolvedValue(undefined);
  adminRoleRequestsApi.downloadIdDocument.mockResolvedValue(undefined);
  adminLaterPhaseApi.materialVersions.mockResolvedValue([material]);
  adminLaterPhaseApi.certificateRequests.mockResolvedValue([]);
  adminLaterPhaseApi.downloadMaterial.mockResolvedValue(undefined);
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

it('shows material course context, file metadata and submission date in UTC', async () => {
  render(<AdminLaterApprovalsPage type="materials" />);
  expect(await screen.findByText('Inclusive UX')).toBeTruthy();
  expect(screen.getByText('UX101')).toBeTruthy();
  expect(screen.getByText('Published')).toBeTruthy();
  expect(screen.getByText('workshop-guide.png')).toBeTruthy();
  expect(screen.getByText('2.5 KB')).toBeTruthy();
  expect(screen.getByText('30 Sept 2026, 17:30 UTC')).toBeTruthy();
});

it('states missing material metadata and hides download for a missing attachment', async () => {
  adminLaterPhaseApi.materialVersions.mockResolvedValue([{ ...material, courseTitle: null, courseCode: null, courseStatus: null, fileName: null, fileSizeBytes: null, submittedAt: null, filePath: '' }]);
  render(<AdminLaterApprovalsPage type="materials" />);
  await screen.findByText('Workshop guide');
  expect(screen.getByText('Course title unavailable')).toBeTruthy();
  expect(screen.getByText('Course code unavailable')).toBeTruthy();
  expect(screen.getByText('Course status unavailable')).toBeTruthy();
  expect(screen.getByText('File name unavailable')).toBeTruthy();
  expect(screen.getByText('File size unavailable')).toBeTruthy();
  expect(screen.getByText('Submission date unavailable')).toBeTruthy();
  expect(screen.queryByRole('button', { name: 'Download submitted file' })).toBeNull();
});

it('awaits material download and guards review actions while the file request is busy', async () => {
  let finish;
  adminLaterPhaseApi.downloadMaterial.mockImplementation(() => new Promise((resolve) => { finish = resolve; }));
  render(<AdminLaterApprovalsPage type="materials" />);
  const button = await screen.findByRole('button', { name: 'Download submitted file' });
  fireEvent.click(button); fireEvent.click(button);
  expect(adminLaterPhaseApi.downloadMaterial).toHaveBeenCalledTimes(1);
  expect(screen.getByRole('button', { name: 'Approve' }).disabled).toBe(true);
  finish();
  expect(await screen.findByText('File download started. Check your browser downloads.')).toBeTruthy();
  expect(adminLaterPhaseApi.downloadMaterial).toHaveBeenCalledWith('admin-token', 8, 'workshop-guide.png');
});

it('keeps a failed review distinct from a failed queue load', async () => {
  adminLaterPhaseApi.reviewMaterial.mockRejectedValue({ status: 500, message: 'SqlServerRetryingExecutionStrategy failed', traceId: 'review-1' });
  render(<AdminLaterApprovalsPage type="materials" />);
  fireEvent.click(await screen.findByRole('button', { name: 'Approve' }));
  expect(await screen.findByText(/The review could not be confirmed/)).toBeTruthy();
  expect(screen.queryByText('Records could not be loaded')).toBeNull();
  expect(screen.queryByText(/SqlServerRetrying/)).toBeNull();
  await waitFor(() => expect(screen.getByRole('button', { name: 'Approve' }).disabled).toBe(false));
});

it('keeps a file download failure local to the attachment rather than relabeling the queue as failed', async () => {
  adminLaterPhaseApi.downloadMaterial.mockRejectedValue({ status: 500, message: 'System.IO.IOException failed', traceId: 'material-download-1' });
  render(<AdminLaterApprovalsPage type="materials" />);
  fireEvent.click(await screen.findByRole('button', { name: 'Download submitted file' }));
  expect(await screen.findByText(/Could not download this submitted file/)).toBeTruthy();
  expect(screen.getByText(/Reference: material-download-1/)).toBeTruthy();
  expect(screen.getByText('Workshop guide')).toBeTruthy();
  expect(screen.queryByText('Records could not be loaded')).toBeNull();
  expect(screen.queryByText(/IOException/)).toBeNull();
});
