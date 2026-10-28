import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { coursesApi } from '../../api';

export default function MemberPlatformCoursesPage() {
  const [courses, setCourses] = useState([]);
  const [filters, setFilters] = useState({ category: '', level: '', tag: '' });
  const [error, setError] = useState('');
  useEffect(() => {
    coursesApi.platform(filters).then(setCourses).catch((err) => setError(err.message));
  }, [filters]);
  return <section className="member-page"><div className="page-header"><div><p className="eyebrow">Created by CoLearnX</p><h1>CoLearnX courses</h1><p>Platform-owned programs taught by numbered CoLearnX instructors.</p></div></div>
    <div className="toolbar"><label>Category<select value={filters.category} onChange={(e) => setFilters({ ...filters, category: e.target.value })}><option value="">All</option><option value="IT & Data">IT & Data</option><option value="Business & Management">Business</option><option value="Lifestyle & Crafts">Lifestyle</option></select></label><label>Level<select value={filters.level} onChange={(e) => setFilters({ ...filters, level: e.target.value })}><option value="">All</option><option>Beginner</option><option>Intermediate</option><option>Advanced</option></select></label><label>Tag<input type="search" value={filters.tag} placeholder="e.g. Cloud" onChange={(e) => setFilters({ ...filters, tag: e.target.value })} /></label></div>
    {error ? <div className="callout warn">{error}</div> : null}
    <div className="grid-3">{courses.map((course) => <Link className="course-card" key={course.id} to={`/member/courses/${course.id}`}><div className="thumb">{course.code}</div><h2>{course.title}</h2><p>Creator: CoLearnX</p><p>{course.trainerNames?.join(', ') || course.trainerName}</p></Link>)}</div>
  </section>;
}
