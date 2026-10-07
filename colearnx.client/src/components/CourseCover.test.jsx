import { afterEach, expect, it } from 'vitest';
import { cleanup, render } from '@testing-library/react';
import CourseCover from './CourseCover';

afterEach(cleanup);

function coverFor(code) {
  const { container } = render(<CourseCover code={code} />);
  const cover = container.querySelector('.course-cover');
  const look = [cover.dataset.pattern, cover.dataset.tone];
  cleanup();
  return { cover, look };
}

it('gives the same course the same cover every time', () => {
  expect(coverFor('INFT 2051').look).toEqual(coverFor('INFT 2051').look);
});

it('varies covers across courses using only the known patterns and tones', () => {
  const looks = ['INFT 2002', 'INFT 3030', 'INFT 2051', 'CRT-17', 'DES 1001', 'SEC 4410'].map((code) => coverFor(code).look);
  expect(new Set(looks.map((look) => look.join('/'))).size).toBeGreaterThan(2);
  looks.forEach(([pattern, tone]) => {
    expect(['grid', 'rings', 'stripes', 'dots']).toContain(pattern);
    expect(['purple', 'teal', 'blend']).toContain(tone);
  });
});

it('is decorative and shows the subject prefix above the course number', () => {
  const { container } = render(<CourseCover code="INFT 2051" />);
  const cover = container.querySelector('.course-cover');
  expect(cover.getAttribute('aria-hidden')).toBe('true');
  expect(cover.querySelector('small').textContent).toBe('INFT');
  expect(cover.textContent).toBe('INFT2051');
});
