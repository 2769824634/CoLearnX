// Where the trainer stands on an unpublished Intake's schedule, as labels and status tones.
export const INVITATION = {
  NotSent: { label: 'Trainer not invited', tone: 'draft' },
  Invited: { label: 'Waiting for trainer', tone: 'pendingapproval' },
  Accepted: { label: 'Trainer accepted', tone: 'published' },
  Declined: { label: 'Trainer declined', tone: 'rejected' },
};

export const invitationOf = (intake) => INVITATION[intake?.trainerInvitationStatus] || INVITATION.NotSent;

// Draft and Rejected Intakes have not been published yet.
export const isUnpublished = (intake) => intake?.status === 'Draft' || intake?.status === 'Rejected';
