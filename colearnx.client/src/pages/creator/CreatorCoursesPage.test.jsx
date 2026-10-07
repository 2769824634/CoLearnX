import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { AuthContext } from '../../auth/AuthContext';
import AppRouter from '../../routes/AppRouter';

const auth = {
  token: 'creator-token',
  user: {
    id: 3,
    email: 'creator@colearnx.test',
    fullName: 'Course Creator',
    activeRole: 'Creator',
    roles: ['Creator'],
  },
  booting: false,
  isAuthenticated: true,
  activeRole: 'creator',
  roles: ['Creator'],
  logout: vi.fn(),
  switchRole: vi.fn(),
};

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe('Creator Course workspace', () => {
  it('shows creator-owned courses and an intake review entry point', async () => {
    vi.stubGlobal('fetch', vi.fn(async (path) => {
      const data = path === '/api/creator/courses'
        ? [{ id: 17, code: 'CRT-17', title: 'Accessible Learning', status: 'Draft', courseLevelName: 'Beginner', learningPathName: 'Design', creditCost: 24 }]
        : [];
      return new Response(JSON.stringify(data), { status: 200, headers: { 'Content-Type': 'application/json' } });
    }));

    render(
      <MemoryRouter initialEntries={['/creator/courses']}>
        <AuthContext.Provider value={auth}>
          <AppRouter />
        </AuthContext.Provider>
      </MemoryRouter>,
    );

    expect(await screen.findByRole('heading', { name: 'Course workspace' })).toBeTruthy();
    expect(screen.getByText('Accessible Learning')).toBeTruthy();
    expect(screen.getByRole('link', { name: 'Session approvals' })).toBeTruthy();
    expect(screen.getByRole('link', { name: /Approve Trainer sessions/i })).toBeTruthy();
    expect(screen.getAllByRole('link', { name: /Create Course/i }).length).toBeGreaterThan(0);
    expect(screen.getByText('24 credits')).toBeTruthy();
  });

  it('walks a new Course through three steps and saves it as a draft', async () => {
    stubCourseApi();
    renderAt('/creator/courses/new');

    expect(await screen.findByRole('heading', { name: 'Create Course' })).toBeTruthy();
    const steps = screen.getByRole('list', { name: 'Course creation steps' });
    expect(within(steps).getAllByRole('listitem').map((item) => item.textContent)).toEqual(['1Basic information', '2Materials', '3Interests & price']);
    expect(within(steps).getByRole('button', { name: /Basic information/ }).getAttribute('aria-current')).toBe('step');
    expect(within(steps).getByRole('button', { name: /Interests & price/ }).disabled).toBe(true);
    expect(screen.queryByLabelText('Credit cost')).toBeNull();

    fireEvent.click(screen.getByRole('button', { name: 'Next' }));
    expect(screen.getByLabelText('Course code')).toBeTruthy();
    expect(screen.queryByLabelText('Files to upload when this Course is saved')).toBeNull();

    fireEvent.change(screen.getByLabelText('Course code'), { target: { value: 'CRT-44' } });
    fireEvent.change(screen.getByLabelText('Title'), { target: { value: 'Inclusive Design' } });
    fireEvent.change(screen.getByLabelText('Course level'), { target: { value: '1' } });
    fireEvent.change(screen.getByLabelText('Learning path'), { target: { value: '3' } });
    fireEvent.change(screen.getByLabelText('Category'), { target: { value: 'Design' } });
    fireEvent.click(screen.getByRole('button', { name: 'Next' }));

    const upload = screen.getByLabelText('Files to upload when this Course is saved');
    fireEvent.change(upload, { target: { files: [new File(['notes'], 'notes.pdf', { type: 'application/pdf' })] } });
    expect(screen.getByText('notes.pdf')).toBeTruthy();
    fireEvent.click(screen.getByRole('button', { name: 'Back' }));
    expect(screen.getByLabelText('Title').value).toBe('Inclusive Design');
    fireEvent.click(screen.getByRole('button', { name: 'Next' }));
    expect(screen.getByText('notes.pdf')).toBeTruthy();
    fireEvent.click(screen.getByRole('button', { name: 'Next' }));

    fireEvent.click(screen.getByLabelText('Sketching'));
    fireEvent.change(screen.getByLabelText('Credit cost'), { target: { value: '24' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save draft' }));

    expect(await screen.findByRole('heading', { name: 'Inclusive Design' })).toBeTruthy();
    const createRequest = globalThis.fetch.mock.calls.find(([path, options]) => path === '/api/creator/courses' && options?.method === 'POST');
    expect(JSON.parse(createRequest[1].body)).toMatchObject({ code: 'CRT-44', title: 'Inclusive Design', courseLevelId: 1, learningPathId: 3, category: 'Design', creditCost: 24, interestIds: [2] });
    expect(globalThis.fetch.mock.calls.filter(([path, options]) => path === '/api/materials' && options?.method === 'POST')).toHaveLength(1);
  });

  it('shows chosen interests as removable chips with per-category counts', async () => {
    stubCourseApi({ existing: course({ id: 44, interestIds: [2] }) });
    renderAt('/creator/courses/44');

    const steps = await screen.findByRole('list', { name: 'Course creation steps' });
    fireEvent.click(within(steps).getByRole('button', { name: /Interests & price/ }));
    expect(screen.getByText('1 of 2 selected')).toBeTruthy();
    fireEvent.click(screen.getByRole('button', { name: 'Remove Sketching' }));
    expect(screen.getByText(/Selected 0 of 4/)).toBeTruthy();
    expect(screen.getByLabelText('Sketching').checked).toBe(false);
    expect(screen.queryByRole('button', { name: 'Remove Sketching' })).toBeNull();

    fireEvent.change(screen.getByPlaceholderText('Search interests'), { target: { value: 'zzz' } });
    expect(screen.getByText('No interests match “zzz”.')).toBeTruthy();
  });

  it('lets an existing Draft jump between steps and blocks submitting unsaved edits', async () => {
    stubCourseApi({ existing: course({ id: 44, interestIds: [2] }) });
    renderAt('/creator/courses/44');

    expect(await screen.findByRole('heading', { name: 'Inclusive Design' })).toBeTruthy();
    const steps = screen.getByRole('list', { name: 'Course creation steps' });
    fireEvent.click(within(steps).getByRole('button', { name: /Interests & price/ }));
    expect(screen.getByText(/Selected 1 of 4/)).toBeTruthy();
    fireEvent.click(within(steps).getByRole('button', { name: /Basic information/ }));
    fireEvent.change(screen.getByLabelText('Title'), { target: { value: 'Unsaved title' } });
    fireEvent.click(within(steps).getByRole('button', { name: /Interests & price/ }));
    fireEvent.click(screen.getByRole('button', { name: 'Submit for approval' }));

    expect(await screen.findByText('Save your changes before submitting for approval.')).toBeTruthy();
    expect(globalThis.fetch.mock.calls.some(([path]) => path === '/api/creator/courses/44/submit')).toBe(false);
    fireEvent.click(within(steps).getByRole('button', { name: /Basic information/ }));
    expect(screen.getByLabelText('Title').value).toBe('Unsaved title');
  });

  it('submits a saved Draft for approval from the last step and locks editing', async () => {
    stubCourseApi({ existing: course({ id: 44, interestIds: [2] }) });
    renderAt('/creator/courses/44');

    const steps = await screen.findByRole('list', { name: 'Course creation steps' });
    fireEvent.click(within(steps).getByRole('button', { name: /Interests & price/ }));
    fireEvent.click(screen.getByRole('button', { name: 'Submit for approval' }));

    expect(await screen.findByText('PendingApproval')).toBeTruthy();
    expect(screen.queryByRole('button', { name: 'Save changes' })).toBeNull();
    expect(screen.queryByRole('list', { name: 'Course creation steps' })).toBeNull();
  });
});

function stubCourseApi({ existing } = {}) {
  vi.stubGlobal('fetch', vi.fn(async (path, options = {}) => {
    if (path === '/api/interests') {
      return json([{ id: 1, slug: 'design', name: 'Design', children: [
        { id: 2, slug: 'sketching', name: 'Sketching', children: [] },
        { id: 3, slug: 'typography', name: 'Typography', children: [] },
      ] }]);
    }
    if (path === '/api/creator/courses/options') {
      return json({ courseLevels: [{ id: 1, name: 'Beginner' }], learningPaths: [{ id: 3, name: 'Design' }] });
    }
    if (path === '/api/creator/courses' && options.method === 'POST') {
      return json(course({ id: 44, ...JSON.parse(options.body) }));
    }
    if (path === '/api/creator/courses/44/submit') {
      return json(course({ id: 44, status: 'PendingApproval' }));
    }
    if (String(path).startsWith('/api/materials')) {
      return json(options.method === 'POST' ? { id: 90 } : []);
    }
    return json(existing || course({ id: 44 }));
  }));
}

function renderAt(path) {
  render(
    <MemoryRouter initialEntries={[path]}>
      <AuthContext.Provider value={auth}>
        <AppRouter />
      </AuthContext.Provider>
    </MemoryRouter>,
  );
}

function json(data) {
  return new Response(JSON.stringify(data), { status: 200, headers: { 'Content-Type': 'application/json' } });
}

function course(overrides = {}) {
  return {
    id: 44,
    code: 'CRT-44',
    title: 'Inclusive Design',
    description: null,
    creatorId: 3,
    creatorEmail: 'creator@colearnx.test',
    creatorName: 'Course Creator',
    courseLevelId: 1,
    courseLevelName: 'Beginner',
    learningPathId: 3,
    learningPathName: 'Design',
    category: 'Design',
    creditCost: 24,
    status: 'Draft',
    learningOutcomes: [],
    createdAt: '2026-09-09T00:00:00Z',
    reviewReason: null,
    ...overrides,
  };
}
