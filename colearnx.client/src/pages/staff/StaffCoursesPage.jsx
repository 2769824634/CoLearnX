import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { staffCoursesApi } from '../../api';
import useStaffAuth from '../../auth/useStaffAuth';

export default function StaffCoursesPage() {
  const { token } = useStaffAuth();
  const [courses, setCourses] = useState([]);
  const [error, setError] = useState('');
  useEffect(() => {
    const controller = new AbortController();
    staffCoursesApi.list(token, controller.signal).then(setCourses)
      .catch((err) => { if (err.name !== 'AbortError') setError(err.message); });
    return () => controller.abort();
  }, [token]);
  return <section className="creator-page"><div className="page-header"><div><p className="eyebrow">CoLearnX catalogue</p><h1>Platform courses</h1><p>Every course here is owned and displayed by CoLearnX.</p></div><Link className="btn btn-primary" to="/staff/courses/new">Create platform course</Link></div>
    {error ? <div className="callout warn">{error}</div> : null}
    <div className="creator-application-list">{courses.map((course) => <Link key={course.id} className="creator-application-row" to={`/staff/courses/${course.id}`}><div className="creator-application-index">{course.code}</div><div><h2>{course.title}</h2><p>{course.courseLevelName} · {course.learningPathName}</p></div><span className={`creator-status ${course.status.toLowerCase()}`}>{course.status}</span></Link>)}</div>
  </section>;
}
