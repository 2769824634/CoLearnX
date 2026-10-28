import { afterEach, expect, it, vi } from 'vitest';
vi.unmock('./notificationRealtime.js');
import { connectNotificationRealtime } from './notificationRealtime';

afterEach(() => { vi.restoreAllMocks(); });

it('connects to the notification hub on the current origin', async () => {
  const connection = { state: 'Disconnected', on: vi.fn(), onreconnected: vi.fn(), onclose: vi.fn(), start: vi.fn().mockResolvedValue(), stop: vi.fn().mockResolvedValue() };
  const builder = {
    withUrl: vi.fn().mockReturnThis(),
    withAutomaticReconnect: vi.fn().mockReturnThis(),
    configureLogging: vi.fn().mockReturnThis(),
    build: vi.fn(() => connection),
  };
  const realtime = connectNotificationRealtime('member', () => {}, () => builder);
  expect(builder.withUrl.mock.calls[0][0]).toBe(`${window.location.origin}/hubs/notifications`);
  expect(realtime.connected).toBe(false);
  await realtime.stop();
});

it('falls back to polling when the hub connection cannot be created', async () => {
  const realtime = connectNotificationRealtime('member', () => {}, () => { throw new Error('unsupported'); });
  expect(realtime.connected).toBe(false);
  await expect(realtime.stop()).resolves.toBeUndefined();
});
