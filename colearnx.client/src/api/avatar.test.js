import { expect, it, vi } from 'vitest';
import { usersApi } from './index';

it('uploads avatar as multipart data and reads the image with the signed-in token', async () => {
  const fetch = vi.spyOn(globalThis, 'fetch').mockImplementation(async (path) => path.includes('/avatar?')
    ? new Response('image', { headers: { 'Content-Type': 'image/png' } })
    : new Response(JSON.stringify({ avatarUrl: '/api/users/7/avatar?v=1' }), { headers: { 'Content-Type': 'application/json' } }));
  try {
    const result = await usersApi.uploadAvatar(7, new Blob(['image'], { type: 'image/png' }), 'member-token');
    const image = await usersApi.avatar(result.avatarUrl, 'member-token');
    expect(result.avatarUrl).toBe('/api/users/7/avatar?v=1');
    expect(image.type).toBe('image/png');
    expect(fetch.mock.calls[0][0]).toBe('/api/users/7/avatar');
    expect(fetch.mock.calls[0][1].headers.Authorization).toBe('Bearer member-token');
    expect(fetch.mock.calls[0][1].body).toBeInstanceOf(FormData);
    expect(fetch.mock.calls[1][0]).toBe('/api/users/7/avatar?v=1');
  } finally {
    fetch.mockRestore();
  }
});
