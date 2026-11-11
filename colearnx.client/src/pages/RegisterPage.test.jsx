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
        role: 'Member',
      });
    });
  });

  it('submits the chosen Trainer role and explains certification', async () => {
    auth.register.mockResolvedValue({ emailVerificationRequired: true, message: 'Check your email.' });
    render(
      <MemoryRouter initialEntries={['/register']}>
        <AuthContext.Provider value={auth}>
          <AppRouter />
        </AuthContext.Provider>
      </MemoryRouter>,
    );

    fireEvent.click(screen.getByRole('button', { name: 'Sign up as Trainer' }));
    expect(screen.getByText(/must be certified by an administrator/i)).toBeTruthy();
    fireEvent.change(screen.getByLabelText('Full name'), { target: { value: 'New Trainer' } });
    fireEvent.change(screen.getByLabelText('Email'), { target: { value: 'new.trainer@colearnx.test' } });
    fireEvent.change(screen.getByLabelText('Password'), { target: { value: 'Password123!' } });
    fireEvent.change(screen.getByLabelText('Confirm password'), { target: { value: 'Password123!' } });
    fireEvent.click(screen.getByRole('button', { name: 'Create account' }));

    await waitFor(() => {
      expect(auth.register).toHaveBeenCalledWith(expect.objectContaining({ email: 'new.trainer@colearnx.test', role: 'Trainer' }));
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

  it('waits for email verification instead of signing the member in', async () => {
    auth.register.mockResolvedValue({
      emailVerificationRequired: true,
      message: 'Check your email to verify your account before signing in.',
    });
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
    fireEvent.change(screen.getByLabelText('Confirm password'), { target: { value: 'Password123!' } });
    fireEvent.click(screen.getByRole('button', { name: 'Create account' }));

    expect(await screen.findByText('Check your email to verify your account before signing in.')).toBeTruthy();
    expect(auth.isAuthenticated).toBe(false);
  });

  it('shows the exact password rule that failed and clears it after editing', async () => {
    render(
      <MemoryRouter initialEntries={['/register']}>
        <AuthContext.Provider value={auth}>
          <AppRouter />
        </AuthContext.Provider>
      </MemoryRouter>,
    );

    fireEvent.change(screen.getByLabelText('Full name'), { target: { value: 'New Learner' } });
    fireEvent.change(screen.getByLabelText('Email'), { target: { value: 'learner@example.test' } });
    fireEvent.change(screen.getByLabelText('Password'), { target: { value: 'weak' } });
    fireEvent.change(screen.getByLabelText('Confirm password'), { target: { value: 'weak' } });
    fireEvent.click(screen.getByRole('button', { name: 'Create account' }));

    expect(await screen.findByText(/Password must be 10–72 characters/)).toBeTruthy();
    fireEvent.change(screen.getByLabelText('Password'), { target: { value: 'StrongPass123!' } });
    expect(screen.queryByText(/Password must be 10–72 characters/)).toBeNull();
  });

  it('masks the target mailbox and explains spam-folder delivery after registration', async () => {
    auth.register.mockResolvedValue({
      emailVerificationRequired: true,
      message: 'Check your email to verify your account before signing in.',
    });
    render(
      <MemoryRouter initialEntries={['/register']}>
        <AuthContext.Provider value={auth}>
          <AppRouter />
        </AuthContext.Provider>
      </MemoryRouter>,
    );

    fireEvent.change(screen.getByLabelText('Full name'), { target: { value: 'New Learner' } });
    fireEvent.change(screen.getByLabelText('Email'), { target: { value: 'learner@example.test' } });
    fireEvent.change(screen.getByLabelText('Password'), { target: { value: 'StrongPass123!' } });
    fireEvent.change(screen.getByLabelText('Confirm password'), { target: { value: 'StrongPass123!' } });
    fireEvent.click(screen.getByRole('button', { name: 'Create account' }));

    expect(await screen.findByText('l****r@example.test')).toBeTruthy();
    expect(screen.getByText(/spam|junk/i)).toBeTruthy();
    expect(screen.getByText(/few minutes|wait/i)).toBeTruthy();
    expect(screen.queryByText('learner@example.test')).toBeNull();
  });
});

it('lets registrants reveal and hide both password fields without submitting or changing their values', () => {
  render(<MemoryRouter initialEntries={['/register']}><AuthContext.Provider value={auth}><AppRouter /></AuthContext.Provider></MemoryRouter>);
  const password = screen.getByLabelText('Password');
  const confirm = screen.getByLabelText('Confirm password');
  fireEvent.change(password, { target: { value: 'StrongPass123!' } });
  fireEvent.change(confirm, { target: { value: 'StrongPass123!' } });
  expect(password.type).toBe('password');
  expect(confirm.type).toBe('password');
  fireEvent.click(screen.getByRole('button', { name: 'Show password', exact: true }));
  expect(password.type).toBe('text');
  expect(confirm.type).toBe('password');
  fireEvent.click(screen.getByRole('button', { name: 'Show confirmation' }));
  expect(confirm.type).toBe('text');
  fireEvent.click(screen.getByRole('button', { name: 'Hide password', exact: true }));
  fireEvent.click(screen.getByRole('button', { name: 'Hide confirmation' }));
  expect(password.type).toBe('password');
  expect(confirm.type).toBe('password');
  expect(password.value).toBe('StrongPass123!');
  expect(confirm.value).toBe('StrongPass123!');
  expect(auth.register).not.toHaveBeenCalled();
});
