import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { staffCoursesApi } from '../../api';
import useStaffAuth from '../../auth/useStaffAuth';
import { formatDate, statusLabel } from '../trainer/intakeForm';

export default function StaffIntakesPage() {
  const { token } = useStaffAuth();
  const [data, setData] = useState({ courses: [], intakes: [] });
  const [error, setError] = useState('');
  useEffect(() => {
    const controller = new AbortController();
    Promise.all([
      staffCoursesApi.list(token, controller.signal),
      staffCoursesApi.listIntakes(token, controller.signal),
    ]).then(([courses, intakes]) => setData({ courses, intakes }))
      .catch((failure) => { if (failure.name !== 'AbortError') setError(failure.message); });
    return () => controller.abort();
  }, [token]);
  const courses = new Map(data.courses.map((course) => [course.id, course]));
  return <section className="creator-page">
    <div className="page-header"><div><p className="eyebrow">CoLearnX delivery</p><h1>Platform classes</h1><p>Open classes for your published platform courses, then add sessions and publish the schedule.</p></div><Link className="btn btn-primary" to="/staff/intakes/new">Open a class</Link></div>
    {error ? <div className="callout warn">{error}</div> : null}
    <div className="creator-application-list">{data.intakes.map((intake) => {
      const course = courses.get(intake.courseId);
      return <article className="creator-application-row" key={intake.id}>
        <div className="creator-application-index">#{intake.id}</div>
        <div><p className="creator-eyebrow">{course?.code || `Course #${intake.courseId}`}</p><h2>{course?.title || `Course #${intake.courseId}`}</h2><p>{formatDate(intake.startsAt)} → {formatDate(intake.endsAt)}</p></div>
        <span className={`creator-status ${(intake.status || '').toLowerCase()}`}>{statusLabel(intake.status)}</span>
        <Link to={`/staff/intakes/${intake.id}`}>Open →</Link>
      </article>;
    })}</div>
  </section>;
}
