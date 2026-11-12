import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import TrainerAssessmentSubmissions from './TrainerAssessmentSubmissions';

afterEach(cleanup);

it('lists each learner submission with downloads and names who has not submitted', async () => {
  const api = { downloadSubmission: vi.fn(async () => null) };
  render(<TrainerAssessmentSubmissions token="trainer-token" api={api}
    learners={[{ enrollmentId: 1, fullName: 'Ada' }, { enrollmentId: 2, fullName: 'Ben' }]}
    assessments={[{
      id: 7, title: 'Final project', dueAt: '2099-01-01T00:00:00Z',
      submissions: [{ enrollmentId: 1, userId: 11, learnerName: 'Ada', submittedAt: '2098-12-30T10:00:00Z',
        files: [{ id: 40, fileName: 'report.pdf', format: 'PDF', sizeBytes: 4096 }, { id: 41, fileName: 'deck.pptx', format: 'PPTX', sizeBytes: 2048 }] }],
    }]} />);

  expect(screen.getByText('1/2 submitted')).toBeTruthy();
  expect(screen.getByText('report.pdf')).toBeTruthy();
  expect(screen.getByText('deck.pptx')).toBeTruthy();
  expect(screen.getByText('Not submitted: Ben')).toBeTruthy();
  fireEvent.click(screen.getByRole('button', { name: 'Download report.pdf from Ada' }));
  await waitFor(() => expect(api.downloadSubmission).toHaveBeenCalledWith('trainer-token', 7, 40, 'report.pdf'));
});

it('renders nothing when the Intake has no assessments', () => {
  const { container } = render(<TrainerAssessmentSubmissions token="t" api={{}} learners={[]} assessments={[]} />);
  expect(container.innerHTML).toBe('');
});
