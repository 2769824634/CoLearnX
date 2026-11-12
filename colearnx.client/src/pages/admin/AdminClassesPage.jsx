import { useMemo, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { adminOverviewApi } from '../../api';
import Modal from '../../components/Modal';
import AdminClassDetail from './AdminClassDetail';
import AdminClassTable from './AdminClassTable';
import AdminDataState from './AdminDataState';
import {
  CLASS_STATUS_FILTERS, LOW_ATTENDANCE, filterClasses, isLowAttendance, peopleOptions, sortEndingSoon,
} from './adminClasses';
import useAdminQuery from './useAdminQuery';

const loadClasses = (token, _queryKey, signal) => adminOverviewApi.classes(token, signal);

export default function AdminClassesPage() {
  const query = useAdminQuery(loadClasses);
  const [params, setParams] = useSearchParams();
  const selectedId = /^[1-9]\d*$/.test(params.get('classId') || '') ? Number(params.get('classId')) : null;
  const [filters, setFilters] = useState({ status: '', trainerId: '', creatorId: '', sort: 'start' });
  const rows = useMemo(() => query.data || [], [query.data]);
  const trainers = useMemo(() => peopleOptions(rows, 'trainerId', 'trainerName'), [rows]);
  const creators = useMemo(() => peopleOptions(rows, 'creatorId', 'creatorName'), [rows]);
  const shown = useMemo(() => {
    const filtered = filterClasses(rows, filters);
    return filters.sort === 'ending' ? sortEndingSoon(filtered) : filtered;
  }, [rows, filters]);
  const running = rows.filter((row) => row.status === 'InProgress').length;
  const open = rows.filter((row) => row.status === 'Published').length;
  const low = rows.filter((row) => isLowAttendance(row.attendanceRate) && row.status !== 'Cancelled').length;
  const update = (key) => (event) => setFilters({ ...filters, [key]: event.target.value });
  const openClass = (id) => setParams(id ? { classId: String(id) } : {});

  return <section className="admin-review-page later-page">
    <header className="admin-review-header"><div><p className="admin-form-eyebrow">Class overview</p><h1>Classes</h1>
      <p>Every class on the platform in one table. Open a class to see its sessions, learners, materials and certificates.</p></div>
      <button className="btn btn-ghost" type="button" onClick={query.refresh} disabled={query.loading}>Refresh</button>
    </header>
    <AdminDataState loading={query.loading} error={query.error} onRetry={query.refresh} />
    {query.data ? <>
      <dl className="admin-summary-stats">
        <div><dt>All classes</dt><dd>{rows.length}</dd></div>
        <div><dt>In progress</dt><dd>{running}</dd></div>
        <div><dt>Open for registration</dt><dd>{open}</dd></div>
        <div><dt>Attendance below {Math.round(LOW_ATTENDANCE * 100)}%</dt><dd className={low ? 'later-negative' : undefined}>{low}</dd></div>
      </dl>
      <form className="admin-audit-filters admin-class-filters" onSubmit={(event) => event.preventDefault()}>
        <label>Status<select value={filters.status} onChange={update('status')}>
          {CLASS_STATUS_FILTERS.map((item) => <option key={item.value} value={item.value}>{item.label}</option>)}
        </select></label>
        <label>Trainer<select value={filters.trainerId} onChange={update('trainerId')}>
          <option value="">All Trainers</option>
          {trainers.map((person) => <option key={person.id} value={person.id}>{person.name}</option>)}
        </select></label>
        <label>Creator<select value={filters.creatorId} onChange={update('creatorId')}>
          <option value="">All Creators</option>
          {creators.map((person) => <option key={person.id} value={person.id}>{person.name}</option>)}
        </select></label>
        <label>Sort by<select value={filters.sort} onChange={update('sort')}>
          <option value="start">Latest start date</option>
          <option value="ending">Ending soonest</option>
        </select></label>
      </form>
      <p className="admin-class-hint">Rows in red have attendance below {Math.round(LOW_ATTENDANCE * 100)}%.</p>
      <AdminClassTable rows={shown} onOpen={openClass} emptyText={rows.length ? 'No classes match these filters.' : 'No classes yet.'} />
    </> : null}
    <Modal open={selectedId !== null} title="Class details" onClose={() => openClass(null)} width={1100}>
      {selectedId !== null ? <AdminClassDetail key={selectedId} classId={selectedId} onChanged={query.refresh} /> : null}
    </Modal>
  </section>;
}
