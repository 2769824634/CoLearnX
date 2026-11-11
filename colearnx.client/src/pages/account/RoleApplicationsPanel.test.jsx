import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, render, screen } from '@testing-library/react';
import { AuthContext } from '../../auth/AuthContext';
import RoleApplicationsPanel from './RoleApplicationsPanel';

function json(data) {
  return new Response(JSON.stringify(data), { status: 200, headers: { 'Content-Type': 'application/json' } });
}

let nextId = 100;
function mount(user, requests) {
  vi.stubGlobal('fetch', vi.fn(async () => json(requests)));
  render(
    <AuthContext.Provider value={{ user: { id: nextId++, ...user }, refreshUser: vi.fn() }}>
      <RoleApplicationsPanel />
    </AuthContext.Provider>,
  );
}

afterEach(() => { cleanup(); vi.unstubAllGlobals(); });

it('does not call an uncertified role active while its certification is pending', async () => {
  mount({ roles: ['Trainer'], activeRole: 'Trainer', certifiedAt: null }, [{ id: 1, requestedRole: 'Trainer', status: 'Pending' }]);

  expect(await screen.findByText('Trainer request is pending')).toBeTruthy();
  expect(screen.queryByText('Trainer workspace is active')).toBeNull();
});

it('asks an uncertified role to finish certification', async () => {
  mount({ roles: ['Creator'], activeRole: 'Creator', certifiedAt: null }, []);

  expect(await screen.findByText('Creator certification is not complete')).toBeTruthy();
  expect(screen.queryByText('Creator workspace is active')).toBeNull();
});

it('still shows a certified role as active', async () => {
  mount({ roles: ['Trainer'], activeRole: 'Trainer', certifiedAt: '2026-11-11T00:00:00Z' }, []);

  expect(await screen.findByText('Trainer workspace is active')).toBeTruthy();
});
