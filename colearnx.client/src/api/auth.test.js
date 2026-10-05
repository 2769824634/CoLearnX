import { expect, it, vi } from 'vitest';
import { authApi } from './index';

it('sends token-only password resets and keeps the legacy email argument optional', async () => {
  const fetch = vi.spyOn(globalThis, 'fetch').mockImplementation(async () => (
    new Response(JSON.stringify({ message: 'ok' }), { headers: { 'Content-Type': 'application/json' } })
  ));
  try {
    await authApi.resetPassword('reset-token', 'StrongPass123!');
    expect(JSON.parse(fetch.mock.calls[0][1].body)).toEqual({
      token: 'reset-token',
      newPassword: 'StrongPass123!',
    });

    await authApi.resetPassword('legacy-token', 'Changed789!', 'legacy@example.test');
    expect(JSON.parse(fetch.mock.calls[1][1].body)).toEqual({
      token: 'legacy-token',
      newPassword: 'Changed789!',
      email: 'legacy@example.test',
    });
  } finally {
    fetch.mockRestore();
  }
});
