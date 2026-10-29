import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { staffCoursesApi } from '../../api';
import useStaffAuth from '../../auth/useStaffAuth';
import { InterestPicker } from '../creator/CreatorCourseFormPage';
import { staffLaterPhaseApi } from '../../api/trainerLaterPhase';
import TrainerLearnersPage from '../trainer/TrainerLearnersPage';

const empty = {
  code: '', title: '', description: '', courseLevelId: '', learningPathId: '', courseSubjectId: '',
  durationHours: '', category: '', creditCost: '', priceExceptionReason: '', learningOutcomes: [], interestIds: [],
};

function priceCell(form, options) {
  const subject = (options.courseSubjects || []).find((item) => String(item.id) === String(form.courseSubjectId));
  return (options.priceCells || []).find((item) => item.band === subject?.band && String(item.courseLevelId) === String(form.courseLevelId));
}

async function loadStaffLearnerWorkspace(token, courseId, signal) {
  const summaries = await staffCoursesApi.listIntakes(token, signal);
  const selected = summaries.filter((item) => item.courseId === Number(courseId));
  const [cohorts, certificateRequests] = await Promise.all([
    Promise.all(selected.map(async (summary) => {
      const [intake, learners, assessments] = await Promise.all([
        staffCoursesApi.getIntake(token, summary.id, signal),
        staffLaterPhaseApi.learners(token, summary.id, signal),
        staffLaterPhaseApi.assessments(token, summary.id, signal),
      ]);
      return { ...intake, learners, assessments };
    })),
    staffLaterPhaseApi.certificateRequests(token, '', signal),
  ]);
  return { cohorts, certificateRequests };
}

export default function StaffCourseFormPage() {
  const { courseId } = useParams();
  const { token } = useStaffAuth();
  const navigate = useNavigate();
  const [form, setForm] = useState(empty);
  const [options, setOptions] = useState({ courseLevels: [], learningPaths: [], interestTree: [] });
  const [error, setError] = useState('');
  const [learnersOpen, setLearnersOpen] = useState(false);
  useEffect(() => {
    const controller = new AbortController();
    Promise.all([
      staffCoursesApi.options(token, controller.signal),
      staffCoursesApi.interests(token, controller.signal),
      courseId ? staffCoursesApi.get(token, courseId, controller.signal) : Promise.resolve(null),
    ]).then(([nextOptions, interestTree, course]) => {
      setOptions({ ...nextOptions, interestTree: Array.isArray(interestTree) ? interestTree : [] });
      if (course) setForm({
        ...empty,
        ...course,
        courseLevelId: String(course.courseLevelId || ''),
        learningPathId: String(course.learningPathId || ''),
        courseSubjectId: String(course.courseSubjectId || ''),
        durationHours: course.durationHours ? String(course.durationHours) : '',
        creditCost: course.creditCost ?? '',
        priceExceptionReason: course.priceExceptionReason || '',
        learningOutcomes: course.learningOutcomes || [],
        interestIds: course.interestIds || [],
      });
    }).catch((err) => { if (err.name !== 'AbortError') setError(err.message); });
    return () => controller.abort();
  }, [courseId, token]);
  async function save(event) {
    event.preventDefault();
    setError('');
    const body = {
      ...form,
      courseLevelId: Number(form.courseLevelId),
      learningPathId: Number(form.learningPathId),
      courseSubjectId: Number(form.courseSubjectId),
      durationHours: Number(form.durationHours),
      creditCost: Number(form.creditCost),
      interestIds: form.interestIds || [],
    };
    try {
      const saved = courseId ? await staffCoursesApi.update(token, courseId, body) : await staffCoursesApi.create(token, body);
      navigate(`/staff/courses/${saved.id}`, { replace: true });
    } catch (err) { setError(err.message || 'Could not save course.'); }
  }
  return <section className="creator-page"><div className="page-header"><div><p className="eyebrow">Platform course</p><h1>{courseId ? 'Edit course' : 'Create course'}</h1></div></div>
    {error ? <div className="callout warn">{error}</div> : null}
    <form className="card" onSubmit={save}>
      <div className="form-group"><label htmlFor="platform-code">Course code</label><input id="platform-code" required value={form.code} onChange={(e) => setForm({ ...form, code: e.target.value })} /></div>
      <div className="form-group"><label htmlFor="platform-title">Title</label><input id="platform-title" required value={form.title} onChange={(e) => setForm({ ...form, title: e.target.value })} /></div>
      <div className="form-group"><label htmlFor="platform-description">Description</label><textarea id="platform-description" value={form.description || ''} onChange={(e) => setForm({ ...form, description: e.target.value })} /></div>
      <div className="form-group"><label htmlFor="platform-level">Level</label><select id="platform-level" required value={form.courseLevelId} onChange={(e) => setForm({ ...form, courseLevelId: e.target.value })}><option value="">Select level</option>{options.courseLevels.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}</select></div>
      <div className="form-group"><label htmlFor="platform-subject">Subject</label><select id="platform-subject" required value={form.courseSubjectId} onChange={(e) => setForm({ ...form, courseSubjectId: e.target.value })}><option value="">Select subject</option>{(options.courseSubjects || []).map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}</select></div>
      <div className="form-group"><label htmlFor="platform-path">Learning path</label><select id="platform-path" required value={form.learningPathId} onChange={(e) => setForm({ ...form, learningPathId: e.target.value })}><option value="">Select path</option>{options.learningPaths.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}</select></div>
      <div className="form-group"><label htmlFor="platform-hours">Duration (hours)</label><input id="platform-hours" type="number" min="1" step="1" required value={form.durationHours} onChange={(e) => setForm({ ...form, durationHours: e.target.value })} /></div>
      <div className="form-group"><label htmlFor="platform-category">Category</label><input id="platform-category" value={form.category || ''} onChange={(e) => setForm({ ...form, category: e.target.value })} /></div>
      <div className="form-group"><label htmlFor="platform-credit">Credit cost</label><input id="platform-credit" type="number" min={priceCell(form, options)?.minCredits} max={priceCell(form, options) ? priceCell(form, options).maxCredits * 2 : undefined} step="1" required value={form.creditCost} onChange={(e) => setForm({ ...form, creditCost: e.target.value })} aria-describedby="platform-credit-hint" /><small id="platform-credit-hint">{priceCell(form, options) ? `Enter a whole number from ${priceCell(form, options).minCredits} to ${priceCell(form, options).maxCredits} credits. A higher price up to ${priceCell(form, options).maxCredits * 2} needs a reason.` : 'Choose a level and subject before setting the price.'}</small></div>
      {priceCell(form, options) && Number(form.creditCost) > priceCell(form, options).maxCredits ? <div className="form-group"><label htmlFor="platform-price-reason">Reason for a higher price</label><textarea id="platform-price-reason" required maxLength={500} value={form.priceExceptionReason} onChange={(e) => setForm({ ...form, priceExceptionReason: e.target.value })} /></div> : null}
      <InterestPicker tree={options.interestTree} selectedIds={form.interestIds || []} onToggle={(id) => setForm((current) => ({ ...current, interestIds: current.interestIds.includes(id) ? current.interestIds.filter((item) => item !== id) : current.interestIds.length < 4 ? [...current.interestIds, id] : current.interestIds }))} />
      <button type="submit" className="btn btn-primary">Save platform course</button>
      {courseId && form.status !== 'PendingApproval' ? <button type="button" className="btn btn-ghost" onClick={async () => { try { setForm(await staffCoursesApi.submit(token, courseId)); } catch (err) { setError(err.message); } }}>Submit for approval</button> : null}
    </form>
    {courseId ? <section className="later-panel">
      <div className="trainer-section-heading"><div><p className="trainer-eyebrow">Course operations</p><h2>Learners & assessments</h2></div><button type="button" className="btn btn-ghost" aria-expanded={learnersOpen} onClick={() => setLearnersOpen((value) => !value)}>{learnersOpen ? 'Hide learner list' : 'Show learner list'}</button></div>
      {learnersOpen ? <TrainerLearnersPage courseId={Number(courseId)} embedded token={token} laterPhaseApi={staffLaterPhaseApi} loadWorkspace={loadStaffLearnerWorkspace} reviewRoleLabel="Staff" /> : <p className="trainer-help">Expand to manage learners, assessments, grades and certificate reviews for this platform course.</p>}
    </section> : null}
  </section>;
}
