import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { trainerLaterPhaseApi } from '../../api/trainerLaterPhase';
import TrainerSessionMaterialsPanel from './TrainerSessionMaterialsPanel';

vi.mock('../../api/trainerLaterPhase', () => ({
  trainerLaterPhaseApi: {
    sessionMaterials: vi.fn(),
    uploadSessionMaterial: vi.fn(),
    downloadSessionMaterial: vi.fn(),
  },
}));

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

const intake = {
  id: 7,
  sessions: [
    { id: 9, label: 'Session 9' },
    { id: 10, label: 'Session 10' },
  ],
};

function mockLists(groups = {}) {
  trainerLaterPhaseApi.sessionMaterials.mockImplementation(async (_token, _intakeId, sessionId) => groups[sessionId] || []);
}

describe('TrainerSessionMaterialsPanel', () => {
  it('loads and renders each Session list with an independent upload form', async () => {
    mockLists({
      9: [{ id: 15, courseSessionId: 9, sessionLabel: 'Session 9', title: 'Slide deck', format: 'PPTX', uploadedAt: '2026-10-05T10:00:00Z' }],
    });
    render(<TrainerSessionMaterialsPanel token="trainer-token" intake={intake} />);

    expect(await screen.findByText('Slide deck')).toBeTruthy();
    expect(screen.getByRole('heading', { name: 'Session 9' })).toBeTruthy();
    expect(screen.getByRole('heading', { name: 'Session 10' })).toBeTruthy();
    expect(screen.getByText('No materials uploaded for this Session.')).toBeTruthy();
    expect(trainerLaterPhaseApi.sessionMaterials).toHaveBeenCalledWith('trainer-token', 7, 9, expect.any(AbortSignal));
    expect(trainerLaterPhaseApi.sessionMaterials).toHaveBeenCalledWith('trainer-token', 7, 10, expect.any(AbortSignal));
    expect(screen.getByLabelText('Material title for Session 9')).toBeTruthy();
    expect(screen.getByLabelText('Material title for Session 10')).toBeTruthy();
  });

  it('validates the shared 20 MB file limit and supported extensions before uploading', async () => {
    mockLists();
    render(<TrainerSessionMaterialsPanel token="trainer-token" intake={intake} />);
    await screen.findByRole('heading', { name: 'Session 9' });

    const title = screen.getByLabelText('Material title for Session 9');
    const file = screen.getByLabelText('Material file for Session 9');
    fireEvent.change(title, { target: { value: 'Unsupported' } });
    fireEvent.change(file, { target: { files: [new File(['zip'], 'notes.zip', { type: 'application/zip' })] } });
    fireEvent.submit(screen.getByRole('button', { name: 'Upload material for Session 9' }).closest('form'));
    expect((await screen.findByRole('alert')).textContent).toMatch(/PDF, PPTX, DOCX, PNG, JPG or JPEG/);
    expect(trainerLaterPhaseApi.uploadSessionMaterial).not.toHaveBeenCalled();

    const tooLarge = new File([new Uint8Array(20 * 1024 * 1024 + 1)], 'notes.pdf', { type: 'application/pdf' });
    fireEvent.change(file, { target: { files: [tooLarge] } });
    fireEvent.submit(screen.getByRole('button', { name: 'Upload material for Session 9' }).closest('form'));
    expect((await screen.findByRole('alert')).textContent).toMatch(/20 MB or smaller/);
    expect(trainerLaterPhaseApi.uploadSessionMaterial).not.toHaveBeenCalled();
  });

  it('uploads to the selected Session, refreshes its list, and disables duplicate submissions', async () => {
    mockLists();
    trainerLaterPhaseApi.uploadSessionMaterial.mockImplementation(async () => new Promise((resolve) => {
      setTimeout(() => resolve({ id: 16 }), 0);
    }));
    render(<TrainerSessionMaterialsPanel token="trainer-token" intake={intake} />);
    await screen.findByRole('heading', { name: 'Session 9' });

    fireEvent.change(screen.getByLabelText('Material title for Session 9'), { target: { value: 'New handout' } });
    fireEvent.change(screen.getByLabelText('Material file for Session 9'), {
      target: { files: [new File(['pdf'], 'handout.pdf', { type: 'application/pdf' })] },
    });
    const submit = screen.getByRole('button', { name: 'Upload material for Session 9' });
    fireEvent.submit(submit.closest('form'));
    expect(submit.disabled).toBe(true);
    await waitFor(() => expect(trainerLaterPhaseApi.uploadSessionMaterial).toHaveBeenCalledWith(
      'trainer-token', 7, 9, 'New handout', expect.any(File),
    ));
    await waitFor(() => expect(trainerLaterPhaseApi.sessionMaterials).toHaveBeenCalledTimes(4));
  });

  it('downloads a listed file and keeps the session list when the download fails', async () => {
    mockLists({
      9: [{ id: 15, courseSessionId: 9, sessionLabel: 'Session 9', title: 'Slide deck', format: 'PDF', uploadedAt: '2026-10-05T10:00:00Z' }],
    });
    trainerLaterPhaseApi.downloadSessionMaterial.mockRejectedValue({ status: 404, message: 'The session material was not found.' });
    render(<TrainerSessionMaterialsPanel token="trainer-token" intake={intake} />);
    const download = await screen.findByRole('button', { name: 'Download Slide deck' });
    fireEvent.click(download);
    expect((await screen.findByRole('alert')).textContent).toContain('The session material was not found.');
    expect(screen.getByText('Slide deck')).toBeTruthy();
    expect(trainerLaterPhaseApi.downloadSessionMaterial).toHaveBeenCalledWith(
      'trainer-token', 7, 9, 15, expect.stringMatching(/Slide deck\.pdf/i),
    );
  });

  it('shows an empty state when the Intake has no Sessions without requesting unrelated resources', async () => {
    render(<TrainerSessionMaterialsPanel token="trainer-token" intake={{ id: 7, sessions: [] }} />);
    expect(screen.getByText('No Sessions are configured for this Intake.')).toBeTruthy();
    expect(trainerLaterPhaseApi.sessionMaterials).not.toHaveBeenCalled();
  });
});
