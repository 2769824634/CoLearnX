import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { trainerLaterPhaseApi } from '../../api/trainerLaterPhase';
import TrainerResourcesPanel from './TrainerResourcesPanel';

vi.mock('../../api/trainerLaterPhase', () => ({
  canPreviewMaterial: (format) => /^(png|jpe?g|gif|webp|pdf)$/i.test(String(format || '')),
  trainerLaterPhaseApi: {
    availableMaterials: vi.fn(),
    intakeMaterials: vi.fn(),
    recordings: vi.fn(),
    attachMaterial: vi.fn(),
    addRecording: vi.fn(),
    downloadMaterial: vi.fn(),
  },
}));

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

const attached = {
  materialVersionId: 5,
  title: 'L-07 Test Material',
  format: 'PNG',
  filePath: 'materials/3/file.png',
  versionNumber: 1,
};

function mockLists(extra = {}) {
  trainerLaterPhaseApi.availableMaterials.mockResolvedValue(extra.available || []);
  trainerLaterPhaseApi.intakeMaterials.mockResolvedValue(extra.attached || [attached]);
  trainerLaterPhaseApi.recordings.mockResolvedValue([]);
}

it('does not treat a storage key as a page link', async () => {
  mockLists();
  render(<TrainerResourcesPanel token="trainer-token" intake={{ id: 7, courseId: 7, sessions: [{ id: 9, label: 'Session 9' }] }} />);
  expect(await screen.findByText('L-07 Test Material')).toBeTruthy();
  expect(screen.getByRole('button', { name: 'Open' })).toBeTruthy();
  expect(screen.queryByRole('link', { name: 'Open' })).toBeNull();
  expect(screen.queryByRole('link', { name: /materials\/3/ })).toBeNull();
});

it('opens the attached PNG through the intake download and shows an in-page preview', async () => {
  mockLists();
  const blob = new Blob(['png-bytes'], { type: 'image/png' });
  trainerLaterPhaseApi.downloadMaterial.mockResolvedValue(blob);
  URL.createObjectURL = vi.fn(() => 'blob:preview-png');
  render(<TrainerResourcesPanel token="trainer-token" intake={{ id: 7, courseId: 7, sessions: [{ id: 9, label: 'Session 9' }] }} />);
  const open = await screen.findByRole('button', { name: 'Open' });
  fireEvent.click(open);
  fireEvent.click(open);
  await waitFor(() => expect(trainerLaterPhaseApi.downloadMaterial).toHaveBeenCalledTimes(1));
  expect(trainerLaterPhaseApi.downloadMaterial).toHaveBeenCalledWith('trainer-token', 7, 5, expect.stringMatching(/L-07 Test Material\.png/i));
  expect(screen.getByRole('img', { name: 'L-07 Test Material' }).getAttribute('src')).toBe('blob:preview-png');
});

it('keeps the Trainer on the Intake page when download fails', async () => {
  mockLists();
  trainerLaterPhaseApi.downloadMaterial.mockRejectedValue({ status: 404, message: 'The attached material version was not found.', traceId: 't-1' });
  render(<TrainerResourcesPanel token="trainer-token" intake={{ id: 7, courseId: 7, sessions: [{ id: 9, label: 'Session 9' }] }} />);
  fireEvent.click(await screen.findByRole('button', { name: 'Open' }));
  expect(await screen.findByRole('alert')).toBeTruthy();
  expect(screen.getByText('L-07 Test Material')).toBeTruthy();
  expect(screen.queryByRole('img', { name: 'L-07 Test Material' })).toBeNull();
});
