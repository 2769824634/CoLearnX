import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import AdminDataState from './AdminDataState';
import AdminCourseReviewsPage from './AdminCourseReviewsPage';

const api = vi.hoisted(() => ({ list: vi.fn(), review: vi.fn() }));
vi.mock('../../auth/useAdminAuth', () => ({ default: () => ({ token: 'admin-token', logout: vi.fn() }) }));
vi.mock('../../api', () => ({ adminCourseReviewsApi: api }));
afterEach(cleanup);

describe('first-round administrator feedback', () => {
  it('does not promise no changes when a financial write result is unknown', () => {
    render(<AdminDataState error={{ status: 500 }} operation="Credit adjustment" onRetry={() => {}} />);
    expect(screen.getByText('Credit adjustment result is unconfirmed')).toBeTruthy();
    expect(screen.queryByText(/No changes have been made/)).toBeNull();
    expect(screen.getByRole('button', { name: 'Check current records' })).toBeTruthy();
  });

  it('shows learning outcomes and learning path before publishing a course', async () => {
    api.list.mockResolvedValue([{ id: 7, code: 'CLX700', title: 'Reviewable course',
      category: 'Design', level: 'Beginner', creditCost: 5, status: 'PendingApproval',
      submittedByName: 'Course creator', submittedByEmail: 'creator@example.com',
      learningPath: 'Product design', learningOutcomes: ['Apply an interview method'], description: 'A complete definition' }]);
    render(<AdminCourseReviewsPage />);
    fireEvent.click(await screen.findByRole('button', { name: 'Review' }));
    expect(screen.getByText('Product design')).toBeTruthy();
    expect(screen.getByText('Apply an interview method')).toBeTruthy();
  });
});
