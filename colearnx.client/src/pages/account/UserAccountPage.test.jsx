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
  it.each(['trainer', 'creator'])('keeps focus during consecutive edits to every %s profile field', async (role) => {
    vi.stubGlobal('fetch', vi.fn(async () => json([])));
    render(
      <MemoryRouter initialEntries={[`/${role}/account`]}>
        <AuthContext.Provider value={authFor(role)}><AppRouter /></AuthContext.Provider>
      </MemoryRouter>,
    );
    const trigger = await screen.findByRole('button', { name: 'Edit Profile' });
    trigger.focus();
    fireEvent.click(trigger);
    const labels = ['Full Name', 'Display name', 'Phone', 'Bio',
      ...(role === 'trainer' ? ['Trainer headline', 'Specialisations'] : ['Creator headline', 'Expertise tags'])];
    for (const label of labels) {
      const field = screen.getByLabelText(label);
      field.focus();
      for (const value of ['A', 'Ab', 'Abc']) {
        fireEvent.change(field, { target: { value } });
        expect(document.activeElement).toBe(field);
        expect(field.value).toBe(value);
      }
    }
    fireEvent.click(screen.getByRole('button', { name: 'Cancel' }));
    expect(document.activeElement).toBe(trigger);
  });

  it('shows a trainer profile and saves Edit Profile changes', async () => {
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
    expect(screen.getByText('trainer@colearnx.test')).toBeTruthy();
    expect(screen.getByText('0411222333')).toBeTruthy();
    expect(screen.getByText('Intakes')).toBeTruthy();
    expect(document.querySelector('.avatar-frame').dataset.role).toBe('trainer');
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

  it('shows a creator profile with role fields in Edit Profile', async () => {
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
    expect(screen.getByText('creator@colearnx.test')).toBeTruthy();
    expect(screen.getByText('Cybersecurity, Cloud')).toBeTruthy();
    expect(document.querySelector('.avatar-frame').dataset.role).toBe('creator');
    fireEvent.click(screen.getByRole('button', { name: 'Edit Profile' }));
    expect(screen.getByLabelText('Creator headline')).toBeTruthy();
    expect(screen.getByLabelText('Expertise tags')).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Save Changes' })).toBeTruthy();
  });
});


describe('multi-role profile editing', () => {
  it.each(['trainer', 'creator'])('edits both role profiles from the %s workspace and keeps hidden data', async (role) => {
    const auth = authFor(role, { roles: ['Member', 'Trainer', 'Creator'], identityVisibility: { member: true, trainer: true, creator: true } });
    const fetchMock = vi.fn(async (path, options = {}) => {
      if (String(path) === '/api/users/3' && options.method === 'PUT') return json({ ...auth.user, ...JSON.parse(options.body) });
      return json([]);
    });
    vi.stubGlobal('fetch', fetchMock);
    render(<MemoryRouter initialEntries={[`/${role}/account`]}><AuthContext.Provider value={auth}><AppRouter /></AuthContext.Provider></MemoryRouter>);
    fireEvent.click(await screen.findByRole('button', { name: 'Edit Profile' }));
    expect(screen.getByLabelText('Trainer headline').value).toBe('Senior Trainer');
    expect(screen.getByLabelText('Creator headline').value).toBe('Content creator');
    fireEvent.change(screen.getByLabelText('Expertise tags'), { target: { value: '' } });
    fireEvent.change(screen.getByLabelText('Creator headline'), { target: { value: 'Updated creator' } });
    fireEvent.click(screen.getByRole('button', { name: 'Creator visibility' }));
    fireEvent.click(screen.getByRole('button', { name: 'Save Changes' }));
    await waitFor(() => expect(auth.refreshUser).toHaveBeenCalled());
    const call = fetchMock.mock.calls.find(([path, options]) => path === '/api/users/3' && options.method === 'PUT');
    const body = JSON.parse(call[1].body);
    expect(body.trainerHeadline).toBe('Senior Trainer');
    expect(body.creatorHeadline).toBe('Updated creator');
    expect(body.expertiseTags).toBe('');
    expect(body.identityVisibility.creator).toBe(false);
  });
});


it.each(['trainer', 'creator'])('does not submit unowned role fields from the %s account', async (role) => {
  const auth = authFor(role);
  const fetchMock = vi.fn(async (path, options = {}) => String(path) === '/api/users/3' && options.method === 'PUT' ? json(auth.user) : json([]));
  vi.stubGlobal('fetch', fetchMock);
  render(<MemoryRouter initialEntries={[`/${role}/account`]}><AuthContext.Provider value={auth}><AppRouter /></AuthContext.Provider></MemoryRouter>);
  fireEvent.click(await screen.findByRole('button', { name: 'Edit Profile' }));
  expect(screen.queryByLabelText(role === 'trainer' ? 'Creator headline' : 'Trainer headline')).toBeNull();
  fireEvent.click(screen.getByRole('button', { name: 'Save Changes' }));
  await waitFor(() => expect(auth.refreshUser).toHaveBeenCalled());
  const body = JSON.parse(fetchMock.mock.calls.find(([path, options]) => path === '/api/users/3' && options.method === 'PUT')[1].body);
  expect(body).not.toHaveProperty(role === 'trainer' ? 'creatorHeadline' : 'trainerHeadline');
  expect(body.identityVisibility).toEqual({ [role]: true });
});
