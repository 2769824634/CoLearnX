import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import CourseNextStep from './CourseNextStep';
import { nextStep } from './nextStepRules';

afterEach(cleanup);

const published = { status: 'Published' };
const row = (status) => ({ status });
const intake = (id, status, trainerInvitationStatus) => ({ id, status, trainerInvitationStatus });

it.each([
  ['a draft', { status: 'Draft' }, [], [], 'Step 1 of 5 · Finish and submit your course for approval', 'submit', 'Submit for approval'],
  ['a rejected course', { status: 'Rejected', reviewReason: 'Add outcomes' }, [], [], 'Changes requested: Add outcomes', 'edit', 'Edit course'],
  ['a course in review', { status: 'PendingApproval' }, [], [], 'Step 2 of 5 · Waiting for admin review', undefined, undefined],
  ['no trainer who can teach it', published, [row('Invited'), row('Declined')], [], 'Step 3 of 5 · Find trainers for this course', 'invite', 'Invite trainer'],
  ['a trainer but no intake', published, [row('Accepted')], [], 'Step 3 of 5 · Create an Intake and schedule its sessions', 'class', 'Create an intake'],
  ['an applicant but no intake', published, [row('Requested')], [], 'Step 3 of 5 · Create an Intake and schedule its sessions', 'class', 'Create an intake'],
  ['scheduled intakes', published, [row('Accepted')], [intake(2, 'Published', 'Accepted'), intake(1, 'Completed', 'Accepted')], 'All set · 2 intakes scheduled', 'classes', 'View intakes'],
])('tells the Creator what to do for %s', (_name, course, roster, classes, text, action, label) => {
  expect(nextStep(course, roster, classes)).toEqual(action ? { text, action, label } : { text });
});

it.each([
  ['NotSent', 'Step 4 of 5 · Send the schedule to a trainer', 'Invite trainer'],
  ['Declined', 'Step 4 of 5 · The trainer declined. Choose another trainer and send again', 'Open intake'],
  ['Invited', 'Step 4 of 5 · Waiting for the trainer to accept the schedule', 'View intake'],
  ['Accepted', 'Step 5 of 5 · The trainer accepted. Publish the intake', 'Publish intake'],
])('follows the newest unpublished intake when its trainer invitation is %s', (invitation, text, label) => {
  const classes = [intake(7, 'Draft', invitation), intake(3, 'Published', 'Accepted')];
  expect(nextStep(published, [row('Accepted')], classes)).toEqual({ text, action: 'intake', label, intakeId: 7 });
});

it('uses the singular for one intake', () => {
  expect(nextStep(published, [row('Accepted')], [intake(1, 'Published', 'Accepted')]).text).toBe('All set · 1 intake scheduled');
});

it('waits for the trainer and intake lists before advising on a published course', () => {
  expect(nextStep(published, null, [])).toBeNull();
  expect(nextStep(published, [], null)).toBeNull();
});

it('passes the step to the page when the main button is used', () => {
  const onAction = vi.fn();
  const step = nextStep(published, [], []);
  render(<CourseNextStep step={step} onAction={onAction} />);
  expect(screen.getByRole('status').textContent).toContain('Step 3 of 5 · Find trainers for this course');
  fireEvent.click(screen.getByRole('button', { name: 'Invite trainer' }));
  expect(onAction).toHaveBeenCalledWith('invite', step);
});

it('shows no button while the course waits for review', () => {
  render(<CourseNextStep step={nextStep({ status: 'PendingApproval' }, null, null)} onAction={vi.fn()} />);
  expect(screen.queryByRole('button')).toBeNull();
});
