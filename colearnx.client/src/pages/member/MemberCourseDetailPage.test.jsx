import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { AuthContext } from '../../auth/AuthContext';
import { MemberDataContext } from './memberDataState';
import MemberCourseDetailPage from './MemberCourseDetailPage';

const auth = {
  token: 'member-token',
  user: {
    id: 1,
    email: 'member@colearnx.test',
    fullName: 'Demo Member',
    displayName: 'Member',
    activeRole: 'Member',
    roles: ['Member'],
  },
  booting: false,
  isAuthenticated: true,
  activeRole: 'member',
  logout: vi.fn(),
  switchRole: vi.fn(),
};

afterEach(() => {
  cleanup();
});

function renderDetail(course, { credits = 200, loadCourseDetail, enrol } = {}) {
  const showToast = vi.fn();
  const enrolFn = enrol || vi.fn().mockResolvedValue({ creditsSpent: 20, balance: 180, heldAfter: 20 });
  const loadFn = loadCourseDetail || vi.fn().mockResolvedValue(course);
  render(
    <AuthContext.Provider value={auth}>
      <MemberDataContext.Provider value={{
        state: { credits, wishlist: [] },
        showToast,
        loadCourseDetail: loadFn,
        enrol: enrolFn,
        toggleWish: vi.fn(),
      }}>
        <MemoryRouter initialEntries={[`/member/courses/${course.id}`]}>
          <Routes>
            <Route path="/member/courses/:courseId" element={<MemberCourseDetailPage />} />
            <Route path="/member/payment" element={<h1>Credit Wallet</h1>} />
          </Routes>
        </MemoryRouter>
      </MemberDataContext.Provider>
    </AuthContext.Provider>,
  );
  return { showToast, enrol: enrolFn, loadCourseDetail: loadFn };
}

describe('Member course enrolment', () => {
  const baseCourse = { id: 15, code: 'STATUS-1', title: 'Registration states', trainer: 'Trainer',
    credits: 20, outcomes: [], alreadyEnrolled: false };
  const openSession = { id: 60, label: 'Open session', when: 'Upcoming', capacity: 10, seats: 3,
    startsAt: '2099-02-01T01:00:00Z', endsAt: '2099-02-01T03:00:00Z',
    physical: true, intakeStatus: 'Published', intakeEnrollmentCount: 7, minEnrollment: 10,
    registrationOpensAt: '2000-01-01T00:00:00Z', registrationClosesAt: '2099-01-01T00:00:00Z' };

  it.each([
    [{ seats: 0 }, 'Session full'],
    [{ intakeStatus: 'Cancelled' }, 'Class cancelled'],
    [{ cancelledAt: '2026-01-01T00:00:00Z' }, 'Class cancelled'],
    [{ registrationOpensAt: '2098-01-01T00:00:00Z' }, 'Registration not open'],
    [{ registrationClosesAt: '2001-01-01T00:00:00Z' }, 'Registration closed'],
    [{ physicalBookingDeadline: '2001-01-01T00:00:00Z' }, 'Physical booking closed'],
    [{ confirmedToRunAt: '2026-01-01T00:00:00Z' }, 'Class confirmed'],
    [{ intakeStatus: 'InProgress' }, 'Class in progress'],
    [{ intakeStatus: 'Completed' }, 'Class completed'],
  ])('disables reservations for unavailable sessions: %s', async (changes, label) => {
    const { enrol } = renderDetail({ ...baseCourse, sessions: [{ ...openSession, ...changes }] });
    const button = await screen.findByRole('button', { name: label, exact: true });
    expect(button.disabled).toBe(true);
    fireEvent.click(button);
    expect(enrol).not.toHaveBeenCalled();
    expect(screen.queryByRole('heading', { name: 'Confirm Enrolment' })).toBeNull();
  });

  it('selects an open alternative and shows the whole Intake count', async () => {
    renderDetail({ ...baseCourse, sessions: [
      { ...openSession, id: 59, label: 'Cancelled session', intakeStatus: 'Cancelled' },
      openSession,
    ] });
    const reserve = await screen.findByRole('button', { name: 'Reserve place' });
    expect(reserve.disabled).toBe(false);
    expect(screen.getAllByText(/7 learners reserved or enrolled · Minimum 10 learners/).length).toBe(2);
    fireEvent.click(reserve);
    expect(screen.getByRole('heading', { name: 'Confirm Enrolment' })).toBeTruthy();
  });

  it('does not crash when a published course has no sessions yet', async () => {
    const { showToast, enrol } = renderDetail({
      id: 9,
      code: 'NEW-1',
      title: 'Fresh Course',
      trainer: 'Trainer',
      credits: 20,
      outcomes: ['Learn the basics'],
      sessions: [],
      alreadyEnrolled: false,
    });

    expect(await screen.findByRole('heading', { name: 'NEW-1 — Fresh Course' })).toBeTruthy();
    const reserve = screen.getByRole('button', { name: 'No available sessions' });
    expect(reserve.disabled).toBe(true);
    fireEvent.click(reserve);

    expect(enrol).not.toHaveBeenCalled();
    expect(showToast).not.toHaveBeenCalled();
    expect(showToast).not.toHaveBeenCalledWith(expect.stringMatching(/Cannot read properties/i));
    expect(screen.queryByRole('heading', { name: 'Confirm Enrolment' })).toBeNull();
  });

  it('does not treat a default online session as full', async () => {
    const { showToast, enrol } = renderDetail({
      id: 10,
      code: 'ONL-1',
      title: 'Online Course',
      trainer: 'Trainer',
      credits: 20,
      outcomes: ['Join live'],
      sessions: [{
        id: 44,
        label: 'Session 1',
        when: '10 Sep 2026, 09:00 – 11:00',
        seats: 0,
        capacity: 0,
        physical: false,
        startsAt: '2099-02-01T01:00:00Z', endsAt: '2099-02-01T03:00:00Z',
      }],
      alreadyEnrolled: false,
    });

    expect(await screen.findByRole('heading', { name: 'ONL-1 — Online Course' })).toBeTruthy();
    expect(screen.getByText('Online')).toBeTruthy();
    expect(screen.queryByText('Full')).toBeNull();
    fireEvent.click(screen.getByRole('button', { name: 'Reserve place' }));
    expect(showToast).not.toHaveBeenCalledWith('Session full');
    expect(enrol).not.toHaveBeenCalled();
    expect(screen.getByRole('button', { name: 'Confirm Enrolment' })).toBeTruthy();
  });

  it('opens a modal when credits are too low and goes to payment from it', async () => {
    const { showToast, enrol } = renderDetail({
      id: 11,
      code: 'LOW-1',
      title: 'Credit Course',
      trainer: 'Trainer',
      credits: 25,
      outcomes: ['Need more credits'],
      sessions: [{
        id: 51,
        label: 'Session 1',
        when: '10 Sep 2026, 09:00 – 11:00',
        seats: 8,
        capacity: 10,
        physical: true,
        startsAt: '2099-02-01T01:00:00Z', endsAt: '2099-02-01T03:00:00Z',
      }],
      alreadyEnrolled: false,
    }, { credits: 5 });

    expect(await screen.findByRole('heading', { name: 'LOW-1 — Credit Course' })).toBeTruthy();
    fireEvent.click(screen.getByRole('button', { name: 'Reserve place' }));

    expect(enrol).not.toHaveBeenCalled();
    expect(showToast).not.toHaveBeenCalled();
    expect(screen.queryByRole('heading', { name: 'Confirm Enrolment' })).toBeNull();
    expect(screen.getByRole('heading', { name: 'Insufficient Credits' })).toBeTruthy();
    expect(screen.getByText(/need 25 credits/i)).toBeTruthy();
    expect(screen.getByText(/balance is 5/i)).toBeTruthy();

    fireEvent.click(screen.getByRole('button', { name: 'Go to Payment' }));
    expect(await screen.findByRole('heading', { name: 'Credit Wallet' })).toBeTruthy();
  });

  it('refreshes the Intake count after a successful reservation without submitting again', async () => {
    const after = {
      ...baseCourse,
      alreadyEnrolled: true,
      sessions: [{ ...openSession, seats: 2, intakeEnrollmentCount: 8 }],
    };
    const loadCourseDetail = vi.fn()
      .mockResolvedValueOnce({ ...baseCourse, sessions: [openSession] })
      .mockResolvedValueOnce(after);
    const { enrol } = renderDetail({ ...baseCourse, sessions: [openSession] }, { loadCourseDetail });
    fireEvent.click(await screen.findByRole('button', { name: 'Reserve place' }));
    fireEvent.click(screen.getByRole('button', { name: 'Confirm Enrolment' }));
    expect(await screen.findByText(/8 learners reserved or enrolled · Minimum 10 learners/)).toBeTruthy();
    expect(enrol).toHaveBeenCalledTimes(1);
    await waitFor(() => expect(loadCourseDetail).toHaveBeenCalledTimes(2));
  });

  it('keeps a successful reservation visible when the post-write detail refresh fails', async () => {
    const loadCourseDetail = vi.fn()
      .mockResolvedValueOnce({ ...baseCourse, sessions: [openSession] })
      .mockRejectedValueOnce(new Error('detail refresh unavailable'))
      .mockRejectedValueOnce(new Error('detail refresh still unavailable'));
    const { enrol, showToast } = renderDetail({ ...baseCourse, sessions: [openSession] }, { loadCourseDetail });

    fireEvent.click(await screen.findByRole('button', { name: 'Reserve place' }));
    fireEvent.click(screen.getByRole('button', { name: 'Confirm Enrolment' }));

    expect(await screen.findByRole('heading', { name: 'Enrolment Successful' })).toBeTruthy();
    expect(screen.getByRole('alert').textContent).toMatch(/reservation was saved/i);
    expect(screen.getByRole('button', { name: 'Open My Programs' })).toBeTruthy();
    expect(screen.queryByRole('button', { name: 'Reserve place' })).toBeNull();
    expect(showToast).not.toHaveBeenCalledWith(expect.stringMatching(/Could not reserve your place/i));
    expect(enrol).toHaveBeenCalledTimes(1);
    fireEvent.click(screen.getByRole('button', { name: 'Close' }));
    expect(screen.queryByRole('button', { name: 'Reserve place' })).toBeNull();
    fireEvent.click(screen.getByRole('button', { name: 'Refresh course' }));
    await waitFor(() => expect(loadCourseDetail).toHaveBeenCalledTimes(3));
    expect(enrol).toHaveBeenCalledTimes(1);
  });

  it('shows the sync prompt when member data reload reports failure after reservation', async () => {
    const enrol = vi.fn().mockResolvedValue({ creditsSpent: 20, balance: 180, heldAfter: 20, memberDataRefreshed: false });
    const loadCourseDetail = vi.fn()
      .mockResolvedValueOnce({ ...baseCourse, sessions: [openSession] })
      .mockResolvedValue({ ...baseCourse, alreadyEnrolled: true, sessions: [{ ...openSession, intakeEnrollmentCount: 8 }] });
    const { loadCourseDetail: loadDetail } = renderDetail({ ...baseCourse, sessions: [openSession] }, {
      enrol,
      loadCourseDetail,
    });

    fireEvent.click(await screen.findByRole('button', { name: 'Reserve place' }));
    fireEvent.click(screen.getByRole('button', { name: 'Confirm Enrolment' }));

    expect(await screen.findByRole('heading', { name: 'Enrolment Successful' })).toBeTruthy();
    expect(screen.getByRole('alert').textContent).toMatch(/member data could not refresh/i);
    expect(loadDetail).toHaveBeenCalledTimes(2);
  });
});
