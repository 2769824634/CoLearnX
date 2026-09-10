import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { ApiError } from '../../api/client';
import { creatorCoursesApi, materialsApi } from '../../api';
import { useAuth } from '../../auth/AuthContext';
import MaterialList from '../../components/MaterialList';

// Creator upload + library list. Keep: CreatorUploadPage
export default function CreatorUploadPage() {
  const { token } = useAuth();
  const [title, setTitle] = useState('');
  const [category, setCategory] = useState('Design');
  const [description, setDescription] = useState('');
  const [courseId, setCourseId] = useState('');
  const [file, setFile] = useState(null);
  const [courses, setCourses] = useState([]);
  const [items, setItems] = useState([]);
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
    setItems(list);
  }

  useEffect(() => {
    creatorCoursesApi.list(token)
      .then(setCourses)
      .catch((err) => setError(err.message || 'Failed to load courses'));
    materialsApi.storage(token).then(setStorage).catch(() => setStorage(null));
  }, [token]);

  useEffect(() => {
    if (!courseId) return undefined;
    let cancelled = false;
    materialsApi.list(undefined, courseId, token)
      .then((list) => { if (!cancelled) setItems(list); })
      .catch((err) => { if (!cancelled) setError(err.message || 'Failed to load materials'); });
    return () => { cancelled = true; };
  }, [token, courseId]);

  async function onSubmit(e) {
    e.preventDefault();
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
      setError(err instanceof ApiError ? err.message : 'Upload failed.');
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
            <div className="callout" style={{ marginBottom: 12 }}>
              <div className="callout-title">Could not upload</div>
              {error}
            </div>
          ) : null}
          {!courses.length ? (
            <p className="page-sub" style={{ margin: 0 }}>
              Create a Course first, then upload materials onto it. <Link to="/creator/courses/new">Create Course</Link>
            </p>
          ) : (
            <form onSubmit={onSubmit}>
              <div className="form-group">
                <label htmlFor="material-course">Course</label>
                <select id="material-course" required value={courseId} onChange={(e) => {
                  const next = e.target.value;
                  setCourseId(next);
                  if (!next) setItems([]);
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
            </form>
          )}
        </div>
      </div>

      <div className="card">
        <div className="card-header">{courseId ? 'Course library' : 'Library'}</div>
        <div className="card-body" style={{ padding: items.length ? 0 : 16 }}>
          <MaterialList items={items} cloudLinks={Boolean(storage?.cloudLinks)} onError={setError} onCopied={showToast} />
        </div>
      </div>
      {toast ? <div className="toast show">{toast}</div> : null}
    </>
  );
}
