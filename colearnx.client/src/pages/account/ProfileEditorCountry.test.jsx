import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { AuthContext } from '../../auth/AuthContext';
import { ProfileEditor } from './ProfileEditor';

afterEach(() => { cleanup(); vi.unstubAllGlobals(); });

it('shows the account email as read-only and saves a searchable country', async () => {
  const auth = { user: { id: 3, email: 'member@example.com', roles: ['Member'], country: 'Singapore' }, refreshUser: vi.fn() };
  const fetchMock = vi.fn(async (_path, options) => new Response(options.body, { status: 200, headers: { 'Content-Type': 'application/json' } }));
  vi.stubGlobal('fetch', fetchMock);
  render(<AuthContext.Provider value={auth}><ProfileEditor onClose={vi.fn()} /></AuthContext.Provider>);
  const email = screen.getByLabelText('Email');
  expect(email.value).toBe('member@example.com');
  expect(email.disabled || email.readOnly).toBe(true);
  const country = screen.getByLabelText('Country / Region');
  expect(country.value).toBe('Singapore');
  const options = Array.from(document.getElementById(country.getAttribute('list')).options, (option) => option.value);
  expect(options).toContain('Hong Kong (China)');
  expect(options).toContain('Taiwan (China)');
  expect(options).toContain('Afghanistan');
  expect(options).toContain('Zimbabwe');
  expect(options.length).toBeGreaterThanOrEqual(249);
  expect(options).toEqual([...options].sort((a, b) => a.localeCompare(b, 'en')));
  fireEvent.change(country, { target: { value: 'Taiwan (China)' } });
  fireEvent.click(screen.getByRole('button', { name: 'Save Changes' }));
  await waitFor(() => expect(auth.refreshUser).toHaveBeenCalled());
  const body = JSON.parse(fetchMock.mock.calls[0][1].body);
  expect(body.country).toBe('Taiwan (China)');
  expect(body).not.toHaveProperty('email');
});
