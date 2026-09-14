import { useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { creatorCoursesApi, materialsApi } from '../../api';
import MaterialList from '../../components/MaterialList';
import useTrainerQuery from '../trainer/useTrainerQuery';
import { CreatorError, CreatorHeader } from './CreatorUi';

async function loadCourseForm(token, courseId, signal) {
  const options = await creatorCoursesApi.options(token, signal);
  if (courseId === 'new') return { course: null, options, materials: [] };
  const [course, materials] = await Promise.all([
    creatorCoursesApi.get(token, courseId, signal),
    materialsApi.list(undefined, courseId, token, signal),
  ]);
  return { course, options, materials };
}

const emptyCourse = {
  code: '', title: '', description: '', courseLevelId: '', learningPathId: '',
  category: '', creditCost: '', learningOutcomes: [], status: 'Draft',
};

function titleFromFile(file) {
  return file.name.replace(/\.[^.]+$/, '').trim() || 'Course material';
}

export default function CreatorCourseFormPage() {
  const { courseId } = useParams();
  const key = courseId || 'new';
  const query = useTrainerQuery(loadCourseForm, key);

  if (query.loading) return <section className="creator-page"><div className="creator-empty" role="status">Loading Course…</div></section>;
  if (query.error) return <section className="creator-page"><CreatorError error={query.error} onRetry={query.refresh} /></section>;
  return <CourseEditor key={`${key}:${query.data.course?.status || 'new'}`} initialCourse={query.data.course} initialMaterials={query.data.materials} options={query.data.options} token={query.token} />;
}

function CourseEditor({ initialCourse, initialMaterials, options, token }) {
  const navigate = useNavigate();
  const [course, setCourse] = useState(initialCourse);
  const [materials, setMaterials] = useState(initialMaterials || []);
  const [pendingFiles, setPendingFiles] = useState([]);
  const [form, setForm] = useState(() => toForm(initialCourse || emptyCourse));
  const [error, setError] = useState(null);
  const [busy, setBusy] = useState(false);
  const editable = !course || course.status === 'Draft' || course.status === 'Rejected';

  async function uploadFiles(courseId, files) {
    for (const file of files) {
      await materialsApi.upload({
        title: titleFromFile(file),
        category: form.category || 'General',
        file,
        courseId,
        token,
      });
    }
    setMaterials(await materialsApi.list(undefined, courseId, token));
    setPendingFiles([]);
  }

  async function save(event) {
    event.preventDefault();
    setBusy(true);
    setError(null);
    try {
      const payload = toPayload(form);
      if (course) {
        const updated = await creatorCoursesApi.update(token, course.id, payload);
        setCourse(updated);
        setForm(toForm(updated));
        if (pendingFiles.length) await uploadFiles(updated.id, pendingFiles);
      } else {
        const created = await creatorCoursesApi.create(token, payload);
        try {
          if (pendingFiles.length) await uploadFiles(created.id, pendingFiles);
        } finally {
          navigate(`/creator/courses/${created.id}`);
        }
        return;
      }
    } catch (requestError) {
      setError(requestError);
    } finally {
      setBusy(false);
    }
  }

  async function submitForApproval() {
    setBusy(true);
    setError(null);
    try {
      if (pendingFiles.length) await uploadFiles(course.id, pendingFiles);
      setCourse(await creatorCoursesApi.submit(token, course.id));
    } catch (requestError) {
      setError(requestError);
    } finally {
      setBusy(false);
    }
  }

  async function saveAndSubmit(event) {
    const formElement = event.currentTarget.form;
    if (formElement && !formElement.reportValidity()) return;
    setBusy(true);
    setError(null);
    try {
      const payload = toPayload(form);
      const created = await creatorCoursesApi.create(token, payload);
      try {
        if (pendingFiles.length) await uploadFiles(created.id, pendingFiles);
        await creatorCoursesApi.submit(token, created.id);
      } finally {
        navigate(`/creator/courses/${created.id}`);
      }
    } catch (requestError) {
      setError(requestError);
    } finally {
      setBusy(false);
    }
  }

  const title = course ? course.title : 'Create Course';
  return <section className="creator-page">
    <Link className="creator-back" to="/creator/courses">← Courses</Link>
    <CreatorHeader eyebrow={course ? course.code : 'New Course'} title={title} action={course ? <span className={`creator-status ${course.status.toLowerCase()}`}>{course.status}</span> : null}>
      {course
        ? 'Review the Course definition and submit it for Admin approval when it is ready.'
        : 'Save a draft, or submit it immediately so Admin can publish it to the catalogue.'}
    </CreatorHeader>
    <CreatorError error={error} />
    {course?.reviewReason ? <div className="creator-error"><strong>Admin feedback</strong><p>{course.reviewReason}</p></div> : null}
    {editable ? <form className="card" onSubmit={save}>
      <CourseFields form={form} options={options} setForm={setForm} />
      <div className="trainer-actions">
        <button className="btn btn-primary" type="submit" disabled={busy}>{busy ? 'Saving…' : course ? 'Save changes' : 'Save draft'}</button>
        {course
          ? <button className="btn btn-teal" type="button" disabled={busy} onClick={submitForApproval}>Submit for approval</button>
          : <button className="btn btn-teal" type="button" disabled={busy} onClick={saveAndSubmit}>Save and submit for approval</button>}
      </div>
    </form> : <div className="creator-empty"><strong>Course editing is locked</strong><p>This Course can be edited again only if Admin rejects it.</p></div>}
    <CourseMaterialsCard
      course={course}
      materials={materials}
      pendingFiles={pendingFiles}
      setPendingFiles={setPendingFiles}
      editable={editable}
      onUploadExisting={async (files) => {
        setBusy(true);
        setError(null);
        try {
          await uploadFiles(course.id, files);
        } catch (requestError) {
          setError(requestError);
        } finally {
          setBusy(false);
        }
      }}
      busy={busy}
    />
  </section>;
}

function CourseMaterialsCard({ course, materials, pendingFiles, setPendingFiles, onUploadExisting, busy, editable }) {
  return <div className="card" style={{ marginTop: 16 }}>
    <div className="card-header">Course materials</div>
    <div className="card-body">
      <p className="page-sub" style={{ marginTop: 0 }}>PDF, PPTX, DOCX, PNG or JPG · max 20 MB. Files stay with this Course.</p>
      {course ? <MaterialList items={materials} /> : null}
      {editable || !course ? <div className="form-group">
        <label htmlFor="creator-course-materials">{course ? 'Add files' : 'Files to upload when this Course is saved'}</label>
        <input
          id="creator-course-materials"
          type="file"
          multiple
          accept=".pdf,.pptx,.docx,.png,.jpg,.jpeg"
          disabled={busy}
          onChange={(event) => {
            const files = Array.from(event.target.files || []);
            event.target.value = '';
            if (!files.length) return;
            if (course) onUploadExisting(files);
            else setPendingFiles((current) => [...current, ...files]);
          }}
        />
      </div> : null}
      {!course && pendingFiles.length ? (
        <ul className="later-resource-list">
          {pendingFiles.map((file, index) => (
            <li key={`${file.name}-${index}`}><div><strong>{file.name}</strong><small>{Math.ceil(file.size / 1024)} KB</small></div></li>
          ))}
        </ul>
      ) : null}
    </div>
  </div>;
}

function CourseFields({ form, options, setForm }) {
  const change = (field) => (event) => setForm((value) => ({ ...value, [field]: event.target.value }));
  return <>
    <div className="form-group"><label htmlFor="creator-course-code">Course code</label><input id="creator-course-code" required value={form.code} onChange={change('code')} /></div>
    <div className="form-group"><label htmlFor="creator-course-title">Title</label><input id="creator-course-title" required value={form.title} onChange={change('title')} /></div>
    <div className="form-group"><label htmlFor="creator-course-description">Description</label><textarea id="creator-course-description" value={form.description} onChange={change('description')} /></div>
    <div className="form-group"><label htmlFor="creator-course-level">Course level</label><select id="creator-course-level" required value={form.courseLevelId} onChange={change('courseLevelId')}><option value="">Choose a level</option>{options.courseLevels.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}</select></div>
    <div className="form-group"><label htmlFor="creator-learning-path">Learning path</label><select id="creator-learning-path" required value={form.learningPathId} onChange={change('learningPathId')}><option value="">Choose a path</option>{options.learningPaths.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}</select></div>
    <div className="form-group"><label htmlFor="creator-course-category">Category</label><input id="creator-course-category" required value={form.category} onChange={change('category')} /></div>
    <div className="form-group"><label htmlFor="creator-course-cost">Credit cost</label><input id="creator-course-cost" type="number" min="1" step="1" required value={form.creditCost} onChange={change('creditCost')} /></div>
    <div className="form-group"><label htmlFor="creator-course-outcomes">Learning outcomes</label><textarea id="creator-course-outcomes" value={form.learningOutcomes} onChange={change('learningOutcomes')} placeholder="One outcome per line" /></div>
  </>;
}

function toForm(course) {
  return {
    code: course.code,
    title: course.title,
    description: course.description || '',
    courseLevelId: String(course.courseLevelId || ''),
    learningPathId: String(course.learningPathId || ''),
    category: course.category,
    creditCost: String(course.creditCost || ''),
    learningOutcomes: (course.learningOutcomes || []).join('\n'),
  };
}

function toPayload(form) {
  return {
    ...form,
    courseLevelId: Number(form.courseLevelId),
    learningPathId: Number(form.learningPathId),
    creditCost: Number(form.creditCost),
    learningOutcomes: form.learningOutcomes.split('\n').map((item) => item.trim()).filter(Boolean),
  };
}
