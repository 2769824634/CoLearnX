import { expect, it } from 'vitest';
import { formatCount, formatUtcDate } from './businessPresentation';

it('quantity labels use a singular noun for one item', () => {
  expect(formatCount(0, 'credit')).toBe('0 credits');
  expect(formatCount(1, 'credit')).toBe('1 credit');
  expect(formatCount(-1, 'credit')).toBe('-1 credit');
  expect(formatCount(2, 'learner')).toBe('2 learners');
});

it('formats date-only business labels in UTC', () => {
  expect(formatUtcDate('2099-02-01T23:30:00Z')).toMatch(/01 Feb 2099/);
  expect(formatUtcDate('2099-02-01T23:30:00Z')).toMatch(/UTC/);
});
