import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
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

function renderDetail(course, { credits = 200 } = {}) {
  const showToast = vi.fn();
  const enrol = vi.fn();
  render(
    <AuthContext.Provider value={auth}>
      <MemberDataContext.Provider value={{
        state: { credits, wishlist: [] },
        showToast,
        loadCourseDetail: vi.fn().mockResolvedValue(course),
        enrol,
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
  return { showToast, enrol };
}

describe('Member course enrolment', () => {
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
    fireEvent.click(screen.getByRole('button', { name: 'Enrol Now' }));

    expect(enrol).not.toHaveBeenCalled();
    expect(showToast).toHaveBeenCalledWith(expect.stringMatching(/no published session/i));
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
      }],
      alreadyEnrolled: false,
    });

    expect(await screen.findByRole('heading', { name: 'ONL-1 — Online Course' })).toBeTruthy();
    expect(screen.getByText('Online')).toBeTruthy();
    expect(screen.queryByText('Full')).toBeNull();
    fireEvent.click(screen.getByRole('button', { name: 'Enrol Now' }));
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
      }],
      alreadyEnrolled: false,
    }, { credits: 5 });

    expect(await screen.findByRole('heading', { name: 'LOW-1 — Credit Course' })).toBeTruthy();
    fireEvent.click(screen.getByRole('button', { name: 'Enrol Now' }));

    expect(enrol).not.toHaveBeenCalled();
    expect(showToast).not.toHaveBeenCalled();
    expect(screen.queryByRole('heading', { name: 'Confirm Enrolment' })).toBeNull();
    expect(screen.getByRole('heading', { name: 'Insufficient Credits' })).toBeTruthy();
    expect(screen.getByText(/need 25 credits/i)).toBeTruthy();
    expect(screen.getByText(/balance is 5/i)).toBeTruthy();

    fireEvent.click(screen.getByRole('button', { name: 'Go to Payment' }));
    expect(await screen.findByRole('heading', { name: 'Credit Wallet' })).toBeTruthy();
  });
});
