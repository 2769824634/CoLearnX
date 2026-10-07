import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { AuthContext } from '../auth/AuthContext';
import WorkspaceSwitcher from './WorkspaceSwitcher';

afterEach(() => cleanup());

it('tags each workspace option with its role for the role colour key', () => {
  render(
    <MemoryRouter>
      <AuthContext.Provider value={{
        user: { roles: ['Member', 'Trainer', 'Creator'], activeRole: 'Creator' },
        activeRole: 'creator',
        switchRole: vi.fn(),
      }}>
        <WorkspaceSwitcher />
      </AuthContext.Provider>
    </MemoryRouter>,
  );

  fireEvent.click(screen.getByRole('button', { name: /Creator/ }));

  expect(screen.getAllByRole('option').map((option) => option.dataset.role)).toEqual(['member', 'trainer', 'creator']);
});
