import { describe, expect, it } from 'vitest';
import { shareTotals } from './shareTotals';

describe('shareTotals', () => {
  it('nets each role after a refund reversal', () => {
    const rows = [
      { userId: 2, userName: 'Creator', userEmail: 'creator@example.test', description: 'Content share · UX · enrollment 9', delta: 8 },
      { userId: 3, userName: 'Trainer', userEmail: 'trainer@example.test', description: 'Delivery share · UX · enrollment 9', delta: 2 },
      { userId: 1, userName: 'CoLearnX', userEmail: 'colearnx@system.colearnx', description: 'Platform share · UX · enrollment 9', delta: 10 },
      { userId: 1, userName: 'CoLearnX', userEmail: 'colearnx@system.colearnx', description: 'Platform share · UX · enrollment 4', delta: 5 },
      { userId: 2, userName: 'Creator', userEmail: 'creator@example.test', description: 'Reversal of Content share · UX · enrollment 9', delta: -6 },
    ];
    expect(shareTotals(rows)).toEqual([
      { userId: 1, name: 'CoLearnX', email: 'colearnx@system.colearnx', content: 0, delivery: 0, platform: 15, net: 15 },
      { userId: 2, name: 'Creator', email: 'creator@example.test', content: 2, delivery: 0, platform: 0, net: 2 },
      { userId: 3, name: 'Trainer', email: 'trainer@example.test', content: 0, delivery: 2, platform: 0, net: 2 },
    ]);
  });
});
