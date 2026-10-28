import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { AuthContext } from '../../auth/AuthContext';
import { MemberDataContext } from '../member/memberDataState';
import MemberAccountPage from '../member/MemberAccountPage';
import UserAccountPage from './UserAccountPage';

vi.mock('../../components/MemberShell', () => ({ default: ({ children }) => <div>{children}</div> }));
vi.mock('./AvatarEditor', () => ({ default: () => <div>Upload avatar</div> }));
vi.mock('./ProfileEditor', () => ({ ProfileEditor: () => <div role="dialog">Profile editor</div>, ProfileDetails: () => null, IdentityVisibility: () => <div>Identity visibility details</div> }));
const user = { id: 1, email: 'person@example.test', phone: '0411222333', fullName: 'Person', displayName: 'Person', roles: ['Member'] };
function renderAccount(role, hash = '') {
  const auth = { user, refreshUser: vi.fn() };
  const state = { credits: 0, user };
  vi.stubGlobal('fetch', vi.fn(async () => new Response('[]', { status: 200, headers: { 'Content-Type': 'application/json' } })));
  render(<AuthContext.Provider value={auth}><MemberDataContext.Provider value={{ state, showToast: vi.fn() }}><MemoryRouter initialEntries={[`/${role}/account${hash}`]}>{role === 'member' ? <MemberAccountPage /> : <UserAccountPage extraKind={role} />}</MemoryRouter></MemberDataContext.Provider></AuthContext.Provider>);
}
afterEach(() => { cleanup(); vi.unstubAllGlobals(); });
describe('account shortcuts', () => {
  it.each(['member', 'trainer', 'creator'])('%s shows the full account contact details and opens profile editor', (role) => {
    renderAccount(role, '#edit-profile');
    expect(screen.getByText('person@example.test')).toBeTruthy();
    expect(screen.getByText('0411222333')).toBeTruthy();
    expect(screen.getByRole('dialog').textContent).toContain('Profile editor');
  });
  it.each(['member', 'trainer', 'creator'])('%s scrolls to role applications', (role) => {
    const scrollIntoView = vi.fn();
    Element.prototype.scrollIntoView = scrollIntoView;
    renderAccount(role, '#applications');
    expect(scrollIntoView).toHaveBeenCalledOnce();
    delete Element.prototype.scrollIntoView;
  });
});
