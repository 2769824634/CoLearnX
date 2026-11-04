import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { adminStaffApi } from '../../api';
import useAdminAuth from '../../auth/useAdminAuth';
import AdminStaffPage from './AdminStaffPage';

vi.mock('../../api', () => ({
  adminStaffApi: {
    list: vi.fn(),
    detail: vi.fn(),
    document: vi.fn(),
    resetPassword: vi.fn(),
    provision: vi.fn(),
    setActive: vi.fn(),
  },
}));
vi.mock('../../auth/useAdminAuth');

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

it('lists staff and shows the one-time initial password after provisioning', async () => {
  useAdminAuth.mockReturnValue({ token: 'admin-token' });
  adminStaffApi.list.mockResolvedValue([{
    id: 1,
    staffNumber: 1,
    fullName: 'Trainer One',
    displayName: 'CoLearnX 讲师 001',
    email: 'one@example.com',
    isActive: true,
  }]);
  adminStaffApi.provision.mockResolvedValue({
    initialPassword: 'Stf!Initial123',
    staff: {
      id: 2,
      staffNumber: 2,
      fullName: 'Trainer Two',
      displayName: 'CoLearnX 讲师 002',
      email: 'two@example.com',
      isActive: true,
    },
  });

  render(<AdminStaffPage />);

  expect(await screen.findByText('CoLearnX 讲师 001')).toBeTruthy();
  fireEvent.change(screen.getByLabelText('Email'), { target: { value: 'two@example.com' } });
  fireEvent.change(screen.getByLabelText('Full name'), { target: { value: 'Trainer Two' } });
  fireEvent.change(screen.getByLabelText('Staff document'), {
    target: { files: [new File(['proof'], 'proof.pdf', { type: 'application/pdf' })] },
  });
  fireEvent.submit(screen.getByRole('button', { name: 'Provision staff account' }).closest('form'));

  await waitFor(() => expect(adminStaffApi.provision).toHaveBeenCalled());
  expect(await screen.findByText('Stf!Initial123')).toBeTruthy();
  expect(screen.getByText('CoLearnX 讲师 002')).toBeTruthy();
});

it('opens employee details from the row without toggling account access', async () => {
  useAdminAuth.mockReturnValue({ token: 'admin-details' });
  const account = { id: 15, staffNumber: 15, fullName: 'Details Staff', displayName: 'Employee 015', email: 'details@example.com', phone: '12345', isActive: true };
  adminStaffApi.list.mockResolvedValue([account]);
  adminStaffApi.detail.mockResolvedValue({ account, hasDocument: true, courses: [], intakes: [] });
  render(<AdminStaffPage />);
  fireEvent.click((await screen.findByText('Details Staff')).closest('tr'));
  expect(await screen.findByRole('dialog', { name: 'Employee details' })).toBeTruthy();
  expect(await screen.findByText('12345')).toBeTruthy();
  expect(screen.getByRole('button', { name: 'Download staff document' })).toBeTruthy();
  expect(adminStaffApi.setActive).not.toHaveBeenCalled();
});
