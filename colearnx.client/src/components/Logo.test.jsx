import { afterEach, expect, it } from 'vitest';
import { cleanup, render, screen } from '@testing-library/react';
import Logo from './Logo';

afterEach(() => cleanup());

it('uses the standard brand mark by default', () => {
  render(<Logo />);
  const img = screen.getByRole('img', { name: /neXt/ });
  expect(img.getAttribute('src')).toMatch(/next-logo\.png/);
});

it('uses the light-ink brand mark on dark surfaces', () => {
  render(<Logo tone="dark" />);
  const img = screen.getByRole('img', { name: /neXt/ });
  expect(img.getAttribute('src')).toMatch(/next-logo-on-dark\.png/);
  expect(img.closest('.logo').className).toMatch(/logo-on-dark/);
});
