import { useEffect, useState } from 'react';
import { adminCourseReviewsApi } from '../../api';
import TeachingDatesEditor from '../account/TeachingDatesEditor';
import { statusLabel } from '../trainer/intakeForm';

export default function AdminCourseSchedule({ token, courseId }) {
  const [classes, setClasses] = useState(null);
  const [error, setError] = useState('');
  const [revision, setRevision] = useState(0);
  useEffect(() => {
    const controller = new AbortController();
    adminCourseReviewsApi.schedule(token, courseId, controller.signal).then(setClasses)
      .catch((failure) => { if (failure.name !== 'AbortError') setError(failure.message); });
    return () => controller.abort();
  }, [token, courseId, revision]);
  function update(updated) {
    setClasses((items) => items.map((item) => item.id === updated.id ? updated : item));
  }
  return <>
    {error ? <div role="alert" className="callout warn">{error}<button type="button" className="btn btn-ghost btn-sm" onClick={() => { setError(''); setRevision((value) => value + 1); }}>Try again</button></div> : null}
    {!classes && !error ? <p role="status">Loading class dates…</p> : null}
    {classes?.length === 0 ? <p>No classes have been opened for this course.</p> : null}
    {classes?.map((intake) => <article className="card" key={intake.id}>
      <div className="card-header">Class #{intake.id} · {statusLabel(intake.status)}</div>
      <div className="card-body"><TeachingDatesEditor intake={intake}
        save={(body) => adminCourseReviewsApi.updateTeachingDates(token, courseId, intake.id, body)}
        reload={async () => {
          const items = await adminCourseReviewsApi.schedule(token, courseId);
          const updated = items.find((item) => item.id === intake.id);
          if (!updated) throw new Error('This class is no longer available. Reopen course dates.');
          return updated;
        }} onSaved={update} /></div>
    </article>)}
  </>;
}
