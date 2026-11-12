import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import CourseNextStep from './CourseNextStep';
import { nextStep } from './nextStepRules';

afterEach(cleanup);

const published = { status: 'Published' };
const intake = (id, status, trainerInvitationStatus) => ({ id, status, trainerInvitationStatus });

it.each([
  ['a draft', { status: 'Draft' }, [], 'Step 1 of 5 · Finish and submit your course for approval', 'submit', 'Submit for approval'],
  ['a rejected course', { status: 'Rejected', reviewReason: 'Add outcomes' }, [], 'Changes requested: Add outcomes', 'edit', 'Edit course'],
  ['a course in review', { status: 'PendingApproval' }, [], 'Step 2 of 5 · Waiting for admin review', undefined, undefined],
  ['an approved course without an intake', published, [], 'Step 3 of 5 · Create an Intake and schedule its sessions', 'class', 'Create an intake'],
  ['an approved course whose only intake was cancelled', published, [intake(4, 'Cancelled', 'Accepted')], 'Step 3 of 5 · Create an Intake and schedule its sessions', 'class', 'Create an intake'],
  ['scheduled intakes', published, [intake(2, 'Published', 'Accepted'), intake(1, 'Completed', 'Accepted')], 'All set · 2 intakes scheduled', 'classes', 'View intakes'],
])('tells the Creator what to do for %s', (_name, course, classes, text, action, label) => {
  expect(nextStep(course, classes)).toEqual(action ? { text, action, label } : { text });
});

it.each([
  ['NotSent', 'Step 4 of 5 · Invite a trainer to this intake', 'Invite trainer'],
  ['Declined', 'Step 4 of 5 · The trainer declined. Invite another trainer', 'Open intake'],
  ['Invited', 'Step 4 of 5 · Waiting for the trainer to accept the schedule', 'View intake'],
  ['Accepted', 'Step 5 of 5 · The trainer accepted, but the intake is not published yet. Open it to finish', 'Open intake'],
])('follows the newest unpublished intake when its trainer invitation is %s', (invitation, text, label) => {
  const classes = [intake(7, 'Draft', invitation), intake(3, 'Published', 'Accepted')];
  expect(nextStep(published, classes)).toEqual({ text, action: 'intake', label, intakeId: 7 });
});

it('uses the singular for one intake', () => {
  expect(nextStep(published, [intake(1, 'Published', 'Accepted')]).text).toBe('All set · 1 intake scheduled');
});

it('waits for the intake list before advising on a published course', () => {
  expect(nextStep(published, null)).toBeNull();
});

it('passes the step to the page when the main button is used', () => {
  const onAction = vi.fn();
  const step = nextStep(published, [intake(7, 'Draft', 'NotSent')]);
  render(<CourseNextStep step={step} onAction={onAction} />);
  expect(screen.getByRole('status').textContent).toContain('Step 4 of 5 · Invite a trainer to this intake');
  fireEvent.click(screen.getByRole('button', { name: 'Invite trainer' }));
  expect(onAction).toHaveBeenCalledWith('intake', step);
});

it('shows no button while the course waits for review', () => {
  render(<CourseNextStep step={nextStep({ status: 'PendingApproval' }, null)} onAction={vi.fn()} />);
  expect(screen.queryByRole('button')).toBeNull();
});
