import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { AuthContext } from '../auth/AuthContext';
import MemberShell from './MemberShell';
import UserAvatar from './UserAvatar';

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

describe('UserAvatar', () => {
  it('shows initials when no photo is uploaded', () => {
    render(<UserAvatar name="Huang Yousheng" />);
    expect(screen.getByText('HY')).toBeTruthy();
  });

  it('shows the uploaded photo in the member top bar', async () => {
    vi.stubGlobal('URL', {
      ...URL,
      createObjectURL: vi.fn(() => 'blob:avatar-preview'),
      revokeObjectURL: vi.fn(),
    });
    vi.stubGlobal('fetch', vi.fn(async () => new Response(new Blob(['img'], { type: 'image/png' }))));
    const auth = {
      token: 'member-token',
      user: { id: 4, fullName: 'Huang Yousheng', avatarUrl: '/api/users/4/avatar?v=abc' },
      logout: vi.fn(),
    };
    render(
      <MemoryRouter>
        <AuthContext.Provider value={auth}>
          <MemberShell title="Home">content</MemberShell>
        </AuthContext.Provider>
      </MemoryRouter>,
    );

    const photo = await screen.findByAltText('Huang Yousheng');
    expect(photo.getAttribute('src')).toBe('blob:avatar-preview');
    await waitFor(() => {
      expect(fetch).toHaveBeenCalledWith('/api/users/4/avatar?v=abc', expect.objectContaining({
        headers: expect.objectContaining({ Authorization: 'Bearer member-token' }),
      }));
    });
  });
});
