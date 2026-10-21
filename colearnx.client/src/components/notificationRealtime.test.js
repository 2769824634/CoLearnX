import { afterEach, expect, it, vi } from 'vitest';
import { HubConnectionBuilder } from '@microsoft/signalr';
import { connectNotificationRealtime } from './notificationRealtime';

afterEach(() => { vi.restoreAllMocks(); });

it('connects to the notification hub on the current origin', async () => {
  const withUrl = vi.spyOn(HubConnectionBuilder.prototype, 'withUrl');
  const realtime = connectNotificationRealtime('member', () => {});
  expect(withUrl.mock.calls[0][0]).toBe(`${window.location.origin}/hubs/notifications`);
  expect(realtime.connected).toBe(false);
  await realtime.stop();
});

it('falls back to polling when the hub connection cannot be created', async () => {
  vi.spyOn(HubConnectionBuilder.prototype, 'build').mockImplementation(() => { throw new Error('unsupported'); });
  const realtime = connectNotificationRealtime('member', () => {});
  expect(realtime.connected).toBe(false);
  await expect(realtime.stop()).resolves.toBeUndefined();
});
