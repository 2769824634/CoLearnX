import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { trainerIntakesApi } from '../../api/trainerIntakes';
import { ScheduleForm } from './IntakeForms';
import { intakeLink } from './intakeForm';
import useTrainerQuery from './useTrainerQuery';
import { TrainerError, TrainerHeader, TrainerLoading } from './TrainerUi';

const loadCourses = (token, queryKey, signal) => trainerIntakesApi.publishedCourses(token, signal);

export default function TrainerIntakeCreatePage() {
  const query = useTrainerQuery(loadCourses);
  const navigate = useNavigate();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);
  async function create(body, courseId) {
    if (busy) return;
    setBusy(true);
    setError(null);
    try {
      const intake = await trainerIntakesApi.create(query.token, courseId, body);
      navigate(intakeLink(intake.id), { replace: true });
    } catch (failure) { setError(failure); }
    finally { setBusy(false); }
  }
  return <section className="trainer-page">
    <Link className="trainer-back" to="/trainer/courses">← All Intakes</Link>
    <TrainerHeader eyebrow="Course / New Intake" title="Start with the schedule.">Choose a published course and set its registration and delivery dates. You can add Sessions after saving.</TrainerHeader>
    {query.loading ? <TrainerLoading /> : query.error ? <TrainerError error={query.error} onRetry={query.refresh} /> : query.data.length ? <div className="trainer-editor-layout"><div>
      <TrainerError error={error} />
      <ScheduleForm courses={query.data} onSave={create} onCancel={() => navigate('/trainer/courses')} busy={busy} error={error} onError={setError} />
    </div><aside className="trainer-side-note"><span className="trainer-eyebrow">01 / Preparation</span><h2>A Draft is your planning space.</h2><p>A Draft can start without Sessions. Before submitting, add a valid Session with an online or physical location.</p><hr /><p>The course is fixed after creation. The course Creator confirms your Intake; submission does not publish it.</p></aside></div> : <div className="trainer-empty"><h2>No published courses available</h2><p>A course needs to be published before you can create an Intake.</p><button type="button" className="btn btn-ghost" onClick={query.refresh}>Refresh courses</button></div>}
  </section>;
}
