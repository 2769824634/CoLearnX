import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import AdminCourseReviewsPage from './AdminCourseReviewsPage';
import { AdminPendingContext } from './adminPending';

const courseApi = vi.hoisted(() => ({ list: vi.fn(), review: vi.fn() }));
const materialApi = vi.hoisted(() => ({ downloadMaterial: vi.fn(), reviewMaterial: vi.fn() }));

vi.mock('../../auth/useAdminAuth', () => ({ default: () => ({ token: 'admin-token', logout: vi.fn() }) }));
vi.mock('../../api', () => ({ adminCourseReviewsApi: courseApi }));
vi.mock('../../api/adminLaterPhase', () => ({ adminLaterPhaseApi: materialApi }));

const course = {
  id: 7,
  code: 'CLX700',
  title: 'Inclusive UX',
  category: 'Legacy category',
  learningPath: 'Product design',
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
  materialApi.reviewMaterial.mockResolvedValue({ versionId: 9, status: 'Approved' });
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
    expect(screen.getByText('3 files')).toBeTruthy();
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

  it('places the course by learning path instead of the retired category', async () => {
    render(<AdminCourseReviewsPage />);
    expect(await screen.findByText('Product design')).toBeTruthy();
    fireEvent.click(screen.getByRole('button', { name: 'Review' }));

    expect(screen.getByText('Beginner · 20 credits')).toBeTruthy();
    expect(screen.getAllByText('Product design')).toHaveLength(2);
    expect(screen.queryByText(/Legacy category/)).toBeNull();
  });

  it('lists the trainers on the course with their roster status', async () => {
    courseApi.list.mockResolvedValue([{
      ...course,
      trainers: [
        { trainerId: 31, fullName: 'Alex Chen', status: 'Accepted' },
        { trainerId: 32, fullName: 'Sam Lee', status: 'Invited' },
      ],
    }]);
    render(<AdminCourseReviewsPage />);
    fireEvent.click(await screen.findByRole('button', { name: 'Review' }));

    const trainers = screen.getByRole('list', { name: 'Trainers' });
    expect(trainers.textContent).toContain('Alex Chen · Accepted');
    expect(trainers.textContent).toContain('Sam Lee · Invited');
  });

  it('says when a course has no trainers yet', async () => {
    render(<AdminCourseReviewsPage />);
    fireEvent.click(await screen.findByRole('button', { name: 'Review' }));

    expect(screen.getByText('No trainers on this course yet.')).toBeTruthy();
    expect(screen.queryByRole('list', { name: 'Trainers' })).toBeNull();
  });
});

it('updates the pending badges once a course decision is saved', async () => {
  courseApi.review.mockResolvedValue({ course: { ...course, status: 'Published' }, alreadyReviewed: false });
  const pending = { counts: null, refresh: vi.fn() };
  render(<AdminPendingContext.Provider value={pending}><AdminCourseReviewsPage /></AdminPendingContext.Provider>);
  fireEvent.click(await screen.findByRole('button', { name: 'Review' }));

  fireEvent.click(screen.getByRole('button', { name: 'Publish course' }));

  await waitFor(() => expect(pending.refresh).toHaveBeenCalledTimes(1));
});

describe('files are reviewed together with their course', () => {
  const live = {
    ...course, id: 12, code: 'CLX900', title: 'Live course', status: 'Published',
    materialVersionCount: 2,
    materialVersionStatusCounts: { Approved: 1, PendingApproval: 1 },
    materialVersions: [
      { versionId: 20, title: 'Old handout', format: 'PDF', versionNumber: 1, status: 'Approved' },
      { versionId: 21, title: 'New handout', format: 'PDF', versionNumber: 1, status: 'PendingApproval' },
    ],
  };

  it('asks the queue for published courses that have a new file waiting', async () => {
    render(<AdminCourseReviewsPage />);
    await screen.findByRole('button', { name: 'Review' });
    expect(courseApi.list).toHaveBeenCalledWith('admin-token', 'PendingApproval', expect.anything(), true);
  });

  it('tells the admin that publishing the course also approves its waiting files', async () => {
    render(<AdminCourseReviewsPage />);
    fireEvent.click(await screen.findByRole('button', { name: 'Review' }));
    expect(screen.getByText(/Publishing the course also approves the 1 file waiting for review/)).toBeTruthy();
  });

  it('rejects a single file with a reason before the course is published', async () => {
    materialApi.reviewMaterial.mockResolvedValue({ versionId: 9, status: 'Rejected' });
    render(<AdminCourseReviewsPage />);
    fireEvent.click(await screen.findByRole('button', { name: 'Review' }));
    fireEvent.click(screen.getByRole('button', { name: 'Reject Workshop guide v1' }));
    const confirm = screen.getByRole('button', { name: 'Confirm rejection' });
    expect(confirm.disabled).toBe(true);
    fireEvent.change(screen.getByLabelText('Reason for rejecting Workshop guide'), { target: { value: 'Scan is unreadable.' } });
    fireEvent.click(confirm);
    await waitFor(() => expect(materialApi.reviewMaterial).toHaveBeenCalledWith('admin-token', 9, 'Reject', 'Scan is unreadable.'));
    expect(await screen.findByText('1 Rejected')).toBeTruthy();
    expect(screen.queryByText(/also approves/)).toBeNull();
  });

  it('reviews only the new files of a course that is already published', async () => {
    courseApi.list.mockResolvedValue([live]);
    materialApi.reviewMaterial.mockResolvedValue({ versionId: 21, status: 'Approved' });
    const pending = { counts: null, refresh: vi.fn() };
    render(<AdminPendingContext.Provider value={pending}><AdminCourseReviewsPage /></AdminPendingContext.Provider>);
    expect(await screen.findByText('1 new file')).toBeTruthy();
    fireEvent.click(screen.getByRole('button', { name: 'Review files' }));

    expect(screen.getByRole('dialog', { name: 'Review new files' })).toBeTruthy();
    expect(screen.queryByRole('button', { name: 'Publish course' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Approve Old handout v1' })).toBeNull();
    fireEvent.click(screen.getByRole('button', { name: 'Approve New handout v1' }));

    await waitFor(() => expect(materialApi.reviewMaterial).toHaveBeenCalledWith('admin-token', 21, 'Approve', ''));
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
    expect(screen.getByRole('status').textContent).toMatch(/CLX900 has no files waiting/);
    expect(screen.queryByText('Live course')).toBeNull();
    expect(pending.refresh).toHaveBeenCalled();
  });

  it('keeps the file waiting and explains when its review cannot be saved', async () => {
    courseApi.list.mockResolvedValue([live]);
    materialApi.reviewMaterial.mockRejectedValue({ status: 500, message: 'SqlServerRetryingExecutionStrategy failed', traceId: 'file-1' });
    render(<AdminCourseReviewsPage />);
    fireEvent.click(await screen.findByRole('button', { name: 'Review files' }));
    fireEvent.click(screen.getByRole('button', { name: 'Approve New handout v1' }));
    expect((await screen.findByRole('alert')).textContent).toMatch(/could not be saved.*Reference: file-1/);
    expect(screen.queryByText(/SqlServerRetrying/)).toBeNull();
    expect(screen.getByRole('button', { name: 'Approve New handout v1' }).disabled).toBe(false);
  });
});
