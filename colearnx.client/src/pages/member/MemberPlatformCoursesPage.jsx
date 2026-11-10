import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import MemberShell from '../../components/MemberShell';
import { coursesApi } from '../../api';
import { CourseArt } from '../guest/GuestCourseCard';

export default function MemberPlatformCoursesPage() {
  const navigate = useNavigate();
  const [courses, setCourses] = useState([]);
  const [filters, setFilters] = useState({ category: '', level: '', tag: '' });
  const [error, setError] = useState('');
  useEffect(() => {
    coursesApi.platform(filters).then(setCourses).catch((err) => setError(err.message));
  }, [filters]);
  return (
    <MemberShell title="CoLearnX courses" subtitle="Platform-owned programs taught by numbered CoLearnX instructors.">
      <div className="cx-catalog">
        <div className="cx-filters">
          <select aria-label="Category" value={filters.category} onChange={(e) => setFilters({ ...filters, category: e.target.value })}>
            <option value="">All categories</option>
            <option value="IT & Data">IT & Data</option>
            <option value="Business & Management">Business</option>
            <option value="Lifestyle & Crafts">Lifestyle</option>
          </select>
          <select aria-label="Level" value={filters.level} onChange={(e) => setFilters({ ...filters, level: e.target.value })}>
            <option value="">All levels</option>
            <option>Beginner</option>
            <option>Intermediate</option>
            <option>Advanced</option>
          </select>
          <label className="cx-search">
            <input type="search" aria-label="Tag" value={filters.tag} placeholder="Tag, e.g. Cloud" onChange={(e) => setFilters({ ...filters, tag: e.target.value })} />
          </label>
        </div>
        {error ? <div className="callout warn">{error}</div> : null}
        <div className="cx-grid">
          {courses.map((course) => {
            const open = () => navigate(`/member/courses/${course.id}`);
            const trainers = course.trainerNames?.length ? course.trainerNames.join(', ') : course.trainerName;
            return (
              <article className="cx-card" key={course.id}>
                <button type="button" className="cx-card-art" tabIndex={-1} aria-hidden="true" onClick={open}>
                  <CourseArt code={course.code} path={course.learningPath} />
                </button>
                <div className="cx-card-body">
                  <div className="cx-card-meta">
                    {course.level ? <span className="cx-chip">{course.level}</span> : null}
                    {course.credits != null || course.creditCost != null ? <span className="cx-card-credits"><strong>{course.credits ?? course.creditCost}</strong> Credits</span> : null}
                  </div>
                  <h4 className="cx-card-title">{course.title}</h4>
                  <p className="cx-card-people">Creator <b>CoLearnX</b></p>
                  <p className="cx-card-people">{trainers ? <>Trainer <b>{trainers}</b></> : 'Trainer assigned when a class opens'}</p>
                  <div className="cx-card-actions">
                    <button type="button" className="btn btn-primary btn-sm" onClick={open}>View Details</button>
                  </div>
                </div>
              </article>
            );
          })}
        </div>
      </div>
    </MemberShell>
  );
}
