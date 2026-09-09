import { useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { creatorCoursesApi } from '../../api';
import useTrainerQuery from '../trainer/useTrainerQuery';
import { CreatorError, CreatorHeader } from './CreatorUi';

async function loadCourseForm(token, courseId, signal) {
  const options = await creatorCoursesApi.options(token, signal);
  const course = courseId === 'new' ? null : await creatorCoursesApi.get(token, courseId, signal);
  return { course, options };
}

const emptyCourse = {
  code: '', title: '', description: '', courseLevelId: '', learningPathId: '',
  category: '', creditCost: '', learningOutcomes: [], status: 'Draft',
};

export default function CreatorCourseFormPage() {
  const { courseId } = useParams();
  const key = courseId || 'new';
  const query = useTrainerQuery(loadCourseForm, key);

  if (query.loading) return <section className="creator-page"><div className="creator-empty" role="status">Loading Course…</div></section>;
  if (query.error) return <section className="creator-page"><CreatorError error={query.error} onRetry={query.refresh} /></section>;
  return <CourseEditor key={`${key}:${query.data.course?.status || 'new'}`} initialCourse={query.data.course} options={query.data.options} token={query.token} />;
}

function CourseEditor({ initialCourse, options, token }) {
  const navigate = useNavigate();
  const [course, setCourse] = useState(initialCourse);
  const [form, setForm] = useState(() => toForm(initialCourse || emptyCourse));
  const [error, setError] = useState(null);
  const [busy, setBusy] = useState(false);
  const editable = !course || course.status === 'Draft' || course.status === 'Rejected';

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
      } else {
        const created = await creatorCoursesApi.create(token, payload);
        navigate(`/creator/courses/${created.id}`);
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
      setCourse(await creatorCoursesApi.submit(token, course.id));
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
      {course ? 'Review the Course definition and its approval state.' : 'Save the core Course definition as a Draft.'}
    </CreatorHeader>
    <CreatorError error={error} />
    {course?.reviewReason ? <div className="creator-error"><strong>Admin feedback</strong><p>{course.reviewReason}</p></div> : null}
    {editable ? <form className="card" onSubmit={save}>
      <CourseFields form={form} options={options} setForm={setForm} />
      <div className="trainer-actions">
        <button className="btn btn-primary" type="submit" disabled={busy}>{busy ? 'Saving…' : course ? 'Save changes' : 'Save draft'}</button>
        {course ? <button className="btn btn-teal" type="button" disabled={busy} onClick={submitForApproval}>Submit for approval</button> : null}
      </div>
    </form> : <div className="creator-empty"><strong>Course editing is locked</strong><p>This Course can be edited again only if Admin rejects it.</p></div>}
  </section>;
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

