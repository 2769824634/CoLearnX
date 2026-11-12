import { useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { creatorCoursesApi } from '../../api';
import { creatorIntakesApi } from '../../api/creatorIntakes';
import { useAuth } from '../../auth/AuthContext';
import OpenClassForm from './OpenClassForm';
import useTrainerQuery from '../trainer/useTrainerQuery';
import { CreatorError, CreatorHeader } from './CreatorUi';

const loadCourses = (token, key, signal) => creatorCoursesApi.list(token, signal);
const loadCourse = (token, courseId, signal) => creatorCoursesApi.get(token, courseId, signal);

function CourseClassForm({ course, onCancel, onCreated }) {
  const { token } = useAuth();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);

  async function create(body) {
    if (busy) return;
    setBusy(true);
    setError(null);
    try {
      const intake = await creatorIntakesApi.openClass(token, course.id, body);
      onCreated(intake);
    } catch (failure) { setError(failure); }
    finally { setBusy(false); }
  }

  if (course.status !== 'Published') {
    return <div className="creator-empty"><strong>Publish this course first</strong><p>An Admin must approve the course before you create an intake.</p></div>;
  }
  return <>
    <CreatorHeader eyebrow="Intakes / New" title="Create an intake.">
      Enter every session of this intake and save it. Then invite a trainer from the intake page; they receive the whole schedule.
    </CreatorHeader>
    <CreatorError error={error} />
    <OpenClassForm onSave={create} onCancel={onCancel} busy={busy} error={error} onError={setError} />
  </>;
}

function StandaloneCreate() {
  const query = useTrainerQuery(loadCourses);
  const navigate = useNavigate();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);
  const published = (query.data || []).filter((course) => course.status === 'Published');

  async function create(body, courseId) {
    if (busy) return;
    setBusy(true);
    setError(null);
    try {
      const intake = await creatorIntakesApi.openClass(query.token, courseId, body);
      navigate(`/creator/intakes/${intake.id}`, { replace: true });
    } catch (failure) { setError(failure); }
    finally { setBusy(false); }
  }

  return <section className="creator-page">
    <Link className="creator-back" to="/creator/intakes">← All intakes</Link>
    <CreatorHeader eyebrow="Intakes / New" title="Create an intake.">
      Choose a published course and enter every session. Then invite a trainer from the intake page.
    </CreatorHeader>
    {query.loading ? <div className="creator-empty" role="status">Loading courses…</div>
      : query.error ? <CreatorError error={query.error} onRetry={query.refresh} />
        : published.length ? <>
          <CreatorError error={error} />
          <OpenClassForm courses={published} onSave={create} onCancel={() => navigate('/creator/intakes')} busy={busy} error={error} onError={setError} />
        </> : <div className="creator-empty"><strong>No published courses</strong><p>An Admin must approve a course before you create an intake.</p></div>}
    </section>;
}

export default function CreatorIntakeCreatePage({ course, onCancel, onCreated }) {
  const { courseId } = useParams();
  if (course) return <CourseClassForm course={course} onCancel={onCancel} onCreated={onCreated} />;
  if (courseId) return <RoutedCourseClass />;
  return <StandaloneCreate />;
}

function RoutedCourseClass() {
  const { courseId } = useParams();
  const navigate = useNavigate();
  const query = useTrainerQuery(loadCourse, courseId);
  if (query.loading) return <section className="creator-page"><div className="creator-empty" role="status">Loading course…</div></section>;
  if (query.error) return <section className="creator-page"><CreatorError error={query.error} onRetry={query.refresh} /></section>;
  return <section className="creator-page">
    <Link className="creator-back" to={`/creator/courses/${courseId}`}>← Course</Link>
    <CourseClassForm
      course={query.data}
      onCancel={() => navigate(`/creator/courses/${courseId}`)}
      onCreated={(intake) => navigate(`/creator/courses/${courseId}/intakes/${intake.id}`, { replace: true })}
    />
  </section>;
}
