import assert from 'node:assert/strict';
import test from 'node:test';
import { buildChangeRequest, reviewPayload } from '../src/pages/trainer/b4Workflow.js';

test('buildChangeRequest keeps server session IDs and strips local editor keys', () => {
  const intake = { version: 'v1' };
  const proposal = {
    registrationOpensAt: '2027-01-01T00:00:00.000Z', registrationClosesAt: '2027-01-02T00:00:00.000Z',
    startsAt: '2027-01-03T00:00:00.000Z', endsAt: '2027-01-04T00:00:00.000Z',
    sessions: [{ id: 8, _key: 'existing-8', label: 'Workshop' }, { _key: 'new-1', label: 'Clinic' }],
  };
  assert.deepEqual(buildChangeRequest(intake, proposal), {
    registrationOpensAt: proposal.registrationOpensAt, registrationClosesAt: proposal.registrationClosesAt,
    startsAt: proposal.startsAt, endsAt: proposal.endsAt, version: 'v1',
    sessions: [{ id: 8, label: 'Workshop' }, { id: null, label: 'Clinic' }],
  });
});

test('reviewPayload trims the note and carries the application version', () => {
  assert.deepEqual(reviewPayload('Reject', '  Move session two  ', 'version-2'), {
    decision: 'Reject', confirmationNote: 'Move session two', version: 'version-2',
  });
  assert.deepEqual(reviewPayload('Confirm', '   ', 'version-3'), {
    decision: 'Confirm', confirmationNote: null, version: 'version-3',
  });
});
