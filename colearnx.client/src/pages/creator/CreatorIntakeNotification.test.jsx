import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { AuthContext } from '../../auth/AuthContext';
import CreatorIntakeApplicationDetailPage from './CreatorIntakeApplicationDetailPage';

const query = vi.hoisted(() => ({ data: null, loading: false, error: null }));
vi.mock('../trainer/useTrainerQuery', () => ({ default: () => query }));
afterEach(cleanup);

it('opens a legacy Intake without an application as a read-only current schedule', () => {
  query.data = { application: null, proposedChange: null, currentIntake: {
    id: 42, courseId: 4, status: 'Cancelled', minEnrollment: 10,
    registrationOpensAt: '2026-09-01T00:00:00Z', registrationClosesAt: '2026-09-20T00:00:00Z',
    startsAt: '2026-10-20T00:00:00Z', endsAt: '2026-10-21T00:00:00Z', sessions: [],
  } };
  render(<AuthContext.Provider value={{ user: { id: 8 } }}><MemoryRouter><CreatorIntakeApplicationDetailPage /></MemoryRouter></AuthContext.Provider>);
  expect(screen.getByRole('heading', { name: 'Intake #42' })).toBeTruthy();
  expect(screen.getByText('Cancelled')).toBeTruthy();
  expect(screen.getByText('10')).toBeTruthy();
  expect(screen.queryByRole('button', { name: 'Approve sessions' })).toBeNull();
  expect(screen.getByText(/No application record/)).toBeTruthy();
});

it('shows minimum enrollment, meeting URL and physical booking deadline for review', () => {
  query.data = { application: {
    courseIntakeId: 7, courseCode: 'CLXL1004R1', courseTitle: 'Local repair course', trainerId: 8, trainerName: 'Trainer G',
    kind: 'New', status: 'Pending', version: 'v1',
  }, proposedChange: null, currentIntake: {
    id: 7, courseId: 7, status: 'PendingApproval', minEnrollment: 2, confirmationNote: null,
    registrationOpensAt: '2026-09-01T00:00:00Z', registrationClosesAt: '2026-09-20T00:00:00Z',
    startsAt: '2026-10-20T01:00:00Z', endsAt: '2026-10-20T03:00:00Z',
    sessions: [{
      id: 9, label: 'Session 9', startsAt: '2026-10-20T01:00:00Z', endsAt: '2026-10-20T03:00:00Z',
      meetingLink: 'https://meet.example.test/room-9', physicalAddress: 'Studio 3', physicalCapacity: 3,
      physicalBookingDeadline: '2026-10-19T01:00:00Z',
    }],
  } };
  render(<AuthContext.Provider value={{ user: { id: 8 } }}><MemoryRouter><CreatorIntakeApplicationDetailPage /></MemoryRouter></AuthContext.Provider>);
  expect(screen.getByText('2')).toBeTruthy();
  expect(screen.getByRole('link', { name: 'https://meet.example.test/room-9' })).toBeTruthy();
  expect(screen.getByText(/Booking closes/)).toBeTruthy();
  expect(screen.getByText(/You teach this course/)).toBeTruthy();
});

it('uses a singular seat label and records a completed decision clearly', () => {
  query.data = { application: {
    courseIntakeId: 8, courseCode: 'SINGLE-SEAT', courseTitle: 'Single seat', trainerName: 'Trainer G',
    kind: 'New', status: 'Rejected', version: 'v1',
  }, proposedChange: null, currentIntake: {
    id: 8, courseId: 8, status: 'Rejected', minEnrollment: 2, confirmationNote: 'Please revise the link.',
    registrationOpensAt: '2026-09-01T00:00:00Z', registrationClosesAt: '2026-09-20T00:00:00Z',
    startsAt: '2026-10-20T01:00:00Z', endsAt: '2026-10-20T03:00:00Z',
    sessions: [{ id: 1, label: 'Seat', startsAt: '2026-10-20T01:00:00Z', endsAt: '2026-10-20T03:00:00Z', physicalAddress: 'Studio', physicalCapacity: 1, physicalBookingDeadline: '2026-10-19T01:00:00Z' }],
  } };
  render(<AuthContext.Provider value={{ user: { id: 8 } }}><MemoryRouter><CreatorIntakeApplicationDetailPage /></MemoryRouter></AuthContext.Provider>);
  expect(screen.getByText(/1 seat · Booking closes/)).toBeTruthy();
  expect(screen.getByText('Decision recorded')).toBeTruthy();
});
