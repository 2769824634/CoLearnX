// One line that tells the Creator what to do next on this course:
// submit → admin review → create an Intake with its sessions → invite a trainer → published when the trainer accepts.
const OPEN = new Set(['Draft', 'Rejected']);
const INVITATION_STEPS = {
  NotSent: { text: 'Step 4 of 5 · Send the schedule to a trainer', label: 'Invite trainer' },
  Declined: { text: 'Step 4 of 5 · The trainer declined. Choose another trainer and send again', label: 'Open intake' },
  Invited: { text: 'Step 4 of 5 · Waiting for the trainer to accept the schedule', label: 'View intake' },
  // Accepting normally publishes the Intake. It only stays here when that could not happen automatically.
  Accepted: { text: 'Step 5 of 5 · The trainer accepted, but the intake is not published yet. Open it to finish', label: 'Open intake' },
};

export function nextStep(course, roster, classes) {
  if (course.status === 'Draft') return { text: 'Step 1 of 5 · Finish and submit your course for approval', action: 'submit', label: 'Submit for approval' };
  if (course.status === 'Rejected') return { text: `Changes requested: ${course.reviewReason || 'see the review note'}`, action: 'edit', label: 'Edit course' };
  if (course.status === 'PendingApproval') return { text: 'Step 2 of 5 · Waiting for admin review' };
  // Trainers and intakes only matter once the course is published, and both lists must have arrived first.
  if (course.status !== 'Published' || !roster || !classes) return null;
  // Only Trainers who joined the course can be invited to an Intake.
  if (!roster.some((row) => row.status === 'Accepted')) {
    return { text: 'Step 3 of 5 · Find trainers for this course', action: 'invite', label: 'Invite trainer' };
  }
  if (!classes.length) return { text: 'Step 3 of 5 · Create an Intake and schedule its sessions', action: 'class', label: 'Create an intake' };
  // The list arrives newest first; follow the newest Intake that is not published yet.
  const open = classes.find((item) => OPEN.has(item.status));
  const step = open && INVITATION_STEPS[open.trainerInvitationStatus || 'NotSent'];
  if (step) return { ...step, action: 'intake', intakeId: open.id };
  return { text: `All set · ${classes.length} intake${classes.length === 1 ? '' : 's'} scheduled`, action: 'classes', label: 'View intakes' };
}
