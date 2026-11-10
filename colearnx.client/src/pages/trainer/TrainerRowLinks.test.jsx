import { render, screen, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { expect, test } from 'vitest';
import { IntakeRows } from './TrainerUi';

test('each class row is one link target covering the whole row', () => {
  render(<MemoryRouter><IntakeRows
    intakes={[{ id: 7, courseId: 3, status: 'Published', startsAt: '2026-05-01T00:00:00Z', endsAt: '2026-08-01T00:00:00Z' }]}
    courses={[{ id: 3, code: 'INFT 3030', title: 'Cybersecurity Essentials' }]} /></MemoryRouter>);

  const row = screen.getByRole('article');
  expect(row.classList.contains('trainer-row-link')).toBe(true);
  const links = within(row).getAllByRole('link');
  expect(links).toHaveLength(1);
  expect(links[0].textContent).toBe('Cybersecurity Essentials');
  expect(links[0].getAttribute('href')).toBe('/trainer/courses/intakes/7');
});
