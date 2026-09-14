import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { AuthContext } from '../../auth/AuthContext';
import AppRouter from '../../routes/AppRouter';

function json(data) {
  return new Response(JSON.stringify(data), { status: 200, headers: { 'Content-Type': 'application/json' } });
}

function authFor(role, extra = {}) {
  const titled = role.charAt(0).toUpperCase() + role.slice(1);
  return {
    token: `${role}-token`,
    user: {
      id: 3,
      email: `${role}@colearnx.test`,
      fullName: `${titled} Demo`,
      displayName: titled,
      phone: '0411222333',
      bio: `${titled} bio`,
      activeRole: titled,
      roles: [titled],
      identityVisibility: { [role]: true },
      trainerHeadline: 'Senior Trainer',
      specialisations: 'UI/UX Design',
      creatorHeadline: 'Content creator',
      expertiseTags: 'Cybersecurity, Cloud',
      ...extra,
    },
    booting: false,
    isAuthenticated: true,
    activeRole: role,
    roles: [titled],
    logout: vi.fn(),
    switchRole: vi.fn(),
    refreshUser: vi.fn(),
  };
}

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe('Trainer and Creator My Account', () => {
  it('shows a masked trainer profile and saves Edit Profile changes', async () => {
    const auth = authFor('trainer');
    vi.stubGlobal('fetch', vi.fn(async (path, options = {}) => {
      if (path === '/api/trainer/intakes') return json([{ id: 9, status: 'Published' }, { id: 10, status: 'Draft' }]);
      if (path === '/api/courses') return json([]);
      if (String(path) === '/api/users/3' && options.method === 'PUT') {
        const body = JSON.parse(options.body);
        return json({
          ...auth.user,
          fullName: body.fullName,
          trainerHeadline: body.trainerHeadline,
          specialisations: body.specialisations,
        });
      }
      return json([]);
    }));

    render(
      <MemoryRouter initialEntries={['/trainer/account']}>
        <AuthContext.Provider value={auth}>
          <AppRouter />
        </AuthContext.Provider>
      </MemoryRouter>,
    );

    expect(await screen.findByRole('heading', { name: 'Profile / My Account' })).toBeTruthy();
    expect(screen.getByText('t****r@colearnx.test')).toBeTruthy();
    expect(screen.getByText('****2333')).toBeTruthy();
    expect(screen.getByText('Intakes')).toBeTruthy();
    fireEvent.click(screen.getByRole('button', { name: 'Edit Profile' }));
    fireEvent.change(screen.getByLabelText('Full Name'), { target: { value: 'Gu Yincheng' } });
    fireEvent.change(screen.getByLabelText('Trainer headline'), { target: { value: 'Lead workshop trainer' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save Changes' }));

    await waitFor(() => {
      expect(auth.refreshUser).toHaveBeenCalled();
    });
    expect(auth.refreshUser.mock.calls[0][0].fullName).toBe('Gu Yincheng');
    expect(auth.refreshUser.mock.calls[0][0].trainerHeadline).toBe('Lead workshop trainer');
  });

  it('shows a masked creator profile with role fields in Edit Profile', async () => {
    const auth = authFor('creator');
    vi.stubGlobal('fetch', vi.fn(async (path) => {
      if (path === '/api/creator/courses') {
        return json([{ id: 17, status: 'Published' }, { id: 18, status: 'Draft' }]);
      }
      return json([]);
    }));

    render(
      <MemoryRouter initialEntries={['/creator/account']}>
        <AuthContext.Provider value={auth}>
          <AppRouter />
        </AuthContext.Provider>
      </MemoryRouter>,
    );

    expect(await screen.findByRole('heading', { name: 'Profile / My Account' })).toBeTruthy();
    expect(screen.getByText('c****r@colearnx.test')).toBeTruthy();
    expect(screen.getByText('Cybersecurity, Cloud')).toBeTruthy();
    fireEvent.click(screen.getByRole('button', { name: 'Edit Profile' }));
    expect(screen.getByLabelText('Creator headline')).toBeTruthy();
    expect(screen.getByLabelText('Expertise tags')).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Save Changes' })).toBeTruthy();
  });
});
