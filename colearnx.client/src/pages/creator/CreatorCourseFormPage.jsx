import { useEffect, useRef, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { creatorCoursesApi, interestsApi, materialsApi } from '../../api';
import useTrainerQuery from '../trainer/useTrainerQuery';
import CourseMaterialsCard from './CourseMaterialsCard';
import { categoryChoices } from './courseCategories';
import { CreatorError, CreatorHeader } from './CreatorUi';

async function loadCourseForm(token, courseId, signal) {
  const [options, interestTree] = await Promise.all([creatorCoursesApi.options(token, signal), interestsApi.tree()]);
  options.interestTree = Array.isArray(interestTree) ? interestTree : [];
  if (courseId === 'new') return { course: null, options, materials: [] };
  const [course, materials] = await Promise.all([
    creatorCoursesApi.get(token, courseId, signal),
    materialsApi.list(undefined, courseId, token, signal),
  ]);
  return { course, options, materials };
}

const emptyCourse = {
  code: '', title: '', description: '', courseLevelId: '', learningPathId: '',
  creditCost: '', learningOutcomes: [], interestIds: [], status: 'Draft',
};

const NEW_COURSE_STEPS = [
  ['Basic information', 'Name the Course and place it in the catalogue.'],
  ['Materials', 'Optional. Add files now or come back to this step later.'],
  ['Interests & price', 'Choose 1–4 interests and set the credit cost, then save.'],
];
const EXISTING_COURSE_STEPS = [
  ['Basic information', 'Name the Course and place it in the catalogue.'],
  ['Interests & price', 'Choose 1–4 interests and set the credit cost, then save.'],
];

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
  const [savedSnapshot, setSavedSnapshot] = useState(() => JSON.stringify(toPayload(toForm(initialCourse || emptyCourse))));
  const [error, setError] = useState(null);
  const [busy, setBusy] = useState(false);
  const steps = course ? EXISTING_COURSE_STEPS : NEW_COURSE_STEPS;
  const lastStep = steps.length - 1;
  const [uploadOpen, setUploadOpen] = useState(false);
  const [step, setStep] = useState(0);
  const [reached, setReached] = useState(initialCourse ? EXISTING_COURSE_STEPS.length - 1 : 0);
  const formRef = useRef(null);
  const stepHeadingRef = useRef(null);
  const movedRef = useRef(false);
  const editable = !course || course.status === 'Draft' || course.status === 'Rejected';

  useEffect(() => {
    if (movedRef.current) stepHeadingRef.current?.focus();
  }, [step]);

  // Moving forward only checks the fields on the current step; earlier steps were checked when they were left.
  function goTo(target) {
    if (target > step && !formRef.current?.reportValidity()) return;
    movedRef.current = true;
    setStep(target);
    setReached((value) => Math.max(value, target));
  }

  function onStepSubmit(event) {
    event.preventDefault();
    if (!event.currentTarget.reportValidity()) return;
    if (step < lastStep) goTo(step + 1);
    else save();
  }

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

  async function save() {
    setBusy(true);
    setError(null);
    try {
      const payload = toPayload(form);
      if (course) {
        const updated = await creatorCoursesApi.update(token, course.id, payload);
        setCourse(updated);
        setForm(toForm(updated));
        setSavedSnapshot(JSON.stringify(toPayload(toForm(updated))));
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
    if (JSON.stringify(toPayload(form)) !== savedSnapshot) {
      setError({
        code: 'UNSAVED_CHANGES',
        status: 400,
        message: 'Save your changes before submitting for approval.',
      });
      return;
    }
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
    <CreatorHeader eyebrow={course ? course.code : 'New Course'} title={title} action={course ? <div className="creator-header-actions"><span className={`creator-status ${course.status.toLowerCase()}`}>{course.status}</span><button type="button" className="btn btn-primary" onClick={() => setUploadOpen(true)}>Upload materials</button></div> : null}>
      {course
        ? editable ? 'Review the Course definition and submit it for Admin approval when it is ready.'
          : 'View the submitted Course definition and its materials.'
        : 'Three steps: basic information, materials, then interests and price. The Course is saved when you finish the last step.'}
    </CreatorHeader>
    <CreatorError error={error} />
    {course?.reviewReason ? <div className="creator-error"><strong>Admin feedback</strong><p>{course.reviewReason}</p></div> : null}
    {!editable ? <>
      <div className="creator-empty"><strong>Course editing is locked</strong><p>{course?.status === 'Published'
        ? 'Published definitions are read-only. Contact your course administrator to discuss changes.'
        : 'This submitted Course is locked while Admin reviews it. If rejected, you can update it and submit again.'} Its definition remains available below.</p></div>
      <div className="card creator-course-definition">
        <fieldset disabled className="creator-course-fields"><legend>Submitted Course definition (read-only)</legend>
          <BasicFields form={form} options={options} setForm={setForm} />
          <InterestPriceFields form={form} options={options} setForm={setForm} />
        </fieldset>
      </div>
    </> : <>
      <ol className="creator-steps" aria-label="Course creation steps">
        {steps.map(([label], index) => <li key={label} data-state={index < step ? 'done' : index === step ? 'current' : 'todo'}>
          <button type="button" aria-current={index === step ? 'step' : undefined} disabled={busy || index > reached} onClick={() => goTo(index)}>
            <span className="creator-step-number">{index + 1}</span><span>{label}</span>
          </button>
        </li>)}
      </ol>
      <form ref={formRef} className="card creator-course-definition creator-step-card" onSubmit={onStepSubmit}>
        <div className="creator-step-heading">
          <p>Step {step + 1} of {steps.length}</p>
          <h2 ref={stepHeadingRef} tabIndex={-1}>{steps[step][0]}</h2>
          <span>{steps[step][1]}</span>
        </div>
        <fieldset disabled={busy} className="creator-course-fields">
          {steps[step][0] === 'Basic information' ? <BasicFields form={form} options={options} setForm={setForm} /> : null}
          {steps[step][0] === 'Materials' ? <MaterialsStep pendingFiles={pendingFiles} setPendingFiles={setPendingFiles} busy={busy} /> : null}
          {steps[step][0] === 'Interests & price' ? <InterestPriceFields form={form} options={options} setForm={setForm} /> : null}
        </fieldset>
        <div className="trainer-actions creator-step-actions">
          {step > 0 ? <button className="btn btn-ghost" type="button" disabled={busy} onClick={() => goTo(step - 1)}>Back</button> : null}
          {step < lastStep ? <button className="btn btn-primary" type="submit" disabled={busy}>Next</button> : <>
            <button className="btn btn-primary" type="submit" disabled={busy}>{busy ? 'Saving…' : course ? 'Save changes' : 'Save draft'}</button>
            {course
              ? <button className="btn btn-teal" type="button" disabled={busy} onClick={submitForApproval}>Submit for approval</button>
              : <button className="btn btn-teal" type="button" disabled={busy} onClick={saveAndSubmit}>Save and submit for approval</button>}
          </>}
        </div>
      </form>
    </>}
    {course ? <CourseMaterialsCard course={course} materials={materials} onMaterials={setMaterials} token={token} open={uploadOpen} onOpenChange={setUploadOpen} /> : null}
  </section>;
}

function MaterialsStep({ pendingFiles, setPendingFiles, busy }) {
  return <>
    <p className="page-sub" style={{ marginTop: 0 }}>PDF, PPTX, DOCX, PNG or JPG · max 20 MB. Files stay with this Course.</p>
    <div className="form-group">
        <label htmlFor="creator-course-materials">Files to upload when this Course is saved</label>
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
            setPendingFiles((current) => [...current, ...files]);
          }}
        />
      </div>
      {pendingFiles.length ? (
        <ul className="later-resource-list">
          {pendingFiles.map((file, index) => (
            <li key={`${file.name}-${index}`}>
              <div><strong>{file.name}</strong><small>{Math.ceil(file.size / 1024)} KB</small></div>
              <button type="button" className="btn btn-ghost btn-sm" aria-label={`Remove ${file.name}`} onClick={() => setPendingFiles((current) => current.filter((_, item) => item !== index))}>Remove</button>
            </li>
          ))}
        </ul>
      ) : null}
  </>;
}

const MAX_INTERESTS = 4;

function InterestPicker({ tree, selectedIds, onToggle }) {
  const [query, setQuery] = useState('');
  const [activeId, setActiveId] = useState(null);
  const needle = query.trim().toLowerCase();
  const selectedLeaves = tree.flatMap((category) => category.children || [])
    .filter((leaf) => selectedIds.includes(leaf.id));
  const atLimit = selectedIds.length >= MAX_INTERESTS;
  const groups = tree
    .map((category) => ({ category, leaves: (category.children || []).filter((leaf) => leaf.name.toLowerCase().includes(needle)) }))
    .filter((group) => group.leaves.length);
  const preferredId = groups.find((group) => group.leaves.some((leaf) => selectedIds.includes(leaf.id)))?.category.id
    ?? groups[0]?.category.id;
  const active = groups.find((group) => group.category.id === (activeId ?? preferredId)) ?? groups[0];

  return <div className="creator-interests">
    <div className="creator-interest-status">
      <p aria-live="polite">Selected {selectedIds.length} of {MAX_INTERESTS}{atLimit ? '. Uncheck one interest before choosing another.' : '.'}</p>
      <span className="creator-interest-meter" aria-hidden="true">
        {Array.from({ length: MAX_INTERESTS }, (_, index) => <span key={index} className={index < selectedIds.length ? 'filled' : ''} />)}
      </span>
    </div>
    {selectedLeaves.length
      ? <ul className="creator-interest-chips" aria-label="Selected interests">{selectedLeaves.map((leaf) => <li key={leaf.id}>
        {leaf.name}
        <button type="button" aria-label={`Remove ${leaf.name}`} onClick={() => onToggle(leaf.id)}>✕</button>
      </li>)}</ul>
      : <p className="creator-interest-empty">No interests selected yet.</p>}
    <input id="creator-interest-search" className="creator-interest-search" type="search" value={query} onChange={(event) => setQuery(event.target.value)} placeholder="Search interests" />
    {groups.length && active ? <>
      <div className="creator-interest-categories" aria-label="Interest categories">
        {groups.map(({ category, leaves }) => {
          const chosen = leaves.filter((leaf) => selectedIds.includes(leaf.id)).length;
          return <button type="button" key={category.id} className="creator-interest-category" aria-pressed={category.id === active.category.id} onClick={() => setActiveId(category.id)}>
            <span>{category.name}</span>
            <small className={chosen ? 'has-selection' : ''}>{chosen ? `${chosen} of ${leaves.length} selected` : leaves.length}</small>
          </button>;
        })}
      </div>
      <div className="creator-interest-options">{active.leaves.map((leaf) => {
        const checked = selectedIds.includes(leaf.id);
        return <label key={leaf.id} className="creator-interest-option">
          <input type="checkbox" checked={checked} disabled={!checked && atLimit} onChange={() => onToggle(leaf.id)} />
          <span>{leaf.name}</span>
        </label>;
      })}</div>
    </> : <p className="creator-interest-empty">No interests match “{query.trim()}”.</p>}
  </div>;
}

const fieldChanger = (setForm) => (field) => (event) => setForm((value) => ({ ...value, [field]: event.target.value }));

function BasicFields({ form, options, setForm }) {
  const change = fieldChanger(setForm);
  return <>
    <div className="creator-step-grid">
      <div className="form-group"><label htmlFor="creator-course-code">Course code</label><input id="creator-course-code" required value={form.code} onChange={change('code')} /></div>
      <div className="form-group"><label htmlFor="creator-course-title">Title</label><input id="creator-course-title" required value={form.title} onChange={change('title')} /></div>
    </div>
    <div className="form-group"><label htmlFor="creator-course-description">Description</label><textarea id="creator-course-description" value={form.description} onChange={change('description')} /></div>
    <div className="creator-step-grid">
      <div className="form-group"><label htmlFor="creator-course-level">Course level</label><select id="creator-course-level" required value={form.courseLevelId} onChange={change('courseLevelId')}><option value="">Choose a level</option>{options.courseLevels.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}</select></div>
      <div className="form-group"><label htmlFor="creator-learning-path">Learning path</label><select id="creator-learning-path" required value={form.learningPathId} onChange={change('learningPathId')}><option value="">Choose a path</option>{options.learningPaths.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}</select></div>
    </div>
    <div className="form-group"><label htmlFor="creator-course-category">Category (optional)</label><select id="creator-course-category" value={form.category || ''} onChange={change('category')}><option value="">No category</option>{categoryChoices(form.category).map((item) => <option key={item} value={item}>{item}</option>)}</select></div>
    <div className="form-group"><label htmlFor="creator-course-outcomes">Learning outcomes</label><textarea id="creator-course-outcomes" value={form.learningOutcomes} onChange={change('learningOutcomes')} placeholder="One outcome per line" /></div>
  </>;
}

function InterestPriceFields({ form, options, setForm }) {
  const change = fieldChanger(setForm);
  const toggleInterest = (id) => setForm((value) => ({ ...value,
    interestIds: value.interestIds.includes(id) ? value.interestIds.filter((item) => item !== id)
      : value.interestIds.length < MAX_INTERESTS ? [...value.interestIds, id] : value.interestIds }));
  return <>
    <div className="form-group"><label htmlFor="creator-interest-search">Course interests (choose 1–4 leaves before submitting)</label>
      <InterestPicker tree={options.interestTree || []} selectedIds={form.interestIds} onToggle={toggleInterest} />
    </div>
    <div className="form-group creator-price-field"><label htmlFor="creator-course-cost">Credit cost</label><input id="creator-course-cost" type="number" min="1" step="1" required value={form.creditCost} onChange={change('creditCost')} aria-describedby="creator-course-cost-hint" />
      <small id="creator-course-cost-hint">Whole credits a Member pays to enrol.</small>
    </div>
  </>;
}

function toForm(course) {
  return {
    code: course.code,
    title: course.title,
    description: course.description || '',
    courseLevelId: String(course.courseLevelId || ''),
    learningPathId: String(course.learningPathId || ''),
    category: course.category || '',
    creditCost: String(course.creditCost || ''),
    learningOutcomes: (course.learningOutcomes || []).join('\n'),
    interestIds: course.interestIds || [],
  };
}

function toPayload(form) {
  return {
    ...form,
    courseLevelId: Number(form.courseLevelId),
    learningPathId: Number(form.learningPathId),
    creditCost: Number(form.creditCost),
    learningOutcomes: form.learningOutcomes.split('\n').map((item) => item.trim()).filter(Boolean),
    interestIds: form.interestIds,
  };
}
