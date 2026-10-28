import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { interestsApi, staffCoursesApi } from '../../api';
import useStaffAuth from '../../auth/useStaffAuth';
import { InterestPicker } from '../creator/CreatorCourseFormPage';

const empty = { code: '', title: '', description: '', courseLevelId: '', learningPathId: '', category: '', creditCost: 1, learningOutcomes: [], interestIds: [] };

export default function StaffCourseFormPage() {
  const { courseId } = useParams();
  const { token } = useStaffAuth();
  const navigate = useNavigate();
  const [form, setForm] = useState(empty);
  const [options, setOptions] = useState({ courseLevels: [], learningPaths: [], interestTree: [] });
  const [error, setError] = useState('');
  useEffect(() => {
    const controller = new AbortController();
    Promise.all([
      staffCoursesApi.options(token, controller.signal),
      interestsApi.tree(token),
      courseId ? staffCoursesApi.get(token, courseId, controller.signal) : Promise.resolve(null),
    ]).then(([nextOptions, interestTree, course]) => {
      setOptions({ ...nextOptions, interestTree: Array.isArray(interestTree) ? interestTree : [] });
      if (course) setForm({ ...course, learningOutcomes: course.learningOutcomes || [] });
    }).catch((err) => { if (err.name !== 'AbortError') setError(err.message); });
    return () => controller.abort();
  }, [courseId, token]);
  async function save(event) {
    event.preventDefault();
    setError('');
    const body = { ...form, courseLevelId: Number(form.courseLevelId), learningPathId: Number(form.learningPathId), creditCost: Number(form.creditCost), interestIds: form.interestIds || [] };
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
      <div className="form-group"><label htmlFor="platform-path">Learning path</label><select id="platform-path" required value={form.learningPathId} onChange={(e) => setForm({ ...form, learningPathId: e.target.value })}><option value="">Select path</option>{options.learningPaths.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}</select></div>
      <div className="form-group"><label htmlFor="platform-category">Category</label><input id="platform-category" value={form.category || ''} onChange={(e) => setForm({ ...form, category: e.target.value })} /></div>
      <div className="form-group"><label htmlFor="platform-credit">Credit cost</label><input id="platform-credit" type="number" min="1" required value={form.creditCost} onChange={(e) => setForm({ ...form, creditCost: e.target.value })} /></div>
      <InterestPicker tree={options.interestTree} selectedIds={form.interestIds || []} onToggle={(id) => setForm((current) => ({ ...current, interestIds: current.interestIds.includes(id) ? current.interestIds.filter((item) => item !== id) : current.interestIds.length < 4 ? [...current.interestIds, id] : current.interestIds }))} />
      <button type="submit" className="btn btn-primary">Save platform course</button>
      {courseId && form.status !== 'PendingApproval' ? <button type="button" className="btn btn-ghost" onClick={async () => { try { setForm(await staffCoursesApi.submit(token, courseId)); } catch (err) { setError(err.message); } }}>Submit for approval</button> : null}
    </form>
  </section>;
}
