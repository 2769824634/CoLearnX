import test from 'node:test';
import assert from 'node:assert/strict';
import { utcDate } from '../src/utils/utcDates.js';

test('SQLite timestamps without an offset retain the UTC instant on Member pages', () => {
  assert.equal(utcDate('2026-09-18T06:58:10').toISOString(), '2026-09-18T06:58:10.000Z');
  assert.equal(utcDate('2026-09-18T14:58:10+08:00').toISOString(), '2026-09-18T06:58:10.000Z');
  assert.equal(utcDate('2026-09-18T06:58:10Z').toISOString(), '2026-09-18T06:58:10.000Z');
});
