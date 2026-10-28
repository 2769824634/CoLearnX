import { useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { staffCoursesApi } from '../../api';
import useStaffAuth from '../../auth/useStaffAuth';
import { ScheduleForm } from '../trainer/IntakeForms';

export default function StaffIntakeCreatePage() {
  const { token, staff } = useStaffAuth();
  const navigate = useNavigate();
  const [courses, setCourses] = useState([]);
  const [error, setError] = useState(null);
  const [busy, setBusy] = useState(false);
  useEffect(() => {
    const controller = new AbortController();
    staffCoursesApi.list(token, controller.signal)
      .then((items) => setCourses(items.filter((course) => course.status === 'Published')))
      .catch((failure) => { if (failure.name !== 'AbortError') setError(failure); });
    return () => controller.abort();
  }, [token]);
  async function create(body, courseId) {
    if (busy) return;
    setBusy(true);
    setError(null);
    try {
      const intake = await staffCoursesApi.createIntake(token, courseId, body);
      navigate(`/staff/intakes/${intake.id}`, { replace: true });
    } catch (failure) { setError(failure); }
    finally { setBusy(false); }
  }
  const trainers = staff ? [{ trainerId: staff.userId, fullName: staff.displayName }] : [];
  return <section className="creator-page"><Link className="creator-back" to="/staff/intakes">← All classes</Link><div className="page-header"><div><p className="eyebrow">Platform classes</p><h1>Open a class</h1></div></div>
    {courses.length ? <ScheduleForm courses={courses} trainers={trainers} onSave={create} onCancel={() => navigate('/staff/intakes')} busy={busy} error={error} onError={setError} /> : <div className="creator-empty"><strong>No published platform courses</strong><p>A course must be approved and published before you can open a class.</p></div>}
  </section>;
}
