import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { creatorCoursesApi, materialsApi } from '../../api';
import { useAuth } from '../../auth/AuthContext';
import MaterialList from '../../components/MaterialList';
import { userFacingError } from '../businessPresentation';

function fieldMessages(fieldErrors, field) {
  const entry = Object.entries(fieldErrors || {})
    .find(([key]) => key.toLowerCase() === field.toLowerCase());
  if (!entry) return [];
  return Array.isArray(entry[1]) ? entry[1].filter(Boolean) : [entry[1]].filter(Boolean);
}

function clearServerFieldError(error, field) {
  if (!error || Number(error.status) >= 500 || [401, 403].includes(Number(error.status))) return error;
  const entries = Object.entries(error.fieldErrors || {});
  if (!entries.length) return error;
  const remaining = Object.fromEntries(entries.filter(([key]) => key.toLowerCase() !== field.toLowerCase()));
  if (Object.keys(remaining).length === entries.length) return error;
  if (!Object.keys(remaining).length) return null;
  const messages = Object.values(remaining)
    .flatMap((value) => Array.isArray(value) ? value : [value])
    .filter(Boolean);
  return {
    ...error,
    message: messages.length
      ? `Please correct the remaining fields: ${messages.join(' ')}`
      : 'Please correct the remaining fields.',
    fieldErrors: remaining,
  };
}

function FieldErrors({ field, errors }) {
  if (!errors.length) return null;
  const id = `material-${field}-error`;
  return (
    <div id={id} className="trainer-field-error" role="alert">
      {errors.map((message, index) => <span key={`${field}-${index}`}>{message}</span>)}
    </div>
  );
}

// Creator upload + library list. Keep: CreatorUploadPage
export default function CreatorUploadPage() {
  const { token } = useAuth();
  const [title, setTitle] = useState('');
  const [category, setCategory] = useState('Design');
  const [description, setDescription] = useState('');
  const [courseId, setCourseId] = useState('');
  const [file, setFile] = useState(null);
  const [courses, setCourses] = useState([]);
  const [coursesLoading, setCoursesLoading] = useState(true);
  const [coursesError, setCoursesError] = useState('');
  const [librarySnapshot, setLibrarySnapshot] = useState(null);
  const currentLibrary = librarySnapshot?.token === token && librarySnapshot?.courseId === courseId ? librarySnapshot : null;
  const items = currentLibrary?.items || [];
  const materialsLoading = Boolean(courseId) && !currentLibrary;
  const [storage, setStorage] = useState(null);
  const [busy, setBusy] = useState(false);
  const [errorDetails, setErrorDetails] = useState(null);
  const [fieldErrors, setFieldErrors] = useState({});
  const [toast, setToast] = useState('');

  function clearFieldError(field) {
    const nextError = clearServerFieldError(errorDetails, field);
    if (!errorDetails || nextError !== errorDetails) {
      setFieldErrors((current) => Object.fromEntries(
        Object.entries(current).filter(([key]) => key.toLowerCase() !== field.toLowerCase()),
      ));
    }
    setErrorDetails((current) => clearServerFieldError(current, field));
  }

  function showToast(message) {
    setToast(message);
    window.setTimeout(() => setToast(''), 4000);
  }

  async function load(selectedCourseId = courseId) {
    const list = await materialsApi.list(undefined, selectedCourseId || undefined, token);
    setLibrarySnapshot({ token, courseId: selectedCourseId, items: list });
  }

  useEffect(() => {
    let cancelled = false;
    creatorCoursesApi.list(token)
      .then((list) => { if (!cancelled) setCourses(list); })
      .catch((err) => { if (!cancelled) setCoursesError(userFacingError(err, 'Could not load your courses. Reload this page to retry.')); })
      .finally(() => { if (!cancelled) setCoursesLoading(false); });
    materialsApi.storage(token).then(setStorage).catch(() => setStorage(null));
    return () => { cancelled = true; };
  }, [token]);

  useEffect(() => {
    if (!courseId) return undefined;
    let cancelled = false;
    materialsApi.list(undefined, courseId, token)
      .then((list) => { if (!cancelled) setLibrarySnapshot({ token, courseId, items: list }); })
      .catch((err) => { if (!cancelled) setLibrarySnapshot({ token, courseId, error: userFacingError(err, 'Could not load the course library.') }); });
    return () => { cancelled = true; };
  }, [token, courseId]);

  async function onSubmit(e) {
    e.preventDefault();
    if (busy) return;
    setErrorDetails(null);
    setFieldErrors({});
    if (!courseId) {
      setFieldErrors({ courseId: ['Choose the Course this material belongs to.'] });
      return;
    }
    if (!title.trim()) {
      setFieldErrors({ title: ['Title is required.'] });
      return;
    }
    if (!file) {
      setFieldErrors({ file: ['Choose a file to upload.'] });
      return;
    }
    setBusy(true);
    try {
      await materialsApi.upload({ title, category, description, file, courseId, token });
      setTitle('');
      setDescription('');
      setFile(null);
      setFieldErrors({});
      e.target.reset();
      setCourseId(courseId);
      await load(courseId);
      showToast('Uploaded.');
    } catch (err) {
      setFieldErrors(err?.fieldErrors || {});
      setErrorDetails(err);
    } finally {
      setBusy(false);
    }
  }

  const courseErrors = fieldMessages(fieldErrors, 'courseId');
  const titleErrors = fieldMessages(fieldErrors, 'title');
  const categoryErrors = fieldMessages(fieldErrors, 'category');
  const descriptionErrors = fieldMessages(fieldErrors, 'description');
  const fileErrors = fieldMessages(fieldErrors, 'file');

  return (
    <>
      <h1 className="page-title">Upload Material</h1>
      <p className="page-sub">Attach PDF, PPTX, DOCX, PNG or JPG files to a Course · max 20 MB.</p>

      <div className="card" style={{ marginBottom: 20 }}>
        <div className="card-header">New material</div>
        <div className="card-body">
          {errorDetails ? (
            <div className="callout" role="alert" style={{ marginBottom: 12 }}>
              <div className="callout-title">Material operation needs attention</div>
              {userFacingError(errorDetails, 'The upload could not be confirmed. Check the course library before uploading again.')}
            </div>
          ) : null}
          {coursesLoading ? <p role="status">Loading your courses…</p> : coursesError ? <p role="alert">{coursesError}</p> : !courses.length ? (
            <p className="page-sub" style={{ margin: 0 }}>
              Create a Course first, then upload materials onto it. <Link to="/creator/courses/new">Create Course</Link>
            </p>
          ) : (
            <form onSubmit={onSubmit}><fieldset disabled={busy} style={{ border: 0, padding: 0, minWidth: 0 }}>
                <div className="form-group">
                  <label htmlFor="material-course">Course</label>
                  <select
                    id="material-course"
                    required
                    value={courseId}
                    aria-invalid={courseErrors.length > 0}
                    aria-describedby={courseErrors.length ? 'material-course-error' : undefined}
                    onChange={(e) => {
                      const next = e.target.value;
                      setCourseId(next);
                      clearFieldError('courseId');
                    }}
                  >
                    <option value="">Choose a course</option>
                    {courses.map((course) => (
                      <option key={course.id} value={course.id}>{course.code} — {course.title}</option>
                    ))}
                  </select>
                  <FieldErrors field="course" errors={courseErrors} />
                </div>
                <div className="form-group">
                  <label htmlFor="material-title">Title</label>
                  <input
                    id="material-title"
                    value={title}
                    aria-invalid={titleErrors.length > 0}
                    aria-describedby={titleErrors.length ? 'material-title-error' : undefined}
                    onChange={(e) => {
                      setTitle(e.target.value);
                      clearFieldError('title');
                    }}
                    required
                  />
                  <FieldErrors field="title" errors={titleErrors} />
                </div>
                <div className="form-group">
                  <label htmlFor="material-category">Category</label>
                  <input
                    id="material-category"
                    value={category}
                    aria-invalid={categoryErrors.length > 0}
                    aria-describedby={categoryErrors.length ? 'material-category-error' : undefined}
                    onChange={(e) => {
                      setCategory(e.target.value);
                      clearFieldError('category');
                    }}
                  />
                  <FieldErrors field="category" errors={categoryErrors} />
                </div>
                <div className="form-group">
                  <label htmlFor="material-description">Description</label>
                  <textarea
                    id="material-description"
                    value={description}
                    aria-invalid={descriptionErrors.length > 0}
                    aria-describedby={descriptionErrors.length ? 'material-description-error' : undefined}
                    onChange={(e) => {
                      setDescription(e.target.value);
                      clearFieldError('description');
                    }}
                  />
                  <FieldErrors field="description" errors={descriptionErrors} />
                </div>
                <div className="form-group">
                  <label htmlFor="material-file">File</label>
                  <input
                    id="material-file"
                    type="file"
                    accept=".pdf,.pptx,.docx,.png,.jpg,.jpeg"
                    aria-invalid={fileErrors.length > 0}
                    aria-describedby={fileErrors.length ? 'material-file-error' : undefined}
                    onChange={(e) => {
                      setFile(e.target.files?.[0] ?? null);
                      clearFieldError('file');
                    }}
                    required
                  />
                  <FieldErrors field="file" errors={fileErrors} />
                </div>
              <button type="submit" className="btn btn-primary" disabled={busy}>
                {busy ? 'Uploading…' : 'Upload'}
              </button>
            </fieldset></form>
          )}
        </div>
      </div>

      <div className="card">
        <div className="card-header">{courseId ? 'Course library' : 'Library'}</div>
        <div className="card-body" style={{ padding: items.length ? 0 : 16 }}>
          {!courseId ? <p>Choose a course to view its library.</p> : materialsLoading ? <p role="status">Loading course library…</p> : currentLibrary?.error ? <p role="alert">{currentLibrary.error}</p> : <MaterialList items={items} cloudLinks={Boolean(storage?.cloudLinks)} onError={(message) => setErrorDetails({ message })} onCopied={showToast} />}
        </div>
      </div>
      {toast ? <div className="toast show">{toast}</div> : null}
    </>
  );
}
