export function buildChangeRequest(intake, proposal) {
  return {
    registrationOpensAt: proposal.registrationOpensAt,
    registrationClosesAt: proposal.registrationClosesAt,
    startsAt: proposal.startsAt,
    endsAt: proposal.endsAt,
    sessions: proposal.sessions.map((item) => ({
      id: item.id ?? null,
      ...Object.fromEntries(Object.entries(item).filter(([key]) => key !== '_key' && key !== 'id')),
    })),
    version: intake.version,
  };
}

export function reviewPayload(decision, confirmationNote, version) {
  return { decision, confirmationNote: confirmationNote.trim() || null, version };
}

export function initialChangeProposal(intake) {
  const source = intake.status === 'Rejected' && intake.latestChangeRequest
    ? intake.latestChangeRequest : intake;
  return {
    registrationOpensAt: source.registrationOpensAt,
    registrationClosesAt: source.registrationClosesAt,
    startsAt: source.startsAt,
    endsAt: source.endsAt,
    sessions: source.sessions.map((session, index) => ({ ...session, _key: session.id ? `server-${session.id}` : `draft-${index}` })),
  };
}
