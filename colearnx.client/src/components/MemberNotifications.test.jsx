import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { AuthContext } from '../auth/AuthContext';
import { notificationsApi } from '../api';
import { MemberNotificationsProvider } from './MemberNotificationsProvider';
import MemberNotifications from './MemberNotifications';
import { safeNotificationPath } from './memberNotificationsState';

it.each(['/trainer/courses/intakes/42', '/creator/courses/intake-applications/42'])('opens the exact Intake notification: %s', async (path) => {
  notificationsApi.my.mockResolvedValue({ unreadCount: 1, items: [{ id: 42, title: 'Class confirmed', message: 'Intake #42 will run.', isRead: false, targetPath: path, createdAt: '2026-09-30T12:00:00Z' }] });
  mount();
  fireEvent.click(await screen.findByRole('button', { name: 'Notifications, 1 unread' }));
  expect(screen.getByRole('link', { name: 'Open related page' }).getAttribute('href')).toBe(path);
});

it.each(['//example.com', '/trainer/courses/intakes/0', '/trainer/courses/intakes/42/../43', '/trainer/courses/intakes/42?next=https://example.com', '/creator/courses/intake-applications/42#other', '/trainer/courses/intakes/%34%32'])('rejects an unapproved notification path: %s', (path) => {
  expect(safeNotificationPath(path)).toBeNull();
});

vi.mock('../api', () => ({ notificationsApi: { my: vi.fn(), read: vi.fn(), readAll: vi.fn() } }));
beforeEach(() => { notificationsApi.read.mockResolvedValue(null); notificationsApi.readAll.mockResolvedValue(null); });
afterEach(() => { cleanup(); vi.clearAllMocks(); });
function mount() { render(<MemoryRouter><AuthContext.Provider value={{ token: 'member' }}><MemberNotificationsProvider><MemberNotifications /></MemberNotificationsProvider></AuthContext.Provider></MemoryRouter>); }

it('marks a real notification read and rejects external business links', async () => {
  notificationsApi.my.mockResolvedValue({ unreadCount: 1, items: [{ id: 1, title: 'Certificate issued', message: 'Your certificate is ready.', isRead: false, targetPath: 'https://example.com', createdAt: '2026-09-18T12:00:00Z' }] });
  mount();
  fireEvent.click(await screen.findByRole('button', { name: 'Notifications, 1 unread' }));
  expect(screen.queryByRole('link', { name: 'Open related page' })).toBeNull();
  fireEvent.click(screen.getByRole('button', { name: 'Mark as read' }));
  expect(await screen.findByRole('button', { name: 'Notifications, 0 unread' })).toBeTruthy();
  expect(notificationsApi.read).toHaveBeenCalledWith(1, 'member');
});

it('offers retry for an unavailable list and then shows empty state', async () => {
  notificationsApi.my.mockRejectedValueOnce(new Error('Network unavailable')).mockResolvedValue({ unreadCount: 0, items: [] });
  mount();
  fireEvent.click(screen.getByRole('button', { name: /Notifications/ }));
  expect(await screen.findByText('Network unavailable')).toBeTruthy();
  fireEvent.click(screen.getByRole('button', { name: 'Retry' }));
  expect(await screen.findByText('No notifications yet.')).toBeTruthy();
});
