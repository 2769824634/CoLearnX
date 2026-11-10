import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { AuthContext } from '../../auth/AuthContext';
import { apiRequest } from '../../api/client';
import useTrainerQuery from './useTrainerQuery';

afterEach(() => cleanup());

const loader = vi.fn(async () => 'ready');

function Probe() {
  const query = useTrainerQuery(loader, 'nav');
  if (query.loading) return <p>loading</p>;
  return <button type="button" onClick={query.refresh}>{query.data}</button>;
}

function mount() {
  return render(
    <AuthContext.Provider value={{ token: 'trainer' }}>
      <Probe />
    </AuthContext.Provider>,
  );
}

it('reuses the loaded page when the sidebar opens it again', async () => {
  loader.mockClear();
  const first = mount();
  expect(await screen.findByRole('button', { name: 'ready' })).toBeTruthy();
  expect(loader).toHaveBeenCalledTimes(1);
  first.unmount();
  mount();
  expect(screen.getByRole('button', { name: 'ready' })).toBeTruthy();
  expect(loader).toHaveBeenCalledTimes(1);
});

it('loads again only when refresh is requested', async () => {
  loader.mockClear();
  mount();
  fireEvent.click(await screen.findByRole('button', { name: 'ready' }));
  await waitFor(() => expect(loader).toHaveBeenCalledTimes(2));
});

it('loads the page again after a change was saved through the API', async () => {
  loader.mockClear();
  const first = mount();
  expect(await screen.findByRole('button', { name: 'ready' })).toBeTruthy();
  first.unmount();
  vi.stubGlobal('fetch', vi.fn(async () => new Response(JSON.stringify({ id: 1 }), { status: 201 })));
  try {
    await apiRequest('/api/creator/courses', { method: 'POST', body: {}, token: 'trainer' });
    const callsAfterSave = loader.mock.calls.length;
    await apiRequest('/api/creator/courses', { token: 'trainer' });
    mount();
    await waitFor(() => expect(loader).toHaveBeenCalledTimes(callsAfterSave + 1));
  } finally {
    vi.unstubAllGlobals();
  }
});
