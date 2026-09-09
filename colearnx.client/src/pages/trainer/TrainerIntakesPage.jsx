import { useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import useTrainerQuery, { loadTrainerOverview } from './useTrainerQuery';
import { IntakeRows, TrainerError, TrainerHeader, TrainerLoading } from './TrainerUi';
import { intakeStatuses, isEditable, localTimezone, statusLabel } from './intakeForm';

export default function TrainerIntakesPage() {
  const query = useTrainerQuery(loadTrainerOverview);
  const [params, setParams] = useSearchParams();
  const [search, setSearch] = useState('');
  const status = params.get('status') || '';
  const courses = query.data?.courses || [];
  const courseMap = new Map(courses.map((course) => [course.id, course]));
  const term = search.trim().toLowerCase();
  const intakes = (query.data?.intakes || []).filter((intake) => {
    const course = courseMap.get(intake.courseId);
    const matchesStatus = !status || (status === 'editable' ? isEditable(intake.status) : status === 'delivery' ? ['Published', 'InProgress'].includes(intake.status) : intake.status === status);
    return matchesStatus && `${intake.id} ${intake.courseId} ${course?.title || ''} ${course?.code || ''}`.toLowerCase().includes(term);
  });
  return <section className="trainer-page">
    <TrainerHeader eyebrow="Course / My Intakes" title="Make room for your next cohort." action={<Link className="btn btn-primary" to="/trainer/courses/new">+ Create Intake</Link>}>
      Manage your own teaching runs, from the first draft to Creator confirmation.
    </TrainerHeader>
    <div className="trainer-toolbar">
      <div className="form-group"><label htmlFor="intake-search">Find an Intake</label><input id="intake-search" type="search" placeholder="Course title, code or Intake ID" value={search} onChange={(event) => setSearch(event.target.value)} /></div>
      <div className="form-group"><label htmlFor="intake-status">Status</label><select id="intake-status" value={status} onChange={(event) => setParams(event.target.value ? { status: event.target.value } : {})}><option value="">All statuses</option><option value="editable">Draft & rejected</option><option value="delivery">Published & in progress</option>{intakeStatuses.map((value) => <option key={value} value={value}>{statusLabel(value)}</option>)}</select></div>
      <button type="button" className="btn btn-ghost" onClick={query.refresh} disabled={query.loading}>Refresh</button>
    </div>
    {query.loading ? <TrainerLoading /> : query.error ? <TrainerError error={query.error} onRetry={query.refresh} /> : <>
      <TrainerError error={query.data.catalogError} onRetry={query.refresh} retryLabel="Reload course names" />
      <p className="trainer-result-count">{intakes.length} {intakes.length === 1 ? 'Intake' : 'Intakes'} · Times in {localTimezone}</p>
      <IntakeRows intakes={intakes} courses={courses} empty={status || term ? 'No Intakes match these filters.' : undefined} />
    </>}
  </section>;
}
