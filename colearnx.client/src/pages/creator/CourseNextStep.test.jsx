import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import CourseNextStep from './CourseNextStep';
import { nextStep } from './nextStepRules';

afterEach(cleanup);

const published = { status: 'Published' };
const row = (status) => ({ status });

it.each([
  ['a draft', { status: 'Draft' }, [], [], 'Step 1 of 4 · Finish and submit your course for approval', 'submit', 'Submit for approval'],
  ['a rejected course', { status: 'Rejected', reviewReason: 'Add outcomes' }, [], [], 'Changes requested: Add outcomes', 'edit', 'Edit course'],
  ['a course in review', { status: 'PendingApproval' }, [], [], 'Step 2 of 4 · Waiting for admin review', undefined, undefined],
  ['trainer applications', published, [row('Requested'), row('Requested'), row('Accepted')], [], 'Step 3 of 4 · 2 trainer applications to review', 'trainers', 'Review applications'],
  ['no accepted trainer', published, [row('Invited'), row('Declined')], [], 'Step 3 of 4 · Find a Trainer', 'invite', 'Invite trainer'],
  ['a trainer but no class', published, [row('Accepted')], [], 'Step 4 of 4 · Open your first class', 'class', 'Open a class'],
  ['scheduled classes', published, [row('Accepted')], [{ id: 1 }, { id: 2 }], 'All set · 2 classes scheduled', 'classes', 'View classes'],
])('tells the Creator what to do for %s', (_name, course, roster, classes, text, action, label) => {
  expect(nextStep(course, roster, classes)).toEqual(action ? { text, action, label } : { text });
});

it('uses the singular for one application and one class', () => {
  expect(nextStep(published, [row('Requested')], []).text).toBe('Step 3 of 4 · 1 trainer application to review');
  expect(nextStep(published, [row('Accepted')], [{ id: 1 }]).text).toBe('All set · 1 class scheduled');
});

it('waits for the trainer and class lists before advising on a published course', () => {
  expect(nextStep(published, null, [])).toBeNull();
  expect(nextStep(published, [], null)).toBeNull();
});

it('passes the action to the page when the main button is used', () => {
  const onAction = vi.fn();
  render(<CourseNextStep step={nextStep(published, [], [])} onAction={onAction} />);
  expect(screen.getByRole('status').textContent).toContain('Step 3 of 4 · Find a Trainer');
  fireEvent.click(screen.getByRole('button', { name: 'Invite trainer' }));
  expect(onAction).toHaveBeenCalledWith('invite');
});

it('shows no button while the course waits for review', () => {
  render(<CourseNextStep step={nextStep({ status: 'PendingApproval' }, null, null)} onAction={vi.fn()} />);
  expect(screen.queryByRole('button')).toBeNull();
});
