import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { AuthContext } from '../../auth/AuthContext';
import { authApi, certificatesApi, coursesApi, creditsApi, enrollmentsApi } from '../../api';
import { MemberDataProvider } from './MemberDataContext';
import { useMemberSlices } from './memberDataState';

vi.mock('../../api', () => ({
  authApi: { me: vi.fn() },
  coursesApi: { list: vi.fn(), get: vi.fn(), addWishlist: vi.fn(), removeWishlist: vi.fn() },
  enrollmentsApi: {
    my: vi.fn(),
    enrol: vi.fn(),
    cancelReservation: vi.fn(),
    withdraw: vi.fn(),
    acceptPostponement: vi.fn(),
  },
  creditsApi: { packages: vi.fn(), myLedger: vi.fn() },
  certificatesApi: { my: vi.fn() },
  usersApi: { update: vi.fn() },
}));

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

function mount(page, refreshUser = vi.fn()) {
  render(
    <AuthContext.Provider value={{ user: { id: 1, creditBalance: 100, heldCredits: 5 }, refreshUser }}>
      <MemberDataProvider>{page}</MemberDataProvider>
    </AuthContext.Provider>,
  );
  return refreshUser;
}

function BillingProbe() {
  useMemberSlices('billing');
  return <p>wallet</p>;
}

function CatalogProbe() {
  useMemberSlices('catalog');
  return <p>catalog</p>;
}

function EnrolProbe() {
  const { enrol } = useMemberSlices('enrollments');
  return <button type="button" onClick={() => enrol(4, 8)}>Enrol</button>;
}

it('loads only wallet data on the payment page', async () => {
  creditsApi.packages.mockResolvedValue([]);
  creditsApi.myLedger.mockResolvedValue([]);
  mount(<BillingProbe />);
  await waitFor(() => expect(creditsApi.packages).toHaveBeenCalledTimes(1));
  expect(creditsApi.myLedger).toHaveBeenCalledTimes(1);
  expect(coursesApi.list).not.toHaveBeenCalled();
  expect(enrollmentsApi.my).not.toHaveBeenCalled();
  expect(certificatesApi.my).not.toHaveBeenCalled();
  expect(authApi.me).not.toHaveBeenCalled();
});

it('loads only the catalog on the course list', async () => {
  coursesApi.list.mockResolvedValue([]);
  mount(<CatalogProbe />);
  await waitFor(() => expect(coursesApi.list).toHaveBeenCalledTimes(1));
  expect(creditsApi.packages).not.toHaveBeenCalled();
  expect(enrollmentsApi.my).not.toHaveBeenCalled();
  expect(certificatesApi.my).not.toHaveBeenCalled();
});

it('updates the wallet from the enrolment result and refreshes enrolments only', async () => {
  enrollmentsApi.my.mockResolvedValue([]);
  enrollmentsApi.enrol.mockResolvedValue({
    enrollmentId: 9,
    creditsSpent: 20,
    balanceAfter: 80,
    heldAfter: 25,
    status: 'Reserved',
  });
  const refreshUser = mount(<EnrolProbe />);
  await waitFor(() => expect(enrollmentsApi.my).toHaveBeenCalledTimes(1));
  fireEvent.click(screen.getByRole('button', { name: 'Enrol' }));
  await waitFor(() => expect(enrollmentsApi.my).toHaveBeenCalledTimes(2));
  expect(coursesApi.list).not.toHaveBeenCalled();
  expect(creditsApi.packages).not.toHaveBeenCalled();
  expect(certificatesApi.my).not.toHaveBeenCalled();
  expect(authApi.me).not.toHaveBeenCalled();
  expect(refreshUser).toHaveBeenCalledWith(expect.objectContaining({
    creditBalance: 80,
    heldCredits: 25,
    totalCredits: 105,
  }));
});

it('does not request the catalog again when the member area is opened twice', async () => {
  coursesApi.list.mockResolvedValue([{
    id: 4, code: 'UX', title: 'UX', trainerName: 'Teacher', creditCost: 10, level: 'Beginner',
    category: 'Design', isFeatured: false, inWishlist: false, interests: [], trainerNames: [], ratingCount: 0,
  }]);
  const { unmount } = render(
    <AuthContext.Provider value={{ user: { id: 1, creditBalance: 100, heldCredits: 0 }, token: 'member', refreshUser: vi.fn() }}>
      <MemberDataProvider><CatalogProbe /></MemberDataProvider>
    </AuthContext.Provider>,
  );
  await waitFor(() => expect(coursesApi.list).toHaveBeenCalledTimes(1));
  unmount();
  render(
    <AuthContext.Provider value={{ user: { id: 1, creditBalance: 100, heldCredits: 0 }, token: 'member', refreshUser: vi.fn() }}>
      <MemberDataProvider><CatalogProbe /></MemberDataProvider>
    </AuthContext.Provider>,
  );
  await screen.findByText('catalog');
  expect(coursesApi.list).toHaveBeenCalledTimes(1);
});
