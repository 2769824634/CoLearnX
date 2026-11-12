// One line that tells the Creator what to do next on this course:
// submit → admin review → create an Intake with its sessions → invite a trainer from the Intake → published when they accept.
const OPEN = new Set(['Draft', 'Rejected']);
const INVITATION_STEPS = {
  NotSent: { text: 'Step 4 of 5 · Invite a trainer to this intake', label: 'Invite trainer' },
  Declined: { text: 'Step 4 of 5 · The trainer declined. Invite another trainer', label: 'Open intake' },
  Invited: { text: 'Step 4 of 5 · Waiting for the trainer to accept the schedule', label: 'View intake' },
  // Accepting normally publishes the Intake. It only stays here when that could not happen automatically.
  Accepted: { text: 'Step 5 of 5 · The trainer accepted, but the intake is not published yet. Open it to finish', label: 'Open intake' },
};

export function nextStep(course, classes) {
  if (course.status === 'Draft') return { text: 'Step 1 of 5 · Finish and submit your course for approval', action: 'submit', label: 'Submit for approval' };
  if (course.status === 'Rejected') return { text: `Changes requested: ${course.reviewReason || 'see the review note'}`, action: 'edit', label: 'Edit course' };
  if (course.status === 'PendingApproval') return { text: 'Step 2 of 5 · Waiting for admin review' };
  // Intakes only matter once the course is published, and their list must have arrived first.
  if (course.status !== 'Published' || !classes) return null;
  const live = classes.filter((item) => item.status !== 'Cancelled');
  if (!live.length) return { text: 'Step 3 of 5 · Create an Intake and schedule its sessions', action: 'class', label: 'Create an intake' };
  // The list arrives newest first; follow the newest Intake that is not published yet.
  const open = live.find((item) => OPEN.has(item.status));
  const step = open && INVITATION_STEPS[open.trainerInvitationStatus || 'NotSent'];
  if (step) return { ...step, action: 'intake', intakeId: open.id };
  return { text: `All set · ${live.length} intake${live.length === 1 ? '' : 's'} scheduled`, action: 'classes', label: 'View intakes' };
}
