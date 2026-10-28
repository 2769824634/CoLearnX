import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { MemoryRouter, useLocation } from 'react-router-dom';
import { ShellChrome } from './ShellChrome';

function Location() { const location = useLocation(); return <output data-testid="location">{location.pathname + location.hash}</output>; }
function renderShell(role = 'member') {
  const logout = vi.fn();
  render(<MemoryRouter initialEntries={[`/${role}/home`]}><ShellChrome role={role} identityName="Test User" logout={logout} navLabel="Navigation" navHintId="nav-hint" navItems={null}><Location /></ShellChrome></MemoryRouter>);
  return logout;
}
afterEach(cleanup);
describe('account menu', () => {
  it.each(['member', 'trainer', 'creator'])('opens for %s and navigates to the account shortcuts', (role) => {
    renderShell(role);
    fireEvent.click(screen.getByRole('button', { name: 'Account menu' }));
    expect(screen.getByRole('link', { name: 'My Account' }).getAttribute('href')).toBe(`/${role}/account`);
    expect(screen.getByRole('link', { name: 'Change password' }).getAttribute('href')).toBe('/forgot-password');
    fireEvent.click(screen.getByRole('link', { name: 'Edit Profile' }));
    expect(screen.getByTestId('location').textContent).toBe(`/${role}/account#edit-profile`);
    fireEvent.click(screen.getByRole('button', { name: 'Account menu' }));
    fireEvent.click(screen.getByRole('link', { name: 'Identity applications & status' }));
    expect(screen.getByTestId('location').textContent).toBe(`/${role}/account#applications`);
  });
  it('closes on Escape and outside click, and logs out from the menu', () => {
    const logout = renderShell();
    const trigger = screen.getByRole('button', { name: 'Account menu' });
    fireEvent.click(trigger);
    fireEvent.keyDown(document, { key: 'Escape' });
    expect(screen.queryByRole('link', { name: 'My Account' })).toBeNull();
    fireEvent.click(trigger);
    fireEvent.pointerDown(document.body);
    expect(screen.queryByRole('link', { name: 'My Account' })).toBeNull();
    fireEvent.click(trigger);
    fireEvent.click(screen.getByRole('button', { name: 'Log out' }));
    expect(logout).toHaveBeenCalledOnce();
  });
  it('does not move or trap focus when the menu opens', () => {
    renderShell();
    const trigger = screen.getByRole('button', { name: 'Account menu' });
    trigger.focus();
    fireEvent.click(trigger);
    expect(document.activeElement).toBe(trigger);
    const next = screen.getByRole('link', { name: 'Change password' });
    next.focus();
    expect(document.activeElement).toBe(next);
    fireEvent.keyDown(next, { key: 'Tab' });
    expect(document.activeElement).toBe(next);
  });
  it('keeps the existing logout control for admin', () => {
    renderShell('admin');
    expect(screen.getByRole('button', { name: 'Log out' })).toBeTruthy();
    expect(screen.queryByRole('button', { name: 'Account menu' })).toBeNull();
  });
});
