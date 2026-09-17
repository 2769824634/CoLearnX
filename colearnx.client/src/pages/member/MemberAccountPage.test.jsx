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

describe('Member role applications', () => {
  it('shows a pending Creator request and submits a Trainer application', async () => {
    const requests = [
      {
        id: 7,
        requestedRole: 'Creator',
        status: 'Pending',
        reviewNote: null,
        createdAt: '2026-09-01T00:00:00Z',
        reviewedAt: null,
      },
    ];
    renderAccount({}, async (path, options = {}) => {
      if (path === '/api/role-requests/my') return json(requests);
      if (path === '/api/role-requests' && options.method === 'POST') {
        const body = options.body;
        const requestedRole = body instanceof FormData ? body.get('requestedRole') : JSON.parse(body).requestedRole;
        const statement = body instanceof FormData ? body.get('statement') : '';
        const created = {
          id: 8,
          requestedRole,
          status: 'Pending',
          statement,
          reviewNote: null,
          createdAt: '2026-09-17T00:00:00Z',
          reviewedAt: null,
          hasResume: true,
          hasIdDocument: true,
        };
        requests.push(created);
        return json(created);
      }
      return json([]);
    });

    expect(await screen.findByText('Creator request is pending')).toBeTruthy();
    expect(screen.queryByLabelText('Resume / degree')).toBeNull();
    fireEvent.click(screen.getByRole('button', { name: 'Apply for Trainer' }));
    expect(screen.getByLabelText('Resume / degree')).toBeTruthy();
    expect(screen.getByLabelText('Application statement')).toBeTruthy();
    const resume = new File(['resume'], 'resume.pdf', { type: 'application/pdf' });
    const idDocument = new File(['id'], 'id.pdf', { type: 'application/pdf' });
    fireEvent.change(screen.getByLabelText('Resume / degree'), { target: { files: [resume] } });
    fireEvent.change(screen.getByLabelText('Identity document'), { target: { files: [idDocument] } });
    fireEvent.change(screen.getByLabelText('Application statement'), { target: { value: 'I facilitate inclusive workshops.' } });
    fireEvent.click(screen.getByRole('button', { name: 'Submit application' }));
    expect(await screen.findByText('Trainer request is pending')).toBeTruthy();
    expect(screen.queryByRole('button', { name: 'Apply for Trainer' })).toBeNull();
  });

  it('refreshes the signed-in user after a role is approved', async () => {
    const { refreshUser } = renderAccount({}, async (path) => {
      if (path === '/api/role-requests/my') {
        return json([{
          id: 9,
          requestedRole: 'Trainer',
          status: 'Approved',
          reviewNote: null,
          createdAt: '2026-09-01T00:00:00Z',
          reviewedAt: '2026-09-02T00:00:00Z',
        }]);
      }
      if (path === '/api/auth/me') {
        return json({ ...memberUser, roles: ['Member', 'Trainer'] });
      }
      return json([]);
    });

    expect(await screen.findByText(/Trainer (access approved|workspace is active)/i)).toBeTruthy();
    await waitFor(() => {
      expect(refreshUser).toHaveBeenCalled();
    });
    expect(refreshUser.mock.calls[0][0].roles).toContain('Trainer');
  });
});
