import assert from 'node:assert/strict';
import test from 'node:test';
import { ApiError, apiRequest } from '../src/api/client.js';

function memoryStorage() {
  const store = new Map();
  return {
    getItem: (key) => (store.has(key) ? store.get(key) : null),
    setItem: (key, value) => { store.set(key, String(value)); },
    removeItem: (key) => { store.delete(key); },
  };
}

if (typeof globalThis.localStorage === 'undefined') {
  globalThis.localStorage = memoryStorage();
}
if (typeof globalThis.sessionStorage === 'undefined') {
  globalThis.sessionStorage = memoryStorage();
}
if (typeof globalThis.window === 'undefined') {
  globalThis.window = globalThis;
}
if (typeof globalThis.CustomEvent === 'undefined') {
  globalThis.CustomEvent = class CustomEvent {
    constructor(type, init = {}) {
      this.type = type;
      this.detail = init.detail;
    }
  };
}

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

test('authenticated 401 on a session API signs the user out', async (t) => {
  const events = [];
  const originalDispatch = window.dispatchEvent;
  t.mock.method(globalThis, 'fetch', async () => new Response(JSON.stringify({
    code: 'UNAUTHENTICATED', message: 'Sign in required.',
  }), { status: 401 }));
  window.dispatchEvent = (event) => {
    events.push(event);
    return true;
  };
  t.after(() => { window.dispatchEvent = originalDispatch; });
  localStorage.setItem('colearnx.token', 'stale-token');

  await assert.rejects(apiRequest('/api/auth/me'), (error) => error.status === 401);
  assert.equal(localStorage.getItem('colearnx.token'), null);
  assert.equal(events.length, 1);
  assert.equal(events[0].type, 'colearnx:session-replaced');
  assert.equal(events[0].detail.admin, false);
});

test('login 401 does not treat the failure as a replaced session', async (t) => {
  t.mock.method(globalThis, 'fetch', async () => new Response(JSON.stringify({
    code: 'LOGIN_FAILED', message: 'Sign-in failed.',
  }), { status: 401 }));
  localStorage.setItem('colearnx.token', 'keep-me');
  await assert.rejects(apiRequest('/api/auth/login', { token: 'test-token' }), (error) => {
    assert.equal(error.message, 'Sign-in failed.');
    assert.equal(error.status, 401);
    assert.equal(error.code, 'LOGIN_FAILED');
    assert.deepEqual(error.fieldErrors, {});
    return true;
  });
  assert.equal(localStorage.getItem('colearnx.token'), 'keep-me');
});

test('successful JSON and no-content responses are unchanged', async (t) => {
  const fetch = t.mock.method(globalThis, 'fetch', async () => new Response(JSON.stringify({ id: 1 }), { status: 201 }));
  assert.deepEqual(await apiRequest('/api/trainer/courses/1/intakes', { token: 'test-token' }), { id: 1 });
  fetch.mock.mockImplementation(async () => new Response(null, { status: 204 }));
  assert.equal(await apiRequest('/api/example', { token: 'test-token' }), null);
});
