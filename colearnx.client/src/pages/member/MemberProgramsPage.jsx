import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import MemberShell from '../../components/MemberShell';
import { useMemberData } from './memberDataState';
import { enrollmentsApi } from '../../api';
import useTrainerQuery from '../trainer/useTrainerQuery';

async function loadHub(token, enrollmentId, signal) {
  if (!enrollmentId) return { materials: [], recordings: [] };
  const [materials, recordings] = await Promise.all([
    enrollmentsApi.materials(enrollmentId, token, signal),
    enrollmentsApi.recordings(enrollmentId, token, signal),
  ]);
  return { materials, recordings };
}

// Active / completed enrollments.
export default function MemberProgramsPage() {
  const navigate = useNavigate();
  const { state, showToast } = useMemberData();
  const [tab, setTab] = useState('active');
  const [selectedId, setSelectedId] = useState(null);
  const list = state.enrolled.filter((e) => e.status === tab);
  const hub = list.find((item) => item.enrollmentId === selectedId) || list[0];
  const query = useTrainerQuery(loadHub, hub?.enrollmentId || '');

  async function download(item) {
    try { await enrollmentsApi.downloadMaterial(hub.enrollmentId, item.id, item.title, query.token); }
    catch (error) { showToast(error.message || 'Material download failed'); }
  }

  return (
    <MemberShell
      title="My Programs"
    >
      <div className="tabs">
        <button type="button" className={`tab${tab === 'active' ? ' active' : ''}`} onClick={() => setTab('active')}>
          Active ({state.enrolled.filter((e) => e.status === 'active').length})
        </button>
        <button type="button" className={`tab${tab === 'completed' ? ' active' : ''}`} onClick={() => setTab('completed')}>
          Completed ({state.enrolled.filter((e) => e.status === 'completed').length})
        </button>
      </div>
      <button type="button" className="btn btn-ghost" onClick={() => navigate('/member/disputes')} style={{ marginBottom: 16 }}>My Disputes</button>

      <div className="grid-2-1">
        <div>
          {list.length === 0 ? (
            <div className="callout">
              No {tab} programs yet.{' '}
              <button type="button" className="btn btn-ghost" onClick={() => navigate('/member/courses')}>Browse catalog</button>
            </div>
          ) : (
            list.map((e) => (
              <button type="button" className="card" key={e.enrollmentId || e.id} onClick={() => setSelectedId(e.enrollmentId)} style={{ marginBottom: 8, width: '100%', textAlign: 'left', borderColor: hub?.enrollmentId === e.enrollmentId ? 'var(--purple)' : undefined }}>
                <div className="card-body">
                  <strong>{e.courseCode} — {e.courseTitle}</strong>
                  <div style={{ fontSize: 12, color: 'var(--slate)' }}>Trainer: {e.trainer}</div>
                  <div style={{ marginTop: 8 }}>
                    <strong style={{ color: 'var(--purple)' }}>{e.progress}%</strong>
                    <div className="progress-bar" style={{ width: 120, marginTop: 4 }}>
                      <div className="fill" style={{ width: `${e.progress}%` }} />
                    </div>
                  </div>
                </div>
              </button>
            ))
          )}
        </div>
        <div className="card">
          <div className="card-header">{hub ? `${hub.courseCode} — Learning Hub` : 'Learning Hub'}</div>
          <div className="card-body">
            <p style={{ fontSize: 12, color: 'var(--slate)' }}>
              {hub ? `Session materials for ${hub.courseTitle}` : 'Select a program'}
            </p>
            {hub ? <button type="button" className="btn btn-ghost" onClick={query.refresh}>Refresh resources</button> : null}
            {query.loading && hub ? <p role="status">Loading resources…</p> : null}
            {query.error && hub ? <p role="alert">{query.error.message}</p> : null}
            {query.data && hub ? <>
              <h3>Materials</h3>
              {query.data.materials.length ? query.data.materials.map((item) => <button key={item.id} type="button" className="btn btn-ghost btn-block" onClick={() => download(item)}>{item.title} · {item.format}</button>) : <p>No materials attached yet.</p>}
              <h3>Recordings</h3>
              {query.data.recordings.length ? query.data.recordings.map((item) => <a key={item.id} className="btn btn-ghost btn-block" href={item.recordingUrl} target="_blank" rel="noopener noreferrer">{item.title}</a>) : <p>No recordings added yet.</p>}
              {hub.meetingLink ? <a className="btn btn-primary btn-block" href={hub.meetingLink} target="_blank" rel="noopener noreferrer">Join Live Session</a> : null}
            </> : null}
            {hub ? <button type="button" className="btn btn-ghost btn-block" onClick={() => navigate(`/member/disputes?enrollmentId=${hub.enrollmentId}`)}>View or submit a dispute</button> : null}
          </div>
        </div>
      </div>
    </MemberShell>
  );
}
