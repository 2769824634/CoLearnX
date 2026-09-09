import { cleanup, render, screen } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { MemoryRouter } from 'react-router-dom';
import { AuthProvider } from '../../auth/AuthContext';
import AppRouter from '../../routes/AppRouter';

const creatorUser = {
  id: 3,
  email: 'creator@colearnx.test',
  fullName: 'Creator Test',
  displayName: 'Creator',
  creditBalance: 0,
  activeRole: 'Creator',
  roles: ['Member', 'Creator'],
  identityVisibility: { member: true, creator: true },
};

function renderCreatorRoute(path) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <AuthProvider>
        <AppRouter />
      </AuthProvider>
    </MemoryRouter>,
  );
}

describe('Creator routes', () => {
  beforeEach(() => {
    localStorage.setItem('colearnx.token', 'creator-token');
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(
        new Response(JSON.stringify(creatorUser), {
          status: 200,
          headers: { 'Content-Type': 'application/json' },
        }),
      ),
    );
  });

  afterEach(() => {
    cleanup();
    localStorage.clear();
    vi.unstubAllGlobals();
  });

  it('renders the Creator course workspace at the locked courses route', async () => {
    renderCreatorRoute('/creator/courses');

    expect(
      await screen.findByRole('heading', { name: 'Course workspace' }),
    ).toBeTruthy();
    expect(screen.getByText('Course management')).toBeTruthy();
  });

  it('renders the Creator overview at the locked home route', async () => {
    renderCreatorRoute('/creator/home');

    expect(
      await screen.findByRole('heading', { name: 'Creator overview' }),
    ).toBeTruthy();
    expect(screen.getByText('Workspace status')).toBeTruthy();
  });

  it('renders the material workspace at the locked upload route', async () => {
    renderCreatorRoute('/creator/upload');

    expect(
      await screen.findByRole('heading', { name: 'Material upload workspace' }),
    ).toBeTruthy();
    expect(screen.getByText('Content library')).toBeTruthy();
  });

  it('renders the usage workspace at the locked usage route', async () => {
    renderCreatorRoute('/creator/usage');

    expect(
      await screen.findByRole('heading', { name: 'Usage records workspace' }),
    ).toBeTruthy();
    expect(screen.getByText('Material insights')).toBeTruthy();
  });

  it('renders the signed-in Creator at the locked account route', async () => {
    renderCreatorRoute('/creator/account');

    expect(
      await screen.findByRole('heading', { name: 'Creator account' }),
    ).toBeTruthy();
    expect(screen.getByText('c*****r@colearnx.test')).toBeTruthy();
    expect(screen.queryByText('creator@colearnx.test')).toBeNull();
  });
});
