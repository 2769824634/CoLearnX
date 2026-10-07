import { afterEach, expect, it } from 'vitest';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import PasswordInput from './PasswordInput';

afterEach(cleanup);

function renderField() {
  return render(
    <>
      <label htmlFor="pw">Password</label>
      <PasswordInput id="pw" value="Secret123!" onChange={() => {}} />
    </>,
  );
}

it('reveals and hides the password without losing the label', () => {
  renderField();
  const input = screen.getByLabelText('Password');
  expect(input.type).toBe('password');
  fireEvent.click(screen.getByRole('button', { name: 'Show password' }));
  expect(input.type).toBe('text');
  expect(screen.getByRole('button', { name: 'Hide password' }).getAttribute('aria-pressed')).toBe('true');
  fireEvent.click(screen.getByRole('button', { name: 'Hide password' }));
  expect(input.type).toBe('password');
});

it('warns while Caps Lock is on and clears the warning when it is off', () => {
  renderField();
  const input = screen.getByLabelText('Password');
  fireEvent.keyUp(input, { key: 'A', modifierCapsLock: true });
  expect(screen.getByText('Caps Lock is on')).toBeTruthy();
  expect(input.getAttribute('aria-describedby')).toBe('pw-caps');
  fireEvent.keyUp(input, { key: 'a', modifierCapsLock: false });
  expect(screen.queryByText('Caps Lock is on')).toBeNull();
});
