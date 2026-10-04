import assert from 'node:assert/strict';
import test from 'node:test';

function memoryStorage() {
  const store = new Map();
  return {
    getItem: (key) => (store.has(key) ? store.get(key) : null),
    setItem: (key, value) => { store.set(key, String(value)); },
    removeItem: (key) => { store.delete(key); },
  };
}
if (typeof globalThis.localStorage === 'undefined') globalThis.localStorage = memoryStorage();
if (typeof globalThis.sessionStorage === 'undefined') globalThis.sessionStorage = memoryStorage();
if (typeof globalThis.window === 'undefined') globalThis.window = globalThis;

const { canPreviewMaterial } = await import('../src/api/trainerLaterPhase.js');

test('Trainer preview is limited to browser-native image and PDF types', () => {
  assert.equal(canPreviewMaterial('PNG'), true);
  assert.equal(canPreviewMaterial('jpg'), true);
  assert.equal(canPreviewMaterial('PDF'), true);
  assert.equal(canPreviewMaterial('PPTX'), false);
  assert.equal(canPreviewMaterial('DOCX'), false);
});
