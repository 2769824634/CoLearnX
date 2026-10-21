import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { act, cleanup, fireEvent, render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import AdminAuthContext from '../../auth/AdminAuthContext';
import { AdminRoleShell } from '../../layouts/RoleShell';

const overviewApi = vi.hoisted(() => ({ pendingCounts: vi.fn() }));
vi.mock('../../api', async (importOriginal) => ({ ...(await importOriginal()), adminOverviewApi: overviewApi }));
vi.mock('../../components/Logo', () => ({ default: () => <span>Logo</span> }));
vi.mock('../../components/UserAvatar', () => ({ default: () => <span>Avatar</span> }));

const counts = (overrides = {}) => ({ roleRequests: 1, courses: 1, materialVersions: 1, certificates: 1, disputes: 2, ...overrides });

function setHidden(hidden) {
  Object.defineProperty(document, 'hidden', { configurable: true, get: () => hidden });
}

async function settle() {
  await act(async () => { await Promise.resolve(); });
}

function renderShell() {
  return render(
    <MemoryRouter initialEntries={['/admin/home']}>
      <AdminAuthContext.Provider value={{ admin: { email: 'admin@colearnx.test' }, token: 'admin-token', logout: vi.fn() }}>
        <Routes>
          <Route element={<AdminRoleShell />}>
            <Route path="*" element={<div>Admin page</div>} />
          </Route>
        </Routes>
      </AdminAuthContext.Provider>
    </MemoryRouter>,
  );
}

beforeEach(() => {
  vi.useFakeTimers();
  setHidden(false);
});

afterEach(() => {
  cleanup();
  vi.useRealTimers();
  vi.resetAllMocks();
  delete document.hidden;
});

it('shows pending approvals and open disputes as navigation badges', async () => {
  overviewApi.pendingCounts.mockResolvedValue(counts());
  renderShell();
  await settle();

  expect(overviewApi.pendingCounts).toHaveBeenCalledWith('admin-token');
  expect(screen.getByRole('link', { name: 'Approvals, 4 pending' })).toBeTruthy();
  expect(screen.getByRole('link', { name: 'Disputes, 2 pending' })).toBeTruthy();
});

it('hides a badge at zero and caps large numbers at 99+', async () => {
  overviewApi.pendingCounts.mockResolvedValue(counts({ roleRequests: 120, disputes: 0 }));
  renderShell();
  await settle();

  expect(screen.getByRole('link', { name: 'Approvals, 123 pending' }).textContent).toContain('99+');
  expect(screen.getByRole('link', { name: 'Disputes' }).textContent).toBe('Disputes');
});

it('refreshes every 30 seconds while visible and when the window becomes visible again', async () => {
  overviewApi.pendingCounts.mockResolvedValue(counts());
  renderShell();
  await settle();
  expect(overviewApi.pendingCounts).toHaveBeenCalledTimes(1);

  await act(async () => { await vi.advanceTimersByTimeAsync(30_000); });
  expect(overviewApi.pendingCounts).toHaveBeenCalledTimes(2);

  setHidden(true);
  await act(async () => { await vi.advanceTimersByTimeAsync(30_000); });
  expect(overviewApi.pendingCounts).toHaveBeenCalledTimes(2);

  setHidden(false);
  await act(async () => { document.dispatchEvent(new Event('visibilitychange')); });
  expect(overviewApi.pendingCounts).toHaveBeenCalledTimes(3);
});

it('announces new submissions only when the total grows', async () => {
  overviewApi.pendingCounts
    .mockResolvedValueOnce(counts())
    .mockResolvedValueOnce(counts())
    .mockResolvedValueOnce(counts({ courses: 3 }))
    .mockResolvedValueOnce(counts({ courses: 2 }));
  renderShell();
  await settle();
  expect(screen.queryByText(/waiting for review/)).toBeNull();

  await act(async () => { await vi.advanceTimersByTimeAsync(30_000); });
  expect(screen.queryByText(/waiting for review/)).toBeNull();

  await act(async () => { await vi.advanceTimersByTimeAsync(30_000); });
  const notice = screen.getByRole('link', { name: '2 new items are waiting for review' });
  expect(notice.getAttribute('href')).toBe('/admin/approvals');

  fireEvent.click(screen.getByRole('button', { name: 'Dismiss new items notice' }));
  expect(screen.queryByText(/waiting for review/)).toBeNull();

  await act(async () => { await vi.advanceTimersByTimeAsync(30_000); });
  expect(screen.queryByText(/waiting for review/)).toBeNull();
});

it('points a new dispute notice at the disputes page', async () => {
  overviewApi.pendingCounts
    .mockResolvedValueOnce(counts())
    .mockResolvedValueOnce(counts({ disputes: 3 }));
  renderShell();
  await settle();

  await act(async () => { await vi.advanceTimersByTimeAsync(30_000); });
  expect(screen.getByRole('link', { name: '1 new item is waiting for review' }).getAttribute('href')).toBe('/admin/disputes');
});

it('keeps the last counts when a refresh fails', async () => {
  overviewApi.pendingCounts
    .mockResolvedValueOnce(counts())
    .mockRejectedValueOnce(new Error('offline'));
  renderShell();
  await settle();

  await act(async () => { await vi.advanceTimersByTimeAsync(30_000); });
  expect(screen.getByRole('link', { name: 'Approvals, 4 pending' })).toBeTruthy();
  expect(screen.queryByRole('alert')).toBeNull();
});
