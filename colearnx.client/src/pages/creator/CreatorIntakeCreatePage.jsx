import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { creatorCoursesApi } from '../../api';
import { creatorIntakesApi } from '../../api/creatorIntakes';
import { courseTrainersApi } from '../../api/courseTrainers';
import { ScheduleForm } from '../trainer/IntakeForms';
import useTrainerQuery from '../trainer/useTrainerQuery';
import { CreatorError, CreatorHeader } from './CreatorUi';

const loadCourses = (token, key, signal) => creatorCoursesApi.list(token, signal);

export default function CreatorIntakeCreatePage() {
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
      const intake = await creatorIntakesApi.create(query.token, courseId, body);
      navigate(`/creator/intakes/${intake.id}`, { replace: true });
    } catch (failure) { setError(failure); }
    finally { setBusy(false); }
  }

  return <section className="creator-page">
    <Link className="creator-back" to="/creator/intakes">← All classes</Link>
    <CreatorHeader eyebrow="Classes / New" title="Open a class.">
      Choose a published course and a Trainer who has already accepted it. Add sessions after the draft is saved, then publish.
    </CreatorHeader>
    {query.loading ? <div className="creator-empty" role="status">Loading courses…</div>
      : query.error ? <CreatorError error={query.error} onRetry={query.refresh} />
        : published.length ? <>
          <CreatorError error={error} />
          <ScheduleForm courses={published} trainers={trainers} onCourseChange={onCourseChange} onSave={create} onCancel={() => navigate('/creator/intakes')} busy={busy} error={error} onError={setError} />
        </> : <div className="creator-empty"><strong>No published courses</strong><p>Publish a course before opening a class.</p></div>}
  </section>;
}
