import { beforeEach, vi } from 'vitest';
import { clearReadCache } from './api/readCache';

vi.mock('./components/notificationRealtime.js', () => ({
  connectNotificationRealtime: () => ({ stop() {} }),
}));

beforeEach(() => {
  clearReadCache();
});
