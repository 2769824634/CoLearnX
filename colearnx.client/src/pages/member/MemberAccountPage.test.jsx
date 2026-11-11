import { afterEach, expect, it, vi } from 'vitest';
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

it('does not offer role applications from a Member account', () => {
  renderAccount({}, async () => json([]));
  expect(screen.queryByRole('button', { name: 'Apply for Trainer' })).toBeNull();
  expect(screen.queryByRole('button', { name: 'Apply for Creator' })).toBeNull();
  expect(screen.queryByText('Workspace applications')).toBeNull();
  expect(fetch.mock.calls.some(([path]) => path === '/api/role-requests/my')).toBe(false);
});

it('shows existing role profiles on the member account', async () => {
  const user = { ...memberUser, roles: ['Member', 'Trainer', 'Creator'], trainerHeadline: 'Trainer title', specialisations: 'Teaching', creatorHeadline: 'Creator title', expertiseTags: 'Design' };
  renderAccount({ user }, async () => json([]));
  expect(screen.getByText('Trainer title')).toBeTruthy();
  expect(screen.getByText('Creator title')).toBeTruthy();
  fireEvent.click(screen.getByRole('button', { name: 'Edit Profile' }));
  fireEvent.click(screen.getByRole('tab', { name: 'Trainer' }));
  expect(screen.getByLabelText('Trainer headline').value).toBe('Trainer title');
  fireEvent.click(screen.getByRole('tab', { name: 'Creator' }));
  expect(screen.getByLabelText('Creator headline').value).toBe('Creator title');
  expect(screen.queryByRole('button', { name: 'Creator visibility' })).toBeNull();
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

it('limits member-only editing to member profile fields', async () => {
  const { refreshUser } = renderAccount({}, async (path, options = {}) => options.method === 'PUT' ? json(memberUser) : json([]));
  fireEvent.click(screen.getByRole('button', { name: 'Edit Profile' }));
  expect(screen.queryByRole('button', { name: 'Member visibility' })).toBeNull();
  expect(screen.queryByRole('button', { name: 'Trainer visibility' })).toBeNull();
  expect(screen.queryByRole('button', { name: 'Creator visibility' })).toBeNull();
  expect(screen.queryByLabelText('Trainer headline')).toBeNull();
  expect(screen.queryByLabelText('Creator headline')).toBeNull();
  fireEvent.click(screen.getByRole('button', { name: 'Save Changes' }));
  await waitFor(() => expect(refreshUser).toHaveBeenCalled());
});
