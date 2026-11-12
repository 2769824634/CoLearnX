// One line that tells the Creator what to do next on this course.
export function nextStep(course, roster, classes) {
  if (course.status === 'Draft') return { text: 'Step 1 of 4 · Finish and submit your course for approval', action: 'submit', label: 'Submit for approval' };
  if (course.status === 'Rejected') return { text: `Changes requested: ${course.reviewReason || 'see the review note'}`, action: 'edit', label: 'Edit course' };
  if (course.status === 'PendingApproval') return { text: 'Step 2 of 4 · Waiting for admin review' };
  // Trainers and classes only matter once the course is published, and both lists must have arrived first.
  if (course.status !== 'Published' || !roster || !classes) return null;
  const requested = roster.filter((row) => row.status === 'Requested').length;
  if (requested > 0) return { text: `Step 3 of 4 · ${requested} trainer application${requested === 1 ? '' : 's'} to review`, action: 'trainers', label: 'Review applications' };
  if (!roster.some((row) => row.status === 'Accepted')) return { text: 'Step 3 of 4 · Find a Trainer', action: 'invite', label: 'Invite trainer' };
  if (!classes.length) return { text: 'Step 4 of 4 · Open your first class', action: 'class', label: 'Open a class' };
  return { text: `All set · ${classes.length} class${classes.length === 1 ? '' : 'es'} scheduled`, action: 'classes', label: 'View classes' };
}
