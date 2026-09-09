import assert from 'node:assert/strict';
import test from 'node:test';
import { adminLaterPhaseApi } from '../src/api/adminLaterPhase.js';
import { trainerLaterPhaseApi } from '../src/api/trainerLaterPhase.js';

test('trainer attendance sends the intake-scoped batch contract', async (t) => {
  const fetch = t.mock.method(globalThis, 'fetch', async () => new Response(JSON.stringify([]), { status: 200 }));
  await trainerLaterPhaseApi.saveAttendance('trainer-token', 12, 34, [{ enrollmentId: 56, status: 'Present' }]);
  const [path, options] = fetch.mock.calls[0].arguments;
  assert.equal(path, '/api/trainer/intakes/12/sessions/34/attendance');
  assert.equal(options.method, 'PUT');
  assert.equal(options.headers.Authorization, 'Bearer trainer-token');
  assert.deepEqual(JSON.parse(options.body), { records: [{ enrollmentId: 56, status: 'Present' }] });
});

test('trainer grading targets an assessment and enrollment without client-owned identity', async (t) => {
  const fetch = t.mock.method(globalThis, 'fetch', async () => new Response(JSON.stringify({ score: 88 }), { status: 200 }));
  await trainerLaterPhaseApi.grade('trainer-token', 7, 9, { score: 88, feedback: 'Pass' });
  const [path, options] = fetch.mock.calls[0].arguments;
  assert.equal(path, '/api/trainer/assessments/7/grades/9');
  assert.equal(options.method, 'PUT');
  assert.deepEqual(JSON.parse(options.body), { score: 88, feedback: 'Pass' });
});

test('admin mutations preserve the idempotency key in the JSON body', async (t) => {
  const fetch = t.mock.method(globalThis, 'fetch', async () => new Response(JSON.stringify({ balanceAfter: 115 }), { status: 200 }));
  const key = '11111111-1111-1111-1111-111111111111';
  await adminLaterPhaseApi.adjustCredits('admin-token', { userId: 4, delta: 15, reason: 'Correction', idempotencyKey: key });
  let [path, options] = fetch.mock.calls[0].arguments;
  assert.equal(path, '/api/admin/credits/adjustments');
  assert.equal(options.headers.Authorization, 'Bearer admin-token');
  assert.equal(JSON.parse(options.body).idempotencyKey, key);

  await adminLaterPhaseApi.reviewDispute('admin-token', 22, { decision: 'Refund', refundCredits: 20, reason: 'Duplicate', idempotencyKey: key });
  [path, options] = fetch.mock.calls[1].arguments;
  assert.equal(path, '/api/admin/disputes/22/review');
  assert.equal(JSON.parse(options.body).idempotencyKey, key);
});
