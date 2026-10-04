import { expect, it } from 'vitest';
import { formatCount } from './businessPresentation';

it('quantity labels use a singular noun for one item', () => {
  expect(formatCount(0, 'credit')).toBe('0 credits');
  expect(formatCount(1, 'credit')).toBe('1 credit');
  expect(formatCount(2, 'learner')).toBe('2 learners');
});
