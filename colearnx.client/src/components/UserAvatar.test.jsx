import { afterEach, describe, expect, it, vi } from 'vitest';
import { act, cleanup, render, screen, waitFor } from '@testing-library/react';
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

  it('does not render the previous photo while the avatar identity changes', async () => {
    let objectUrlNumber = 0;
    vi.stubGlobal('URL', {
      ...URL,
      createObjectURL: vi.fn(() => `blob:avatar-${++objectUrlNumber}`),
      revokeObjectURL: vi.fn(),
    });
    function deferred() {
      let resolve;
      const promise = new Promise((next) => { resolve = next; });
      return { promise, resolve };
    }
    const first = deferred();
    const second = deferred();
    let requestNumber = 0;
    vi.stubGlobal('fetch', vi.fn(() => {
      requestNumber += 1;
      return requestNumber === 1 ? first.promise : second.promise;
    }));

    const { rerender } = render(
      <UserAvatar name="First User" avatarUrl="/api/users/4/avatar" token="member-token-a" />,
    );
    await waitFor(() => expect(fetch).toHaveBeenCalledTimes(1));
    await act(async () => {
      first.resolve(new Response(new Blob(['first'], { type: 'image/png' })));
    });
    expect((await screen.findByAltText('First User')).getAttribute('src')).toBe('blob:avatar-1');

    rerender(
      <UserAvatar name="Second User" avatarUrl="/api/users/4/avatar" token="member-token-b" />,
    );
    expect(screen.queryByAltText('First User')).toBeNull();
    expect(screen.getByText('SU')).toBeTruthy();
    expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob:avatar-1');
    await waitFor(() => expect(fetch).toHaveBeenCalledTimes(2));
    await act(async () => {
      second.resolve(new Response(new Blob(['second'], { type: 'image/png' })));
    });
    expect((await screen.findByAltText('Second User')).getAttribute('src')).toBe('blob:avatar-2');
  });
});
