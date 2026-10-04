import { expect, it } from 'vitest';
import { resultClass, resultLabel } from './adminDisplay';

it('keeps Pending approval on one readable line', () => {
  expect(resultLabel('PendingApproval')).toBe('Pending approval');
  expect(resultClass('PendingApproval')).toBe('pendingapproval');
});
