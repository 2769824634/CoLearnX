import { useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { creatorCoursesApi } from '../../api';
import { creatorIntakesApi } from '../../api/creatorIntakes';
import { courseTrainersApi } from '../../api/courseTrainers';
import OpenClassForm from './OpenClassForm';
import useTrainerQuery from '../trainer/useTrainerQuery';
import { CreatorError, CreatorHeader } from './CreatorUi';

const loadCourses = (token, key, signal) => creatorCoursesApi.list(token, signal);
const loadCourse = (token, courseId, signal) => creatorCoursesApi.get(token, courseId, signal);
const loadAccepted = (token, courseId, signal) => courseTrainersApi.list(token, courseId, signal);

function CourseClassForm({ course, onCancel, onCreated }) {
  const query = useTrainerQuery(loadAccepted, String(course.id));
  const trainers = (Array.isArray(query.data) ? query.data : []).filter((trainer) => trainer.status === 'Accepted');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);

  async function create(body) {
    if (busy) return;
    setBusy(true);
    setError(null);
    try {
      const intake = await creatorIntakesApi.openClass(query.token, course.id, body);
      onCreated(intake);
    } catch (failure) { setError(failure); }
    finally { setBusy(false); }
  }

  if (course.status !== 'Published') {
    return <div className="creator-empty"><strong>Publish this course first</strong><p>Publish a course before opening a class.</p></div>;
  }
  return <>
    <CreatorHeader eyebrow="Classes / New" title="Open a class.">
      Say when the class meets and who teaches it. Every session is created for you. Publish the class now, or save it as a draft.
    </CreatorHeader>
    {query.loading ? <div className="creator-empty" role="status">Loading trainers…</div>
      : query.error ? <CreatorError error={query.error} onRetry={query.refresh} />
        : trainers.length ? <>
          <CreatorError error={error} />
          <OpenClassForm trainers={trainers} onSave={create} onCancel={onCancel} busy={busy} error={error} onError={setError} />
        </> : <div className="creator-empty"><strong>No accepted Trainer yet</strong><p>Invite a Trainer and wait until they accept this course.</p></div>}
  </>;
}

function StandaloneCreate() {
  const query = useTrainerQuery(loadCourses);
  const navigate = useNavigate();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);
  const [trainers, setTrainers] = useState([]);
  const published = (query.data || []).filter((course) => course.status === 'Published');

  async function onCourseChange(courseId) {
    setTrainers([]);
    if (!courseId) return;
    try {
      const roster = await courseTrainersApi.list(query.token, courseId);
      setTrainers(roster.filter((trainer) => trainer.status === 'Accepted'));
    } catch (failure) { setError(failure); }
  }

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
    <Link className="creator-back" to="/creator/intakes">← All classes</Link>
    <CreatorHeader eyebrow="Classes / New" title="Open a class.">
      Choose a published course, say when the class meets and who teaches it. Every session is created for you.
    </CreatorHeader>
    {query.loading ? <div className="creator-empty" role="status">Loading courses…</div>
      : query.error ? <CreatorError error={query.error} onRetry={query.refresh} />
        : published.length ? <>
          <CreatorError error={error} />
          <OpenClassForm courses={published} trainers={trainers} onCourseChange={onCourseChange} onSave={create} onCancel={() => navigate('/creator/intakes')} busy={busy} error={error} onError={setError} />
        </> : <div className="creator-empty"><strong>No published courses</strong><p>Publish a course before opening a class.</p></div>}
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
