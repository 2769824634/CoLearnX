import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { act, cleanup, fireEvent, render, screen, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { AuthContext } from '../auth/AuthContext';
import { notificationsApi } from '../api';
import { MemberNotificationsProvider } from './MemberNotificationsProvider';
import MemberNotifications from './MemberNotifications';
import { importantNotificationTypes, safeNotificationPath } from './memberNotificationsState';
import { connectNotificationRealtime } from './notificationRealtime';

it.each(['/trainer/courses/intakes/42', '/creator/intakes/42'])('opens the exact Intake notification: %s', async (path) => {
  notificationsApi.my.mockResolvedValue({ unreadCount: 1, items: [{ id: 42, title: 'Class confirmed', message: 'Intake #42 will run.', isRead: false, targetPath: path, createdAt: '2026-09-30T12:00:00Z' }] });
  mount();
  fireEvent.click(await screen.findByRole('button', { name: 'Notifications, 1 unread' }));
  expect(screen.getByRole('link', { name: 'Open related page' }).getAttribute('href')).toBe(path);
});

it.each(['/member/programs', '/member/programs?tab=reserved', '/member/programs?tab=active&enrollmentId=11', '/creator/approvals', '/creator/intakes', '/trainer/courses', '/trainer/invitations', '/creator/courses'])('opens an approved notification path: %s', (path) => {
  expect(safeNotificationPath(path)).toBe(path);
});

it.each(['N-course-invitation', 'N-course-invitation-accepted', 'N-course-invitation-declined'])('raises a toast for teaching invitation updates: %s', (type) => {
  expect(importantNotificationTypes.has(type)).toBe(true);
});

it.each(['//example.com', '/trainer/courses/intakes/0', '/trainer/courses/intakes/42/../43', '/trainer/courses/intakes/42?next=https://example.com', '/creator/intakes/42#other', '/trainer/courses/intakes/%34%32', '/member/programs?tab=unknown', '/member/programs?next=https://example.com', '/member/home', '/member/account', '/member/courses'])('rejects an unapproved notification path: %s', (path) => {
  expect(safeNotificationPath(path)).toBeNull();
});

vi.mock('../api', () => ({ notificationsApi: { my: vi.fn(), read: vi.fn(), readAll: vi.fn() } }));
vi.mock('./notificationRealtime', () => ({ connectNotificationRealtime: vi.fn(() => ({ stop() {} })) }));
beforeEach(() => {
  notificationsApi.read.mockResolvedValue(null);
  notificationsApi.readAll.mockResolvedValue(null);
  connectNotificationRealtime.mockImplementation(() => ({ stop() {} }));
});
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

const published = { id: 31, type: 'N-course-published', title: 'Course published', message: 'CRS-101 Design Basics was approved by Admin.', isRead: false, targetPath: '/creator/approvals', createdAt: '2026-10-07T09:00:00Z' };

it('opens the notification list in a dialog from the bell', async () => {
  notificationsApi.my.mockResolvedValue({ unreadCount: 1, items: [{ id: 5, type: 'N-place-reserved', title: 'Place reserved', message: 'Seat held.', isRead: false, targetPath: null, createdAt: '2026-10-05T01:30:00Z' }] });
  mount();
  fireEvent.click(await screen.findByRole('button', { name: 'Notifications, 1 unread' }));
  const dialog = screen.getByRole('dialog', { name: 'Notifications' });
  expect(within(dialog).getByText('Place reserved')).toBeTruthy();
  fireEvent.click(within(dialog).getByRole('button', { name: 'Close' }));
  expect(screen.queryByRole('dialog')).toBeNull();
});

it('pops up an unread approval decision and marks it read when acknowledged', async () => {
  notificationsApi.my.mockResolvedValue({ unreadCount: 1, items: [published] });
  mount();
  const popup = await screen.findByRole('dialog', { name: 'Course published' });
  expect(within(popup).getByText('CRS-101 Design Basics was approved by Admin.')).toBeTruthy();
  expect(within(popup).getByRole('link', { name: 'Open related page' }).getAttribute('href')).toBe('/creator/approvals');
  fireEvent.click(within(popup).getByRole('button', { name: 'Got it' }));
  expect(screen.queryByRole('dialog')).toBeNull();
  expect(notificationsApi.read).toHaveBeenCalledWith(31, 'member');
  expect(await screen.findByRole('button', { name: 'Notifications, 0 unread' })).toBeTruthy();
});

it('does not pop up routine or already-read notifications', async () => {
  notificationsApi.my.mockResolvedValue({ unreadCount: 1, items: [{ ...published, id: 32, isRead: true }, { id: 33, type: 'N-place-reserved', title: 'Place reserved', message: 'Seat held.', isRead: false, targetPath: null, createdAt: '2026-10-05T01:30:00Z' }] });
  mount();
  expect(await screen.findByRole('button', { name: 'Notifications, 1 unread' })).toBeTruthy();
  expect(screen.queryByRole('dialog')).toBeNull();
});

it('pops up a decision as soon as it is pushed', async () => {
  let deliver;
  connectNotificationRealtime.mockImplementation((_token, onNotification) => {
    deliver = onNotification;
    return { stop() {} };
  });
  notificationsApi.my.mockResolvedValue({ unreadCount: 0, items: [] });
  mount();
  expect(await screen.findByRole('button', { name: 'Notifications, 0 unread' })).toBeTruthy();
  expect(screen.queryByRole('dialog')).toBeNull();
  await act(async () => { deliver(published); });
  expect(screen.getByRole('dialog', { name: 'Course published' })).toBeTruthy();
  expect(notificationsApi.my).toHaveBeenCalledTimes(1);
});

it('checks for new decisions every minute and pops them up', async () => {
  vi.useFakeTimers({ shouldAdvanceTime: true });
  try {
    notificationsApi.my.mockResolvedValueOnce({ unreadCount: 0, items: [] }).mockResolvedValue({ unreadCount: 1, items: [{ ...published, type: 'N-intake-published', title: 'Class assigned', message: 'A class was published for you.', targetPath: '/trainer/courses/intakes/12' }] });
    mount();
    expect(await screen.findByRole('button', { name: 'Notifications, 0 unread' })).toBeTruthy();
    await act(() => vi.advanceTimersByTimeAsync(60_000));
    expect(await screen.findByRole('dialog', { name: 'Class assigned' })).toBeTruthy();
    expect(notificationsApi.my).toHaveBeenCalledTimes(2);
  } finally { vi.useRealTimers(); }
});

it('labels a notification timestamp as UTC even when the server omits its zone suffix', async () => {
  notificationsApi.my.mockResolvedValue({ unreadCount: 1, items: [{ id: 9, title: 'Place reserved', isRead: false, targetPath: '/member/programs?tab=reserved&enrollmentId=11', createdAt: '2026-10-05T01:30:00' }] });
  mount();
  fireEvent.click(await screen.findByRole('button', { name: 'Notifications, 1 unread' }));
  expect(screen.getByText('05 Oct 2026, 01:30 UTC')).toBeTruthy();
  expect(screen.getByRole('link', { name: 'Open related page' }).getAttribute('href')).toBe('/member/programs?tab=reserved&enrollmentId=11');
});
