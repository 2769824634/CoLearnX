import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import MemberAssessmentCard from './MemberAssessmentCard';

afterEach(() => { cleanup(); vi.unstubAllGlobals(); });

const future = new Date(Date.now() + 3 * 24 * 3600 * 1000).toISOString();
const past = new Date(Date.now() - 3600 * 1000).toISOString();

function pdf(name, size = 12) {
  return new File(['x'.repeat(size)], name, { type: 'application/pdf' });
}

function renderCard(item, props = {}) {
  return render(<MemberAssessmentCard enrollmentId={3} token="member-token" showToast={vi.fn()} {...props}
    item={{ id: 22, title: 'Case study', maxScore: 100, passScore: 60, status: 'Not graded', score: null, files: [], canSubmit: true, dueAt: future, ...item }} />);
}

it('lets a member choose several documents and submit them together before the deadline', async () => {
  const fetch = vi.fn(async () => new Response(JSON.stringify({ id: 22 }), { headers: { 'Content-Type': 'application/json' } }));
  vi.stubGlobal('fetch', fetch);
  const onSubmitted = vi.fn();
  renderCard({}, { onSubmitted });

  expect(screen.getByText('Open')).toBeTruthy();
  expect(screen.getByText(/Closes in 3 days|Closes in 2 days/)).toBeTruthy();
  fireEvent.change(screen.getByLabelText('Choose documents for Case study'), { target: { files: [pdf('essay.pdf'), pdf('appendix.pdf')] } });
  expect(screen.getByText('essay.pdf')).toBeTruthy();
  expect(screen.getByText('appendix.pdf')).toBeTruthy();
  fireEvent.click(screen.getByRole('button', { name: 'Submit 2 documents' }));

  await waitFor(() => expect(onSubmitted).toHaveBeenCalled());
  const [path, init] = fetch.mock.calls[0];
  expect(path).toBe('/api/enrollments/3/assessments/22/submission');
  expect(init.method).toBe('POST');
  expect(init.body.getAll('files').map((file) => file.name)).toEqual(['essay.pdf', 'appendix.pdf']);
  expect(init.headers.Authorization).toBe('Bearer member-token');
});

it('blocks unsupported files before uploading', () => {
  const fetch = vi.fn();
  vi.stubGlobal('fetch', fetch);
  renderCard();

  fireEvent.change(screen.getByLabelText('Choose documents for Case study'), { target: { files: [new File(['x'], 'notes.txt', { type: 'text/plain' })] } });
  fireEvent.click(screen.getByRole('button', { name: 'Submit assignment' }));

  expect(screen.getByRole('alert').textContent).toMatch(/notes\.txt is not supported/);
  expect(fetch).not.toHaveBeenCalled();
});

it('shows submitted documents and a replace action while still open', () => {
  renderCard({ submittedAt: future, files: [{ id: 5, fileName: 'essay.pdf', format: 'PDF', sizeBytes: 2048, submittedAt: future }] });

  expect(screen.getByText('Submitted')).toBeTruthy();
  expect(screen.getByText('essay.pdf')).toBeTruthy();
  expect(screen.getByRole('button', { name: 'Download' })).toBeTruthy();
  expect(screen.getByText(/replaces all of your current documents/)).toBeTruthy();
});

it('hides the upload area after the deadline', () => {
  renderCard({ dueAt: past, canSubmit: false });

  expect(screen.queryByLabelText('Choose documents for Case study')).toBeNull();
  expect(screen.getByText('Submission closed')).toBeTruthy();
  expect(screen.getByText(/Submissions are closed/)).toBeTruthy();
});

it('treats a deadline that passes while the page is open as closed even if the server allowed it earlier', () => {
  renderCard({ dueAt: past, canSubmit: true });

  expect(screen.queryByLabelText('Choose documents for Case study')).toBeNull();
});

it('shows the grade and feedback once graded', () => {
  renderCard({ status: 'Passed', score: 72, feedback: 'Clear evidence.', canSubmit: false });

  expect(screen.getByText('72/100 · Passed')).toBeTruthy();
  expect(screen.getByText('Clear evidence.')).toBeTruthy();
  expect(screen.queryByLabelText('Choose documents for Case study')).toBeNull();
});
