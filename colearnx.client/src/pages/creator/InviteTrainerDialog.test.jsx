import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, within } from '@testing-library/react';
import { AuthContext } from '../../auth/AuthContext';
import InviteTrainerDialog from './InviteTrainerDialog';

const auth = { user: { id: 1 }, isAuthenticated: true, activeRole: 'creator' };
const json = (data, status = 200) => new Response(JSON.stringify(data), { status, headers: { 'Content-Type': 'application/json' } });
const candidates = [
  { id: 2, fullName: 'Alex Chen', headline: 'Coach', specialisations: 'Accessibility', specialties: ['Digital Product Design'] },
  { id: 6, fullName: 'Erin Dual', headline: 'Facilitator', specialisations: 'Workshops', specialties: ['Digital Product Design'] },
];

function mount(props = {}, results = candidates) {
  const fetch = vi.fn(async () => json(results));
  vi.stubGlobal('fetch', fetch);
  const onInvite = vi.fn();
  render(<AuthContext.Provider value={{ ...auth, token: `creator-token-${Math.random()}` }}>
    <InviteTrainerDialog courseId={9} onInvite={onInvite} onClose={vi.fn()} {...props} />
  </AuthContext.Provider>);
  return { fetch, onInvite };
}

afterEach(() => { cleanup(); vi.unstubAllGlobals(); });

it('lists certified trainers as soon as it opens, filters as the Creator types, and invites the chosen one', async () => {
  const { fetch, onInvite } = mount();
  const dialog = screen.getByRole('dialog', { name: 'Invite a trainer' });

  const erin = (await within(dialog).findByText('Erin Dual')).closest('li');
  expect(fetch.mock.calls.map(([path]) => path)).toEqual(['/api/creator/trainers?courseId=9&search=']);
  fireEvent.change(within(dialog).getByLabelText('Certified trainers in this specialty'), { target: { value: 'er' } });
  await vi.waitFor(() => expect(fetch.mock.calls.map(([path]) => path)).toContain('/api/creator/trainers?courseId=9&search=er'));

  fireEvent.click(within(erin).getByRole('button', { name: 'Invite' }));
  expect(onInvite).toHaveBeenCalledWith(6);
});

it('says so when no certified trainer has this specialty yet', async () => {
  mount({}, []);
  expect(await screen.findByText('No certified trainers in this specialty yet.')).toBeTruthy();
});

it('shows why an invitation failed and disables inviting while one is sent', async () => {
  mount({ busy: true, error: Object.assign(new Error('This Trainer is not certified in the specialty of this Course.'), { code: 'TRAINER_SUBJECT_MISMATCH' }) });
  expect((await screen.findByRole('alert')).textContent).toContain('not certified in the specialty');
  expect((await screen.findAllByRole('button', { name: 'Invite' })).every((button) => button.disabled)).toBe(true);
});
