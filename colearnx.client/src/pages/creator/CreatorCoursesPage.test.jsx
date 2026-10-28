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
    expect(screen.queryByRole('link', { name: 'Upload Material' })).toBeNull();
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
    fireEvent.change(screen.getByLabelText('Subject'), { target: { value: '1' } });
    fireEvent.change(screen.getByLabelText('Learning path'), { target: { value: '3' } });
    fireEvent.change(screen.getByLabelText('Duration (hours)'), { target: { value: '2' } });
    const category = screen.getByRole('combobox', { name: 'Category (optional)' });
    expect(category.required).toBe(false);
    expect(within(category).getAllByRole('option').map((option) => option.textContent)).toEqual(['No category', 'Programming', 'Design', 'Business']);
    fireEvent.change(category, { target: { value: 'Design' } });
    fireEvent.click(screen.getByRole('button', { name: 'Next' }));

    const upload = screen.getByLabelText('Files to upload when this Course is saved');
    fireEvent.change(upload, { target: { files: [new File(['notes'], 'notes.pdf', { type: 'application/pdf' })] } });
    expect(screen.getByText('notes.pdf')).toBeTruthy();
    fireEvent.click(screen.getByRole('button', { name: 'Back' }));
    expect(screen.getByLabelText('Title').value).toBe('Inclusive Design');
    fireEvent.click(screen.getByRole('button', { name: 'Next' }));
    expect(screen.getByText('notes.pdf')).toBeTruthy();
    fireEvent.click(screen.getByRole('button', { name: 'Next' }));

    fireEvent.click(screen.getByRole('button', { name: /Development/ }));
    fireEvent.click(screen.getByLabelText('Web Development'));
    fireEvent.change(screen.getByLabelText('Credit cost'), { target: { value: '40' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save draft' }));

    expect(await screen.findByRole('heading', { name: 'Inclusive Design' })).toBeTruthy();
    const createRequest = globalThis.fetch.mock.calls.find(([path, options]) => path === '/api/creator/courses' && options?.method === 'POST');
    const payload = JSON.parse(createRequest[1].body);
    expect(payload).toMatchObject({ code: 'CRT-44', title: 'Inclusive Design', courseLevelId: 1, courseSubjectId: 1, learningPathId: 3, durationHours: 2, category: 'Design', creditCost: 40, interestIds: [5] });
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
    expect(screen.getByRole('button', { name: 'Upload materials' })).toBeTruthy();
  });

  it.each(['Draft', 'Rejected', 'PendingApproval', 'Published'])('lists current files and offers upload on a %s course', async (status) => {
    stubCourseApi({
      existing: course({ id: 44, status, category: status === 'Rejected' ? '' : 'Design' }),
      materials: [material({ title: 'Syllabus' })],
    });
    renderAt('/creator/courses/44');

    expect(await screen.findByText('Syllabus')).toBeTruthy();
    expect(screen.getByText('Course materials')).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Upload materials' })).toBeTruthy();
    if (status === 'Draft' || status === 'Rejected') {
      const steps = screen.getByRole('list', { name: 'Course creation steps' });
      expect(within(steps).getAllByRole('listitem').map((item) => item.textContent)).toEqual(['1Basic information', '2Interests & price']);
    }
    if (status === 'Rejected') {
      fireEvent.click(screen.getByRole('button', { name: 'Upload materials' }));
      expect(within(screen.getByRole('dialog', { name: 'Upload materials' })).getByLabelText('Category').value).toBe('Design');
    }
  });

  it('uploads every selected file to the open course and then shows them in the library', async () => {
    stubCourseApi({ existing: course({ id: 44, category: 'Design' }) });
    renderAt('/creator/courses/44');

    fireEvent.click(await screen.findByRole('button', { name: 'Upload materials' }));
    const dialog = screen.getByRole('dialog', { name: 'Upload materials' });
    const category = within(dialog).getByRole('combobox', { name: 'Category' });
    expect(category.value).toBe('Design');
    expect(within(category).getAllByRole('option').map((option) => option.textContent)).toEqual(['Programming', 'Design', 'Business']);
    fireEvent.change(category, { target: { value: 'Programming' } });
    fireEvent.change(within(dialog).getByLabelText('Description'), { target: { value: 'Batch notes' } });
    fireEvent.change(within(dialog).getByLabelText('Files'), {
      target: { files: [new File(['notes'], 'notes.pdf', { type: 'application/pdf' }), new File(['slides'], 'slides.pptx', { type: 'application/vnd.ms-powerpoint' })] },
    });
    fireEvent.change(within(dialog).getByLabelText('Title for notes.pdf'), { target: { value: 'Workshop notes' } });
    fireEvent.click(within(dialog).getByRole('button', { name: 'Upload' }));

    expect(await screen.findByText('Uploaded.')).toBeTruthy();
    expect(screen.queryByRole('dialog', { name: 'Upload materials' })).toBeNull();
    expect(screen.getByText('Workshop notes')).toBeTruthy();
    expect(screen.getByText('slides')).toBeTruthy();
    const uploads = globalThis.fetch.mock.calls.filter(([path, options]) => path === '/api/materials' && options?.method === 'POST');
    expect(uploads.map(([, options]) => ({
      title: options.body.get('title'),
      category: options.body.get('category'),
      description: options.body.get('description'),
      courseId: options.body.get('courseId'),
      fileName: options.body.get('file').name,
    }))).toEqual([
      { title: 'Workshop notes', category: 'Programming', description: 'Batch notes', courseId: '44', fileName: 'notes.pdf' },
      { title: 'slides', category: 'Programming', description: 'Batch notes', courseId: '44', fileName: 'slides.pptx' },
    ]);
  });

  it('keeps the failed file in the dialog and shows the files that already uploaded', async () => {
    stubCourseApi({ existing: course({ id: 44 }), failUploadAt: 2 });
    renderAt('/creator/courses/44');
    fireEvent.click(await screen.findByRole('button', { name: 'Upload materials' }));
    const dialog = screen.getByRole('dialog', { name: 'Upload materials' });
    fireEvent.change(within(dialog).getByLabelText('Description'), { target: { value: '   ' } });
    fireEvent.change(within(dialog).getByLabelText('Files'), {
      target: { files: [new File(['notes'], 'notes.pdf', { type: 'application/pdf' }), new File(['slides'], 'slides.pptx', { type: 'application/vnd.ms-powerpoint' })] },
    });
    fireEvent.click(within(dialog).getByRole('button', { name: 'Upload' }));

    expect(await screen.findByText(/File must be 20 MB or smaller. Reference: trace-upload-1/)).toBeTruthy();
    expect(screen.getByRole('dialog', { name: 'Upload materials' })).toBeTruthy();
    expect(screen.getByText('notes')).toBeTruthy();
    expect(within(screen.getByRole('dialog')).queryByLabelText('Title for notes.pdf')).toBeNull();
    expect(within(screen.getByRole('dialog')).getByLabelText('Title for slides.pptx')).toBeTruthy();
    const failedUpload = globalThis.fetch.mock.calls.filter(([path, options]) => path === '/api/materials' && options?.method === 'POST')[1];
    expect(failedUpload[1].body.get('description')).toBeNull();
  });

  it('requires a file and a title before uploading', async () => {
    stubCourseApi({ existing: course({ id: 44 }) });
    renderAt('/creator/courses/44');
    fireEvent.click(await screen.findByRole('button', { name: 'Upload materials' }));
    const dialog = screen.getByRole('dialog', { name: 'Upload materials' });
    fireEvent.click(within(dialog).getByRole('button', { name: 'Upload' }));
    expect(screen.getByText('Choose a file to upload.')).toBeTruthy();

    fireEvent.change(within(dialog).getByLabelText('Files'), {
      target: { files: [new File(['.'], '.pdf', { type: 'application/pdf' })] },
    });
    expect(within(dialog).getByLabelText('Title for .pdf').value).toBe('Course material');
    fireEvent.change(within(dialog).getByLabelText('Title for .pdf'), { target: { value: '   ' } });
    fireEvent.click(within(dialog).getByRole('button', { name: 'Upload' }));
    expect(screen.getByText('Title is required.')).toBeTruthy();
    expect(globalThis.fetch.mock.calls.some(([path, options]) => path === '/api/materials' && options?.method === 'POST')).toBe(false);
  });

  it('holds the dialog open while an upload is in progress', async () => {
    let release;
    stubCourseApi({
      existing: course({ id: 44 }),
      holdUpload: new Promise((resolve) => { release = resolve; }),
    });
    renderAt('/creator/courses/44');
    fireEvent.click(await screen.findByRole('button', { name: 'Upload materials' }));
    const dialog = screen.getByRole('dialog', { name: 'Upload materials' });
    fireEvent.change(within(dialog).getByLabelText('Files'), {
      target: { files: [new File(['notes'], 'notes.pdf', { type: 'application/pdf' })] },
    });
    fireEvent.click(within(dialog).getByRole('button', { name: 'Upload' }));

    expect(await screen.findByRole('button', { name: 'Uploading…' })).toBeTruthy();
    fireEvent.keyDown(document, { key: 'Escape' });
    fireEvent.click(screen.getByRole('button', { name: 'Cancel' }));
    expect(screen.getByRole('dialog', { name: 'Upload materials' })).toBeTruthy();
    release();
    expect(await screen.findByText('Uploaded.')).toBeTruthy();
  });

  it('shows a support reference when an upload cannot be confirmed', async () => {
    stubCourseApi({
      existing: course({ id: 44 }),
      failUploadAt: 1,
      uploadFailure: { status: 500, code: 'UPLOAD_FAILED', message: 'SqlServerRetryingExecutionStrategy failed', traceId: 'trace-upload-1' },
    });
    renderAt('/creator/courses/44');
    fireEvent.click(await screen.findByRole('button', { name: 'Upload materials' }));
    const dialog = screen.getByRole('dialog', { name: 'Upload materials' });
    fireEvent.change(within(dialog).getByLabelText('Files'), {
      target: { files: [new File(['guide'], 'guide.pdf', { type: 'application/pdf' })] },
    });
    fireEvent.click(within(dialog).getByRole('button', { name: 'Upload' }));

    expect(await screen.findByText('The upload could not be confirmed. Check the course library before uploading again. Reference: trace-upload-1')).toBeTruthy();
    expect(screen.queryByText(/SqlServerRetrying/)).toBeNull();
  });

  it('redirects the old upload page to the course list', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => json([])));
    renderAt('/creator/upload');
    expect(await screen.findByRole('heading', { name: 'Course workspace' })).toBeTruthy();
  });
});

function stubCourseApi({ existing, materials = [], failUploadAt = 0, holdUpload, uploadFailure } = {}) {
  const library = [...materials];
  let uploads = 0;
  vi.stubGlobal('fetch', vi.fn(async (path, options = {}) => {
    if (path === '/api/interests') {
      return json([{ id: 1, slug: 'design', name: 'Design', children: [
        { id: 2, slug: 'sketching', name: 'Sketching', children: [] },
        { id: 3, slug: 'typography', name: 'Typography', children: [] },
      ] }, { id: 4, slug: 'development', name: 'Development', children: [
        { id: 5, slug: 'web-development', name: 'Web Development', children: [] },
      ] }]);
    }
    if (path === '/api/creator/courses/options') {
      return json({
        courseLevels: [{ id: 1, name: 'Beginner' }],
        learningPaths: [{ id: 3, name: 'Design' }],
        courseSubjects: [{ id: 1, name: 'Software Development', band: 'DigitalTechnology' }],
        priceCells: [{ band: 'DigitalTechnology', courseLevelId: 1, minCredits: 40, maxCredits: 180 }],
      });
    }
    if (path === '/api/creator/courses' && options.method === 'POST') {
      return json(course({ id: 44, ...JSON.parse(options.body) }));
    }
    if (path === '/api/creator/courses/44/submit') {
      return json(course({ id: 44, status: 'PendingApproval' }));
    }
    if (path === '/api/materials' && options.method === 'POST') {
      uploads += 1;
      if (holdUpload && uploads === 1) await holdUpload;
    if (failUploadAt && uploads === failUploadAt) {
      return new Response(JSON.stringify(uploadFailure || { message: 'File must be 20 MB or smaller.', traceId: 'trace-upload-1' }), {
        status: uploadFailure?.status || 400,
        headers: { 'Content-Type': 'application/json' },
      });
    }
      const item = material({ id: 90 + uploads, title: options.body.get('title') });
      library.push(item);
      return json(item);
    }
    if (String(path).startsWith('/api/materials')) return json(library);
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

function material(overrides = {}) {
  return {
    id: 7,
    title: 'Syllabus',
    format: 'PDF',
    status: 'Approved',
    creatorName: 'Course Creator',
    courseCode: 'CRT-44',
    courseTitle: 'Inclusive Design',
    ...overrides,
  };
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
    creditCost: 40,
    courseSubjectId: 1,
    durationHours: 2,
    status: 'Draft',
    learningOutcomes: [],
    createdAt: '2026-09-09T00:00:00Z',
    reviewReason: null,
    ...overrides,
  };
}
