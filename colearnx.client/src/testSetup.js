import { beforeEach } from 'vitest';
import { clearReadCache } from './api/readCache';

beforeEach(() => {
  clearReadCache();
});
