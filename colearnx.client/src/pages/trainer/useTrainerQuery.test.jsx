import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { AuthContext } from '../../auth/AuthContext';
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
