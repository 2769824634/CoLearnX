import { useEffect, useState } from 'react';
import { materialsApi } from '../../api';
import MaterialList from '../../components/MaterialList';
import Modal from '../../components/Modal';
import { userFacingError } from '../businessPresentation';
import { categoryChoices, startingCategory } from './courseCategories';

function titleFromFile(file) {
  return file.name.replace(/\.[^.]+$/, '').trim() || 'Course material';
}

export default function CourseMaterialsCard({ course, materials, onMaterials, token, open, onOpenChange }) {
  const [toast, setToast] = useState('');

  function showToast(message) {
    setToast(message);
    window.setTimeout(() => setToast(''), 4000);
  }

  return (
    <div className="card" style={{ marginTop: 16 }}>
      <div className="card-header">Course materials</div>
      <div className="card-body">
        <MaterialList items={materials} />
      </div>
      <CourseMaterialUploadDialog
        open={open}
        course={course}
        token={token}
        onClose={() => onOpenChange(false)}
        onLibraryChange={onMaterials}
        onUploaded={(next) => {
          onMaterials(next);
          onOpenChange(false);
          showToast('Uploaded.');
        }}
      />
      {toast ? <div className="toast show">{toast}</div> : null}
    </div>
  );
}

function CourseMaterialUploadDialog({ open, course, token, onClose, onLibraryChange, onUploaded }) {
  const [category, setCategory] = useState('Design');
  const [description, setDescription] = useState('');
  const [files, setFiles] = useState([]);
  const [fileError, setFileError] = useState('');
  const [titleErrors, setTitleErrors] = useState({});
  const [error, setError] = useState(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    if (!open) return;
    setCategory(startingCategory(course.category));
    setDescription('');
    setFiles([]);
    setFileError('');
    setTitleErrors({});
    setError(null);
  }, [open, course]);

  function requestClose() {
    if (busy) return;
    onClose();
  }

  async function onSubmit(event) {
    event.preventDefault();
    if (busy) return;
    setError(null);
    if (!files.length) {
      setFileError('Choose a file to upload.');
      return;
    }
    const missing = files.filter((item) => !item.title.trim());
    if (missing.length) {
      setTitleErrors(Object.fromEntries(missing.map((item) => [item.key, 'Title is required.'])));
      return;
    }
    setTitleErrors({});
    setFileError('');
    setBusy(true);
    const succeeded = new Set();
    try {
      for (const item of files) {
        await materialsApi.upload({
          title: item.title.trim(),
          category: categoryChoices(course.category).includes(category) ? category : 'Design',
          description: description.trim(),
          file: item.file,
          courseId: course.id,
          token,
        });
        succeeded.add(item.key);
      }
      onUploaded(await materialsApi.list(undefined, course.id, token));
    } catch (requestError) {
      try {
        onLibraryChange(await materialsApi.list(undefined, course.id, token));
      } catch {
        // Keep the upload error visible when the library cannot refresh.
      }
      setFiles((current) => current.filter((item) => !succeeded.has(item.key)));
      setError(requestError);
    } finally {
      setBusy(false);
    }
  }

  return (
    <Modal open={open} title="Upload materials" onClose={requestClose}>
      <form onSubmit={onSubmit}>
        {error ? (
          <div className="callout" role="alert" style={{ marginBottom: 12 }}>
            {userFacingError(error, 'The upload could not be confirmed. Check the course library before uploading again.')}
          </div>
        ) : null}
        <fieldset disabled={busy} style={{ border: 0, margin: 0, padding: 0, minWidth: 0 }}>
          <div className="form-group">
            <label htmlFor="course-material-category">Category</label>
            <select id="course-material-category" value={category} onChange={(event) => setCategory(event.target.value)}>
              {categoryChoices(course.category).map((item) => <option key={item} value={item}>{item}</option>)}
            </select>
          </div>
          <div className="form-group">
            <label htmlFor="course-material-description">Description</label>
            <textarea id="course-material-description" value={description} onChange={(event) => setDescription(event.target.value)} />
          </div>
          <div className="form-group">
            <label htmlFor="course-material-files">Files</label>
            <input
              id="course-material-files"
              type="file"
              multiple
              accept=".pdf,.pptx,.docx,.png,.jpg,.jpeg"
              aria-invalid={fileError ? 'true' : undefined}
              aria-describedby={fileError ? 'course-material-files-error' : undefined}
              onChange={(event) => {
                const selected = Array.from(event.target.files || []);
                event.target.value = '';
                if (!selected.length) return;
                setFiles((current) => [...current, ...selected.map((file, index) => ({
                  key: `${file.name}-${file.size}-${file.lastModified}-${index}-${current.length}`,
                  file,
                  title: titleFromFile(file),
                }))]);
                setFileError('');
              }}
            />
            {fileError ? <div id="course-material-files-error" className="trainer-field-error" role="alert">{fileError}</div> : null}
          </div>
          {files.length ? (
            <ul className="later-resource-list">
              {files.map((item) => (
                <li key={item.key}>
                  <div>
                    <strong>{item.file.name}</strong>
                    <input
                      aria-label={`Title for ${item.file.name}`}
                      value={item.title}
                      aria-invalid={titleErrors[item.key] ? 'true' : undefined}
                      onChange={(event) => {
                        const title = event.target.value;
                        setFiles((current) => current.map((entry) => entry.key === item.key ? { ...entry, title } : entry));
                        setTitleErrors((current) => {
                          if (!current[item.key]) return current;
                          const next = { ...current };
                          delete next[item.key];
                          return next;
                        });
                      }}
                    />
                    <small>{Math.ceil(item.file.size / 1024)} KB</small>
                    {titleErrors[item.key] ? <div className="trainer-field-error" role="alert">{titleErrors[item.key]}</div> : null}
                  </div>
                  <button type="button" className="btn btn-ghost btn-sm" aria-label={`Remove ${item.file.name}`} onClick={() => setFiles((current) => current.filter((entry) => entry.key !== item.key))}>Remove</button>
                </li>
              ))}
            </ul>
          ) : null}
        </fieldset>
        <div className="modal-actions">
          <button type="button" className="btn btn-ghost" onClick={requestClose}>Cancel</button>
          <button type="submit" className="btn btn-primary" disabled={busy}>{busy ? 'Uploading…' : 'Upload'}</button>
        </div>
      </form>
    </Modal>
  );
}
