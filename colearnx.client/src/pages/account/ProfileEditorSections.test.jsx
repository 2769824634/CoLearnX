import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { AuthContext } from '../../auth/AuthContext';
import { ProfileEditor } from './ProfileEditor';

afterEach(() => { cleanup(); vi.unstubAllGlobals(); });

it('separates personal, trainer and creator details while retaining edits across sections', async () => {
  const auth = {
    user: {
      id: 3, email: 'user@example.com', roles: ['Member', 'Trainer', 'Creator'],
      identityVisibility: { member: true, trainer: true, creator: false },
      trainerHeadline: 'Trainer', creatorHeadline: 'Creator',
    },
    refreshUser: vi.fn(),
  };
  const fetchMock = vi.fn(async (_path, options) => new Response(options.body, { status: 200, headers: { 'Content-Type': 'application/json' } }));
  vi.stubGlobal('fetch', fetchMock);
  render(<AuthContext.Provider value={auth}><ProfileEditor onClose={vi.fn()} /></AuthContext.Provider>);

  expect(screen.getByLabelText('Occupation').value).toBe('unknown');
  expect(screen.queryByLabelText('Trainer headline')).toBeNull();
  expect(screen.queryByLabelText('Creator headline')).toBeNull();
  fireEvent.change(screen.getByLabelText('Occupation'), { target: { value: 'Community educator' } });
  fireEvent.click(screen.getByRole('tab', { name: 'Trainer' }));
  expect(screen.queryByLabelText('Occupation')).toBeNull();
  expect(screen.queryByLabelText('Creator headline')).toBeNull();
  fireEvent.change(screen.getByLabelText('Trainer headline'), { target: { value: 'Workshop facilitator' } });
  fireEvent.click(screen.getByRole('tab', { name: 'Creator' }));
  expect(screen.queryByLabelText('Trainer headline')).toBeNull();
  fireEvent.change(screen.getByLabelText('Creator headline'), { target: { value: 'Course designer' } });
  fireEvent.click(screen.getByRole('button', { name: 'Creator visibility' }));
  fireEvent.click(screen.getByRole('tab', { name: 'Personal info' }));
  expect(screen.getByLabelText('Occupation').value).toBe('Community educator');
  fireEvent.click(screen.getByRole('button', { name: 'Save Changes' }));
  await waitFor(() => expect(auth.refreshUser).toHaveBeenCalled());
  const body = JSON.parse(fetchMock.mock.calls[0][1].body);
  expect(body).toMatchObject({ occupation: 'Community educator', trainerHeadline: 'Workshop facilitator', creatorHeadline: 'Course designer' });
  expect(body.identityVisibility.creator).toBe(true);
});

it('only shows sections for granted roles and supports keyboard tab navigation', () => {
  const auth = { user: { id: 4, roles: ['Member', 'Trainer'] }, refreshUser: vi.fn() };
  render(<AuthContext.Provider value={auth}><ProfileEditor onClose={vi.fn()} /></AuthContext.Provider>);
  const personal = screen.getByRole('tab', { name: 'Personal info' });
  expect(screen.queryByRole('tab', { name: 'Creator' })).toBeNull();
  personal.focus();
  fireEvent.keyDown(personal, { key: 'ArrowRight' });
  const trainer = screen.getByRole('tab', { name: 'Trainer' });
  expect(trainer.getAttribute('aria-selected')).toBe('true');
  expect(document.activeElement).toBe(trainer);
  expect(screen.getByLabelText('Trainer headline')).toBeTruthy();
});
