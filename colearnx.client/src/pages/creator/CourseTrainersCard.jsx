import { useState } from 'react';
import { courseTrainersApi } from '../../api/courseTrainers';
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

// Who has taught or is teaching this Course. Trainers join it by accepting an Intake; they are invited from the Intake.
export default function CourseTrainersCard({ courseId }) {
  const query = useTrainerQuery(loadRoster, String(courseId));
  const roster = Array.isArray(query.data) ? query.data : [];
  const [busyId, setBusyId] = useState(null);
  const [error, setError] = useState(null);
  const [removing, setRemoving] = useState(null);

  async function remove(trainerId) {
    setBusyId(trainerId);
    setError(null);
    try {
      await courseTrainersApi.remove(query.token, courseId, trainerId);
      query.refresh();
    } catch (requestError) {
      setError(requestError);
    } finally {
      setBusyId(null);
    }
  }

  return <div className="card" id="course-trainers" style={{ marginTop: 16 }}>
    <div className="card-header"><span>Trainers</span></div>
    <div className="card-body">
      <p className="page-sub" style={{ marginTop: 0 }}>Trainers join this Course when they accept an Intake.</p>
      <CreatorError error={error} />
      {query.loading ? <div className="creator-empty" role="status">Loading trainers…</div>
        : query.error ? <CreatorError error={query.error} onRetry={query.refresh} />
          : !roster.length ? <div className="creator-empty">No trainers yet. Invite one from an Intake.</div>
            : <ul className="later-resource-list">{roster.map((row) => <li key={row.trainerId}>
              <div><strong>{row.fullName}</strong><small>{row.headline || 'No headline yet'} · {rowNote(row)}</small></div>
              <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                <StatusPill status={row.status} />
                {/* An invitation sent before Intake invitations existed can still be withdrawn. */}
                {row.status === 'Invited' ? <button type="button" className="btn btn-ghost btn-sm" disabled={busyId === row.trainerId} onClick={() => remove(row.trainerId)}>Withdraw</button> : null}
                {row.status === 'Accepted' ? <button type="button" className="btn btn-ghost btn-sm" disabled={busyId === row.trainerId} onClick={() => setRemoving(row)}>Remove</button> : null}
              </div>
            </li>)}</ul>}
    </div>
    <Modal open={Boolean(removing)} title={removing ? `Remove ${removing.fullName}?` : ''} onClose={() => setRemoving(null)}>
      <p style={{ marginTop: 0 }}>They leave this Course's list. Intakes they already teach are not changed, and they rejoin if they accept another Intake.</p>
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
