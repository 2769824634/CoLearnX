import { useEffect, useRef, useState } from 'react';
import { courseTrainersApi } from '../../api/courseTrainers';
import { useAuth } from '../../auth/AuthContext';
import Modal from '../../components/Modal';
import { formatUtcDateTime } from '../businessPresentation';
import useTrainerQuery from '../trainer/useTrainerQuery';
import { CreatorError } from './CreatorUi';

const STATUS = {
  Accepted: { label: 'Available', tone: 'confirmed' },
  Invited: { label: 'Invited', tone: 'pending' },
  Declined: { label: 'Declined', tone: 'rejected' },
  Removed: { label: 'Removed', tone: 'rejected' },
};
const MIN_SEARCH = 2;
const SEARCH_DELAY_MS = 300;

const loadRoster = (token, courseId, signal) => courseTrainersApi.list(token, courseId, signal);

function StatusPill({ status }) {
  const { label, tone } = STATUS[status] || { label: status, tone: '' };
  return <span className={`creator-status ${tone}`}>{label}</span>;
}

function rowNote(row) {
  if (row.status === 'Removed') return `Removed ${formatUtcDateTime(row.removedAt)}`;
  if (row.status === 'Invited') return `Invited ${formatUtcDateTime(row.invitedAt)}`;
  return `${row.status === 'Accepted' ? 'Joined' : 'Declined'} ${formatUtcDateTime(row.respondedAt || row.invitedAt)}`;
}

export default function CourseTrainersCard({ courseId }) {
  const query = useTrainerQuery(loadRoster, String(courseId));
  const roster = Array.isArray(query.data) ? query.data : [];
  const [busyId, setBusyId] = useState(null);
  const [error, setError] = useState(null);
  const [inviting, setInviting] = useState(false);
  const [removing, setRemoving] = useState(null);

  async function run(trainerId, action) {
    setBusyId(trainerId);
    setError(null);
    try {
      const result = await action();
      query.refresh();
      return result;
    } catch (requestError) {
      setError(requestError);
      return null;
    } finally {
      setBusyId(null);
    }
  }

  const invite = (trainerId) => run(trainerId, () => courseTrainersApi.invite(query.token, courseId, trainerId));
  const remove = (trainerId) => run(trainerId, () => courseTrainersApi.remove(query.token, courseId, trainerId));

  return <div className="card" style={{ marginTop: 16 }}>
    <div className="card-header" style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 12 }}>
      <span>Trainers</span>
      <button type="button" className="btn btn-primary btn-sm" onClick={() => setInviting(true)}>Invite trainer</button>
    </div>
    <div className="card-body">
      <p className="page-sub" style={{ marginTop: 0 }}>Invite Trainers to teach this Course. Trainers who accept become available when you schedule an Intake.</p>
      <CreatorError error={error} />
      {query.loading ? <div className="creator-empty" role="status">Loading trainers…</div>
        : query.error ? <CreatorError error={query.error} onRetry={query.refresh} />
          : !roster.length ? <div className="creator-empty">No trainers yet. Invite one to teach this course.</div>
            : <ul className="later-resource-list">{roster.map((row) => <li key={row.trainerId}>
              <div><strong>{row.fullName}</strong><small>{row.headline || 'No headline yet'} · {rowNote(row)}</small></div>
              <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                <StatusPill status={row.status} />
                {row.status === 'Invited' ? <button type="button" className="btn btn-ghost btn-sm" disabled={busyId === row.trainerId} onClick={() => remove(row.trainerId)}>Withdraw</button> : null}
                {row.status === 'Accepted' ? <button type="button" className="btn btn-ghost btn-sm" disabled={busyId === row.trainerId} onClick={() => setRemoving(row)}>Remove</button> : null}
                {row.status === 'Declined' || row.status === 'Removed' ? <button type="button" className="btn btn-ghost btn-sm" disabled={busyId === row.trainerId} onClick={() => invite(row.trainerId)}>Invite again</button> : null}
              </div>
            </li>)}</ul>}
    </div>
    {inviting ? <InviteDialog roster={roster} onInvite={invite} busyId={busyId} onClose={() => setInviting(false)} /> : null}
    <Modal open={Boolean(removing)} title={removing ? `Remove ${removing.fullName}?` : ''} onClose={() => setRemoving(null)}>
      <p style={{ marginTop: 0 }}>They will no longer be available for new Intakes of this Course. Intakes they already teach are not changed.</p>
      <div className="trainer-actions">
        <button type="button" className="btn btn-ghost" onClick={() => setRemoving(null)}>Cancel</button>
        <button type="button" className="btn btn-primary" disabled={busyId === removing?.trainerId} onClick={async () => {
          const trainerId = removing.trainerId;
          setRemoving(null);
          await remove(trainerId);
        }}>Remove trainer</button>
      </div>
    </Modal>
  </div>;
}

function InviteDialog({ roster, onInvite, busyId, onClose }) {
  const { token } = useAuth();
  const [term, setTerm] = useState('');
  const [results, setResults] = useState(null);
  const [searchError, setSearchError] = useState(null);
  const [sent, setSent] = useState('');
  const timer = useRef(null);
  const pending = useRef(null);
  const statusById = new Map(roster.map((row) => [row.trainerId, row.status]));

  useEffect(() => () => {
    clearTimeout(timer.current);
    pending.current?.abort();
  }, []);

  function search(value) {
    pending.current?.abort();
    const controller = new AbortController();
    pending.current = controller;
    setSearchError(null);
    courseTrainersApi.searchTrainers(token, value, controller.signal).then(
      (items) => { if (!controller.signal.aborted) setResults(Array.isArray(items) ? items : []); },
      (requestError) => { if (!controller.signal.aborted) setSearchError(requestError); },
    );
  }

  function onChange(event) {
    const value = event.target.value;
    setTerm(value);
    setSent('');
    clearTimeout(timer.current);
    pending.current?.abort();
    if (value.trim().length < MIN_SEARCH) {
      setResults(null);
      return;
    }
    timer.current = setTimeout(() => search(value.trim()), SEARCH_DELAY_MS);
  }

  async function invite(candidate) {
    setSent('');
    const result = await onInvite(candidate.id);
    if (result) setSent(result.status === 'Accepted' ? `${candidate.fullName} was added to this Course.` : `Invitation sent to ${candidate.fullName}.`);
  }

  return <Modal open title="Invite a trainer" onClose={onClose}>
    <div className="form-group">
      <label htmlFor="course-trainer-search">Search trainers by name</label>
      <input id="course-trainer-search" type="search" autoComplete="off" value={term} onChange={onChange} placeholder={`Type at least ${MIN_SEARCH} letters`} data-modal-autofocus />
    </div>
    {sent ? <p role="status" style={{ color: 'var(--teal)' }}>{sent}</p> : null}
    <CreatorError error={searchError} />
    {results === null ? null : !results.length ? <p className="page-sub">No trainers match “{term.trim()}”.</p>
      : <ul className="later-resource-list">{results.map((candidate) => {
        const status = statusById.get(candidate.id);
        const onRoster = status === 'Invited' || status === 'Accepted';
        return <li key={candidate.id}>
          <div><strong>{candidate.fullName}</strong><small>{[candidate.headline, candidate.specialisations].filter(Boolean).join(' · ') || 'No profile details yet'}</small></div>
          {onRoster ? <StatusPill status={status} />
            : <button type="button" className="btn btn-primary btn-sm" disabled={busyId === candidate.id} onClick={() => invite(candidate)}>Invite</button>}
        </li>;
      })}</ul>}
  </Modal>;
}
