import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { AuthContext } from '../../auth/AuthContext';
import CertificationPage from './CertificationPage';

const specialties = Array.from({ length: 6 }, (_, i) => ({ id: i + 1, name: `Specialty ${i + 1}`, area: 'DigitalTechnology' }));

function json(data, status = 200) {
  return new Response(JSON.stringify(data), { status, headers: { 'Content-Type': 'application/json' } });
}

function mount(requests = []) {
  const fetch = vi.fn(async (path, init = {}) => {
    if (path === '/api/role-requests/my') return json(requests);
    if (path === '/api/role-requests/specialties') return json(specialties);
    if (path === '/api/role-requests' && init.method === 'POST') {
      return json({ id: 5, requestedRole: 'Trainer', status: 'Pending', specialties: ['Specialty 1'] }, 201);
    }
    return json(null);
  });
  vi.stubGlobal('fetch', fetch);
  render(
    <MemoryRouter>
      <AuthContext.Provider value={{ user: { certifiedAt: null }, refreshUser: vi.fn(), logout: vi.fn() }}>
        <CertificationPage role="Trainer" />
      </AuthContext.Provider>
    </MemoryRouter>,
  );
  return fetch;
}

afterEach(() => { cleanup(); vi.unstubAllGlobals(); });

it('limits the specialty choice to five', async () => {
  mount();
  const boxes = await screen.findAllByRole('checkbox');
  boxes.slice(0, 5).forEach((box) => fireEvent.click(box));
  expect(boxes[5].disabled).toBe(true);
  expect(screen.getByText('Specialties (5/5)')).toBeTruthy();
});

it('hides the form while an application is pending', async () => {
  mount([{ id: 4, requestedRole: 'Trainer', status: 'Pending', specialties: ['Specialty 2'] }]);
  expect(await screen.findByText(/Waiting for administrator review/)).toBeTruthy();
  expect(screen.queryByRole('button', { name: 'Submit for certification' })).toBeNull();
  expect(screen.getByText('Specialty 2')).toBeTruthy();
});

it('shows the rejection reason and allows a new application', async () => {
  mount([{ id: 4, requestedRole: 'Trainer', status: 'Rejected', reviewNote: 'Unreadable ID' }]);
  expect(await screen.findByText(/Unreadable ID/)).toBeTruthy();
  await waitFor(() => expect(screen.getByRole('button', { name: 'Submit for certification' })).toBeTruthy());
});
