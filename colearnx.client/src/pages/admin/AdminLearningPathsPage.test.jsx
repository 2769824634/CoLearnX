import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import AdminAuthContext from '../../auth/AdminAuthContext';
import AdminLearningPathsPage from './AdminLearningPathsPage';

afterEach(() => { cleanup(); vi.unstubAllGlobals(); });

it('lets an administrator save a stage requirement and reload the saved value', async () => {
  let required = 1;
  vi.stubGlobal('fetch', vi.fn(async (url, options = {}) => {
    if (options.method === 'PUT') required = JSON.parse(options.body).requiredCourseCount;
    const paths = [{ id: 4, subjectId: 1, name: 'Software Development', stages: [
      { number: 1, name: 'Foundation', requiredCourseCount: required, completedCourseCount: 0, completed: false, courses: [] },
    ] }];
    return new Response(JSON.stringify(options.method === 'PUT' ? paths[0] : paths), { headers: { 'Content-Type': 'application/json' } });
  }));
  render(<AdminAuthContext.Provider value={{ token: 'admin-learning-path-test' }}><AdminLearningPathsPage /></AdminAuthContext.Provider>);
  const input = await screen.findByLabelText('Foundation courses required');
  fireEvent.change(input, { target: { value: '2' } });
  fireEvent.click(screen.getByRole('button', { name: 'Save' }));
  await screen.findByText('Software Development: Foundation requirement saved.');
  await waitFor(() => expect(screen.getByLabelText('Foundation courses required').value).toBe('2'));
  expect(globalThis.fetch.mock.calls.some(([url, options]) => url === '/api/admin/learning-paths/4/stages/1' && options.method === 'PUT')).toBe(true);
});
