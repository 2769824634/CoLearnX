import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { AuthContext } from '../auth/AuthContext';
import AppRouter from '../routes/AppRouter';

const auth = {
  token: null,
  user: null,
  booting: false,
  isAuthenticated: false,
  activeRole: null,
  roles: [],
  login: vi.fn(),
  register: vi.fn(),
  logout: vi.fn(),
  switchRole: vi.fn(),
};

afterEach(() => {
  cleanup();
  auth.register.mockReset();
});

describe('Account registration', () => {
  it('links login to create account and registers a member', async () => {
    auth.register.mockResolvedValue({
      id: 9,
      email: 'new.learner@colearnx.test',
      fullName: 'New Learner',
      activeRole: 'Member',
    });

    render(
      <MemoryRouter initialEntries={['/login']}>
        <AuthContext.Provider value={auth}>
          <AppRouter />
        </AuthContext.Provider>
      </MemoryRouter>,
    );

    fireEvent.click(screen.getByRole('link', { name: 'Create an account' }));
    expect(await screen.findByRole('heading', { name: 'Create your account' })).toBeTruthy();

    fireEvent.change(screen.getByLabelText('Full name'), { target: { value: 'New Learner' } });
    fireEvent.change(screen.getByLabelText('Email'), { target: { value: 'new.learner@colearnx.test' } });
    fireEvent.change(screen.getByLabelText('Password'), { target: { value: 'Password123!' } });
    fireEvent.change(screen.getByLabelText('Confirm password'), { target: { value: 'Password123!' } });
    fireEvent.click(screen.getByRole('button', { name: 'Create account' }));

    await waitFor(() => {
      expect(auth.register).toHaveBeenCalledWith({
        email: 'new.learner@colearnx.test',
        password: 'Password123!',
        fullName: 'New Learner',
      });
    });
  });

  it('blocks submit when passwords do not match', async () => {
    render(
      <MemoryRouter initialEntries={['/register']}>
        <AuthContext.Provider value={auth}>
          <AppRouter />
        </AuthContext.Provider>
      </MemoryRouter>,
    );

    fireEvent.change(screen.getByLabelText('Full name'), { target: { value: 'New Learner' } });
    fireEvent.change(screen.getByLabelText('Email'), { target: { value: 'new.learner@colearnx.test' } });
    fireEvent.change(screen.getByLabelText('Password'), { target: { value: 'Password123!' } });
    fireEvent.change(screen.getByLabelText('Confirm password'), { target: { value: 'Password123?' } });
    fireEvent.click(screen.getByRole('button', { name: 'Create account' }));

    expect(await screen.findByText('Passwords do not match.')).toBeTruthy();
    expect(auth.register).not.toHaveBeenCalled();
  });
});
