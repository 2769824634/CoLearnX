import { useState } from 'react';
import { materialsApi } from '../../api';
import useTrainerQuery from '../trainer/useTrainerQuery';
import { CreatorError, CreatorHeader } from './CreatorUi';

const loadUsage = (token, _key, signal) => materialsApi.usage(token, signal);

export default function CreatorUsagePage() {
  const query = useTrainerQuery(loadUsage);
  const [courseId, setCourseId] = useState('');
  const [trainerId, setTrainerId] = useState('');
  const [search, setSearch] = useState('');
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');
  const records = query.data || [];
  const courses = [...new Map(records.map((item) => [item.courseId, item])).values()];
  const trainers = [...new Map(records.map((item) => [item.trainerId, item])).values()];
  const visible = records.filter((item) =>
    (!courseId || String(item.courseId) === courseId)
    && (!trainerId || String(item.trainerId) === trainerId)
    && (!search || item.materialTitle.toLowerCase().includes(search.toLowerCase()))
    && (!from || item.usedAt.slice(0, 10) >= from)
    && (!to || item.usedAt.slice(0, 10) <= to));

  return <section className="creator-page">
    <CreatorHeader eyebrow="Material activity" title="Usage Records" action={<button type="button" className="btn btn-ghost" onClick={query.refresh}>Refresh</button>}>
      Each record is created when a Trainer attaches one of your approved materials to a Course Intake. It is not a download or royalty payment.
    </CreatorHeader>
    {query.loading ? <div className="creator-empty" role="status">Loading usage records…</div>
      : query.error ? <CreatorError error={query.error} onRetry={query.refresh} />
        : <>
          <div className="card">
            <div className="card-header">Filter usage</div>
            <div className="card-body">
              <div className="form-group"><label htmlFor="usage-course">Course</label><select id="usage-course" value={courseId} onChange={(event) => setCourseId(event.target.value)}><option value="">All courses</option>{courses.map((item) => <option key={item.courseId} value={item.courseId}>{item.courseCode} — {item.courseTitle}</option>)}</select></div>
              <div className="form-group"><label htmlFor="usage-trainer">Trainer</label><select id="usage-trainer" value={trainerId} onChange={(event) => setTrainerId(event.target.value)}><option value="">All trainers</option>{trainers.map((item) => <option key={item.trainerId} value={item.trainerId}>{item.trainerName}</option>)}</select></div>
              <div className="form-group"><label htmlFor="usage-material">Material</label><input id="usage-material" value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Search material title" /></div>
              <div className="form-group"><label htmlFor="usage-from">From date (UTC)</label><input id="usage-from" type="date" value={from} onChange={(event) => setFrom(event.target.value)} /></div>
              <div className="form-group"><label htmlFor="usage-to">To date (UTC)</label><input id="usage-to" type="date" value={to} onChange={(event) => setTo(event.target.value)} /></div>
            </div>
          </div>
          <div className="creator-section-heading"><div><p className="creator-eyebrow">Your materials</p><h2>{visible.length} recorded {visible.length === 1 ? 'use' : 'uses'}</h2><p>{new Set(visible.map((item) => item.materialId)).size} materials used across {new Set(visible.map((item) => item.courseId)).size} courses</p></div></div>
          {visible.length === 0 ? <div className="creator-empty"><strong>No usage records found</strong><p>Usage appears after a Trainer attaches an approved material to an Intake.</p></div>
            : <div className="creator-application-list">{visible.map((item) => <article className="creator-application-row" key={item.id}>
              <div className="creator-application-index">{item.courseCode}</div>
              <div><p className="creator-eyebrow">{item.courseTitle}</p><h2>{item.materialTitle}</h2><p>Trainer: {item.trainerName}</p></div>
              <time dateTime={item.usedAt}>{new Date(item.usedAt).toLocaleString()}</time>
            </article>)}</div>}
        </>}
  </section>;
}
