import assert from 'node:assert/strict';
import test from 'node:test';
import { ApiError, apiRequest } from '../src/api/client.js';

test('validation errors retain code, HTTP status and field errors', async (t) => {
  const fields = { startsAt: ['The session must be within the Intake delivery period.'] };
  t.mock.method(globalThis, 'fetch', async () => new Response(JSON.stringify({
    code: 'SESSION_OUTSIDE_INTAKE', message: 'Check session dates.', fieldErrors: fields,
  }), { status: 400 }));
  await assert.rejects(apiRequest('/api/trainer/intakes/1/sessions', { token: 'test-token' }), (error) => {
    assert.ok(error instanceof ApiError);
    assert.equal(error.status, 400);
    assert.equal(error.code, 'SESSION_OUTSIDE_INTAKE');
    assert.deepEqual(error.fieldErrors, fields);
    return true;
  });
});

test('legacy errors without fieldErrors remain compatible', async (t) => {
  t.mock.method(globalThis, 'fetch', async () => new Response(JSON.stringify({
    code: 'LOGIN_FAILED', message: 'Sign-in failed.',
  }), { status: 401 }));
  await assert.rejects(apiRequest('/api/auth/login', { token: 'test-token' }), (error) => {
    assert.equal(error.message, 'Sign-in failed.');
    assert.equal(error.status, 401);
    assert.deepEqual(error.fieldErrors, {});
    return true;
  });
});

test('successful JSON and no-content responses are unchanged', async (t) => {
  const fetch = t.mock.method(globalThis, 'fetch', async () => new Response(JSON.stringify({ id: 1 }), { status: 201 }));
  assert.deepEqual(await apiRequest('/api/trainer/courses/1/intakes', { token: 'test-token' }), { id: 1 });
  fetch.mock.mockImplementation(async () => new Response(null, { status: 204 }));
  assert.equal(await apiRequest('/api/example', { token: 'test-token' }), null);
});
