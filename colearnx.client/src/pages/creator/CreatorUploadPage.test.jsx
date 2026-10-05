import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import CreatorUploadPage from './CreatorUploadPage';

const api = vi.hoisted(() => ({
  creatorCoursesApi: { list: vi.fn() },
  materialsApi: { list: vi.fn(), storage: vi.fn(), upload: vi.fn() },
}));

vi.mock('../../auth/AuthContext', () => ({ useAuth: () => ({ token: 'creator-token' }) }));
vi.mock('../../api', () => api);

beforeEach(() => {
  api.creatorCoursesApi.list.mockResolvedValue([{ id: 1, code: 'UX1', title: 'Inclusive UX' }]);
  api.materialsApi.list.mockResolvedValue([]);
  api.materialsApi.storage.mockResolvedValue({ cloudLinks: false });
  api.materialsApi.upload.mockRejectedValue({
    status: 400,
    message: 'Check the highlighted fields.',
    fieldErrors: {
      title: ['Title is required.'],
      file: ['File must be 20 MB or smaller.'],
    },
  });
});

afterEach(() => {
  cleanup();
  vi.resetAllMocks();
});

describe('CreatorUploadPage field feedback', () => {
  it('maps upload fieldErrors beside controls and clears only the edited field', async () => {
    render(<CreatorUploadPage />);
    fireEvent.change(await screen.findByLabelText('Course'), { target: { value: '1' } });
    fireEvent.change(screen.getByLabelText('Title'), { target: { value: 'Guide' } });
    fireEvent.change(screen.getByLabelText('File'), {
      target: { files: [new File(['guide'], 'guide.pdf', { type: 'application/pdf' })] },
    });
    fireEvent.submit(screen.getByRole('button', { name: 'Upload' }).closest('form'));

    expect(await screen.findByText('Title is required.')).toBeTruthy();
    expect(screen.getByText('File must be 20 MB or smaller.')).toBeTruthy();
    expect(screen.getByLabelText('Title').getAttribute('aria-invalid')).toBe('true');
    expect(screen.getByLabelText('File').getAttribute('aria-invalid')).toBe('true');

    fireEvent.change(screen.getByLabelText('Title'), { target: { value: 'Updated guide' } });

    expect(screen.queryByText('Title is required.')).toBeNull();
    expect(screen.getByText('File must be 20 MB or smaller.')).toBeTruthy();
    expect(screen.getByText('Please correct the remaining fields: File must be 20 MB or smaller.')).toBeTruthy();
    expect(screen.getByLabelText('File').getAttribute('aria-invalid')).toBe('true');
  });

  it('keeps a server or authorization failure visible while a field is edited', async () => {
    api.materialsApi.upload.mockRejectedValueOnce({
      status: 500,
      code: 'UPLOAD_FAILED',
      message: 'The server could not confirm the upload.',
      fieldErrors: { file: ['The server could not validate this file yet.'] },
    });
    render(<CreatorUploadPage />);
    fireEvent.change(await screen.findByLabelText('Course'), { target: { value: '1' } });
    fireEvent.change(screen.getByLabelText('Title'), { target: { value: 'Guide' } });
    fireEvent.change(screen.getByLabelText('File'), {
      target: { files: [new File(['guide'], 'guide.pdf', { type: 'application/pdf' })] },
    });
    const form = screen.getByRole('button', { name: 'Upload' }).closest('form');
    fireEvent.submit(form);
    expect(await screen.findByText('The upload could not be confirmed. Check the course library before uploading again.')).toBeTruthy();

    fireEvent.change(screen.getByLabelText('Title'), { target: { value: 'Updated guide' } });

    expect(screen.getByText('The upload could not be confirmed. Check the course library before uploading again.')).toBeTruthy();
    expect(screen.getByText('The server could not validate this file yet.')).toBeTruthy();
  });

  it('clears stale field feedback before a new submission while retaining the latest server error', async () => {
    api.materialsApi.upload
      .mockRejectedValueOnce({ status: 400, message: 'Check fields.', fieldErrors: { title: ['Old title error.'] } })
      .mockRejectedValueOnce({ status: 400, message: 'Check fields.', fieldErrors: { file: ['New file error.'] } });
    render(<CreatorUploadPage />);
    fireEvent.change(await screen.findByLabelText('Course'), { target: { value: '1' } });
    fireEvent.change(screen.getByLabelText('Title'), { target: { value: 'Guide' } });
    fireEvent.change(screen.getByLabelText('File'), {
      target: { files: [new File(['guide'], 'guide.pdf', { type: 'application/pdf' })] },
    });
    const form = screen.getByRole('button', { name: 'Upload' }).closest('form');
    fireEvent.submit(form);
    expect(await screen.findByText('Old title error.')).toBeTruthy();

    fireEvent.submit(form);

    expect(await screen.findByText('New file error.')).toBeTruthy();
    expect(screen.queryByText('Old title error.')).toBeNull();
  });
});
