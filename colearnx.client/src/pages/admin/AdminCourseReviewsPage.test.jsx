import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import AdminCourseReviewsPage from './AdminCourseReviewsPage';

const courseApi = vi.hoisted(() => ({ list: vi.fn(), review: vi.fn() }));
const materialApi = vi.hoisted(() => ({ downloadMaterial: vi.fn() }));

vi.mock('../../auth/useAdminAuth', () => ({ default: () => ({ token: 'admin-token', logout: vi.fn() }) }));
vi.mock('../../api', () => ({ adminCourseReviewsApi: courseApi }));
vi.mock('../../api/adminLaterPhase', () => ({ adminLaterPhaseApi: materialApi }));

const course = {
  id: 7,
  code: 'CLX700',
  title: 'Inclusive UX',
  category: 'Design',
  level: 'Beginner',
  creditCost: 20,
  status: 'PendingApproval',
  submittedByName: 'Course creator',
  submittedByEmail: 'creator@example.com',
  creatorName: 'Course creator',
  interests: [{ id: 11, slug: 'ux-research', name: 'UX research' }],
  materialVersionCount: 3,
  materialVersionStatusCounts: { Approved: 2, PendingApproval: 1 },
  materialVersions: [
    { versionId: 8, title: 'Workshop guide', format: 'PDF', versionNumber: 2, status: 'Approved' },
    { versionId: 9, title: 'Workshop guide', format: 'PDF', versionNumber: 1, status: 'PendingApproval' },
    { versionId: 10, title: 'Research slides', format: 'PPTX', versionNumber: 3, status: 'Approved' },
  ],
};

beforeEach(() => {
  courseApi.list.mockResolvedValue([course]);
  courseApi.review.mockResolvedValue({ course, alreadyReviewed: false });
  materialApi.downloadMaterial.mockResolvedValue(undefined);
});

afterEach(() => {
  cleanup();
  vi.resetAllMocks();
});

describe('AdminCourseReviewsPage material and interest summary', () => {
  it('shows interest names, material status counts and business versions before review', async () => {
    render(<AdminCourseReviewsPage />);
    fireEvent.click(await screen.findByRole('button', { name: 'Review' }));

    expect(screen.getByText('UX research')).toBeTruthy();
    expect(screen.getByText('3 material versions')).toBeTruthy();
    expect(screen.getByText('2 Approved')).toBeTruthy();
    expect(screen.getByText('1 Pending approval')).toBeTruthy();
    expect(screen.getByText('v2')).toBeTruthy();
    expect(screen.getByText('v1')).toBeTruthy();
    expect(screen.queryByText(/materials\//i)).toBeNull();
  });

  it('offers an authorized material download without exposing storage paths', async () => {
    render(<AdminCourseReviewsPage />);
    fireEvent.click(await screen.findByRole('button', { name: 'Review' }));
    const download = screen.getByRole('button', { name: 'Download Workshop guide v2' });

    fireEvent.click(download);

    await waitFor(() => expect(materialApi.downloadMaterial).toHaveBeenCalledWith(
      'admin-token',
      8,
      'Workshop guide.pdf',
    ));
    expect(screen.queryByText(/FilePath|filePath|materials\//)).toBeNull();
  });
});
