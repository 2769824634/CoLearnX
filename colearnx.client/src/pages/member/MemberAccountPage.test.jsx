import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { AuthContext } from '../../auth/AuthContext';
import { MemberDataContext } from './memberDataState';
import MemberAccountPage from './MemberAccountPage';

function json(data, status = 200) {
  return new Response(JSON.stringify(data), { status, headers: { 'Content-Type': 'application/json' } });
}

const memberUser = {
  id: 1,
  email: 'member@colearnx.test',
  fullName: 'Demo Member',
  displayName: 'Member',
  phone: '0411222333',
  bio: 'Learner',
  activeRole: 'Member',
  roles: ['Member'],
  identityVisibility: { member: true, trainer: false, creator: false },
};

function renderAccount(authExtra = {}, fetchImpl) {
  const refreshUser = vi.fn();
  const auth = {
    token: 'member-token',
    user: memberUser,
    booting: false,
    isAuthenticated: true,
    activeRole: 'member',
    roles: ['Member'],
    logout: vi.fn(),
    switchRole: vi.fn(),
    refreshUser,
    ...authExtra,
  };
  vi.stubGlobal('fetch', vi.fn(fetchImpl));
  render(
    <AuthContext.Provider value={auth}>
      <MemberDataContext.Provider value={{
        state: {
          credits: 40,
          user: memberUser,
          identityVisibility: memberUser.identityVisibility,
          prefs: { emailNotif: true, darkMode: false },
        },
        showToast: vi.fn(),
        saveProfile: vi.fn(),
      }}>
        <MemoryRouter>
          <MemberAccountPage />
        </MemoryRouter>
      </MemberDataContext.Provider>
    </AuthContext.Provider>,
  );
  return { refreshUser };
}

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe('One account, one role', () => {
  it('explains that teaching or creating needs a separate account and offers no role application', async () => {
    const fetch = vi.fn(async () => json([]));
    renderAccount({}, fetch);

    expect(await screen.findByText(/Each account holds one role/)).toBeTruthy();
    expect(screen.queryByRole('button', { name: 'Apply for Trainer' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Apply for Creator' })).toBeNull();
    expect(fetch.mock.calls.some(([path]) => String(path).startsWith('/api/role-requests'))).toBe(false);
  });
});


it('shows both role profiles and visibility controls on the member account', async () => {
  const user = { ...memberUser, roles: ['Member', 'Trainer', 'Creator'], trainerHeadline: 'Trainer title', specialisations: 'Teaching', creatorHeadline: 'Creator title', expertiseTags: 'Design', identityVisibility: { member: true, trainer: true, creator: false } };
  renderAccount({ user }, async () => json([]));
  expect(screen.getByText('Trainer title')).toBeTruthy();
  expect(screen.getByText('Creator title')).toBeTruthy();
  fireEvent.click(screen.getByRole('button', { name: 'Edit Profile' }));
  fireEvent.click(screen.getByRole('tab', { name: 'Trainer' }));
  expect(screen.getByLabelText('Trainer headline').value).toBe('Trainer title');
  fireEvent.click(screen.getByRole('tab', { name: 'Creator' }));
  expect(screen.getByLabelText('Creator headline').value).toBe('Creator title');
  expect(screen.getByRole('button', { name: 'Creator visibility' }).getAttribute('aria-pressed')).toBe('false');
});


it('saves member multi-role details in one request and refreshes the signed-in user', async () => {
  const user = { ...memberUser, roles: ['Member', 'Trainer', 'Creator'], trainerHeadline: 'Trainer title', specialisations: 'Teaching', creatorHeadline: 'Creator title', expertiseTags: 'Design' };
  const { refreshUser } = renderAccount({ user }, async (path, options = {}) => options.method === 'PUT' ? json({ ...user, ...JSON.parse(options.body) }) : json([]));
  fireEvent.click(screen.getByRole('button', { name: 'Edit Profile' }));
  fireEvent.click(screen.getByRole('tab', { name: 'Creator' }));
  fireEvent.change(screen.getByLabelText('Creator headline'), { target: { value: 'New creator' } });
  fireEvent.click(screen.getByRole('button', { name: 'Save Changes' }));
  await waitFor(() => expect(refreshUser).toHaveBeenCalled());
  expect(refreshUser.mock.calls[0][0]).toMatchObject({ trainerHeadline: 'Trainer title', creatorHeadline: 'New creator' });
});

it('limits member-only editing and submitted visibility to the member role', async () => {
  const { refreshUser } = renderAccount({}, async (path, options = {}) => options.method === 'PUT' ? json(memberUser) : json([]));
  fireEvent.click(screen.getByRole('button', { name: 'Edit Profile' }));
  expect(screen.getByRole('button', { name: 'Member visibility' })).toBeTruthy();
  expect(screen.queryByRole('button', { name: 'Trainer visibility' })).toBeNull();
  expect(screen.queryByRole('button', { name: 'Creator visibility' })).toBeNull();
  expect(screen.queryByLabelText('Trainer headline')).toBeNull();
  expect(screen.queryByLabelText('Creator headline')).toBeNull();
  fireEvent.click(screen.getByRole('button', { name: 'Save Changes' }));
  await waitFor(() => expect(refreshUser).toHaveBeenCalled());
  const call = fetch.mock.calls.find(([path, options]) => path === '/api/users/1' && options.method === 'PUT');
  expect(JSON.parse(call[1].body).identityVisibility).toEqual({ member: true });
});
