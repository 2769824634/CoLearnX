import { useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import useTrainerQuery, { loadTrainerOverview } from './useTrainerQuery';
import { TrainerError, TrainerHeader, TrainerLoading } from './TrainerUi';

export default function TrainerIntakesPage() {
  const query = useTrainerQuery(loadTrainerOverview);
  const [params, setParams] = useSearchParams();
  const [search, setSearch] = useState('');
  const status = params.get('status') || '';
  const term = search.trim().toLowerCase();
  const intakes = query.data?.intakes || [];
  const courses = (query.data?.invitations || [])
    .filter((invitation) => invitation.status === 'Accepted')
    .map((invitation) => ({
      ...invitation,
      intakes: intakes.filter((intake) => intake.courseId === invitation.courseId),
    }))
    .filter((course) => {
      const matchingIntakes = !status || course.intakes.some((intake) => status === 'delivery'
        ? ['Published', 'InProgress'].includes(intake.status)
        : intake.status === status);
      return matchingIntakes && `${course.courseCode} ${course.courseTitle} ${course.creatorName}`.toLowerCase().includes(term);
    });
  return <section className="trainer-page">
    <TrainerHeader eyebrow="Course" title="My courses" action={<button type="button" className="btn btn-ghost" onClick={query.refresh} disabled={query.loading}>Refresh</button>}>
      Courses appear here after you accept a teaching invitation. Open a Course to manage its classes, learners and assessments.
    </TrainerHeader>
    <div className="trainer-toolbar">
      <div className="form-group"><label htmlFor="course-search">Find a Course</label><input id="course-search" type="search" placeholder="Course title or code" value={search} onChange={(event) => setSearch(event.target.value)} /></div>
      <div className="form-group"><label htmlFor="course-status">Class status</label><select id="course-status" value={status} onChange={(event) => setParams(event.target.value ? { status: event.target.value } : {})}><option value="">All statuses</option><option value="delivery">Published & in progress</option><option value="Published">Published</option><option value="InProgress">In progress</option><option value="Completed">Completed</option></select></div>
      <button type="button" className="btn btn-ghost" onClick={query.refresh} disabled={query.loading}>Refresh</button>
    </div>
    {query.loading ? <TrainerLoading /> : query.error ? <TrainerError error={query.error} onRetry={query.refresh} /> : <>
      <TrainerError error={query.data.catalogError} onRetry={query.refresh} retryLabel="Reload course names" />
      <p className="trainer-result-count">{courses.length} {courses.length === 1 ? 'Course' : 'Courses'}</p>
      {courses.length ? <div className="trainer-intake-list">{courses.map((course) => <article className="trainer-intake-row" key={course.courseId}>
        <div className="trainer-intake-title"><small>{course.courseCode} · from {course.creatorName}</small><h3><Link to={`/trainer/courses/${course.courseId}`}>{course.courseTitle}</Link></h3><p>{course.intakes.length} {course.intakes.length === 1 ? 'class' : 'classes'}</p></div>
        <Link className="trainer-open" to={`/trainer/courses/${course.courseId}`} aria-label={`Open ${course.courseTitle}`}>Open →</Link>
      </article>)}</div> : <div className="trainer-empty">{status || term ? 'No courses match these filters.' : 'No accepted teaching courses yet. Accept a teaching invitation to add a Course.'}</div>}
    </>}
  </section>;
}
