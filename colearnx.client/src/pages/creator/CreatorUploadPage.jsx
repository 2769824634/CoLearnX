import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { creatorCoursesApi, materialsApi } from '../../api';
import { useAuth } from '../../auth/AuthContext';
import MaterialList from '../../components/MaterialList';
import { userFacingError } from '../businessPresentation';

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
  const [error, setError] = useState('');
  const [toast, setToast] = useState('');

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
    setError('');
    if (!courseId) {
      setError('Choose the Course this material belongs to.');
      return;
    }
    if (!file) {
      setError('Choose a file to upload.');
      return;
    }
    setBusy(true);
    try {
      await materialsApi.upload({ title, category, description, file, courseId, token });
      setTitle('');
      setDescription('');
      setFile(null);
      e.target.reset();
      setCourseId(courseId);
      await load(courseId);
      showToast('Uploaded.');
    } catch (err) {
      setError(userFacingError(err, 'The upload could not be confirmed. Check the course library before uploading again.'));
    } finally {
      setBusy(false);
    }
  }

  return (
    <>
      <h1 className="page-title">Upload Material</h1>
      <p className="page-sub">Attach PDF, PPTX, DOCX, PNG or JPG files to a Course · max 20 MB.</p>

      <div className="card" style={{ marginBottom: 20 }}>
        <div className="card-header">New material</div>
        <div className="card-body">
          {error ? (
            <div className="callout" role="alert" style={{ marginBottom: 12 }}>
              <div className="callout-title">Material operation needs attention</div>
              {error}
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
                <select id="material-course" required value={courseId} onChange={(e) => {
                  const next = e.target.value;
                  setCourseId(next);
                  setError('');
                }}>
                  <option value="">Choose a course</option>
                  {courses.map((course) => (
                    <option key={course.id} value={course.id}>{course.code} — {course.title}</option>
                  ))}
                </select>
              </div>
              <div className="form-group">
                <label htmlFor="material-title">Title</label>
                <input
                  id="material-title"
                  value={title}
                  onChange={(e) => setTitle(e.target.value)}
                  required
                />
              </div>
              <div className="form-group">
                <label htmlFor="material-category">Category</label>
                <input
                  id="material-category"
                  value={category}
                  onChange={(e) => setCategory(e.target.value)}
                />
              </div>
              <div className="form-group">
                <label htmlFor="material-description">Description</label>
                <textarea
                  id="material-description"
                  value={description}
                  onChange={(e) => setDescription(e.target.value)}
                />
              </div>
              <div className="form-group">
                <label htmlFor="material-file">File</label>
                <input
                  id="material-file"
                  type="file"
                  accept=".pdf,.pptx,.docx,.png,.jpg,.jpeg"
                  onChange={(e) => setFile(e.target.files?.[0] ?? null)}
                  required
                />
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
          {!courseId ? <p>Choose a course to view its library.</p> : materialsLoading ? <p role="status">Loading course library…</p> : currentLibrary?.error ? <p role="alert">{currentLibrary.error}</p> : <MaterialList items={items} cloudLinks={Boolean(storage?.cloudLinks)} onError={(message) => setError(userFacingError({ message }, 'Could not download this material.'))} onCopied={showToast} />}
        </div>
      </div>
      {toast ? <div className="toast show">{toast}</div> : null}
    </>
  );
}
