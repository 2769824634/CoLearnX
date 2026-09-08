import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import MemberShell from '../../components/MemberShell';
import { useMemberData } from './MemberDataContext';

// Active / completed enrollments.
export default function MemberProgramsPage() {
  const navigate = useNavigate();
  const { state, showToast } = useMemberData();
  const [tab, setTab] = useState('active');
  const list = state.enrolled.filter((e) => e.status === tab);
  const hub = list[0] || state.enrolled[0];

  return (
    <MemberShell
      title="My Programs"
      onNotify={() => showToast('No new notifications')}
    >
      <div className="tabs">
        <button type="button" className={`tab${tab === 'active' ? ' active' : ''}`} onClick={() => setTab('active')}>
          Active ({state.enrolled.filter((e) => e.status === 'active').length})
        </button>
        <button type="button" className={`tab${tab === 'completed' ? ' active' : ''}`} onClick={() => setTab('completed')}>
          Completed ({state.enrolled.filter((e) => e.status === 'completed').length})
        </button>
      </div>

      <div className="grid-2-1">
        <div>
          {list.length === 0 ? (
            <div className="callout">
              No {tab} programs yet.{' '}
              <span className="link" onClick={() => navigate('/member/courses')}>Browse catalog</span>
            </div>
          ) : (
            list.map((e) => (
              <div className="card" key={e.enrollmentId || e.id} style={{ marginBottom: 8 }}>
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
              </div>
            ))
          )}
        </div>
        <div className="card">
          <div className="card-header">{hub ? `${hub.courseCode} — Learning Hub` : 'Learning Hub'}</div>
          <div className="card-body">
            <p style={{ fontSize: 12, color: 'var(--slate)' }}>
              {hub ? `Session materials for ${hub.courseTitle}` : 'Select a program'}
            </p>
            <button type="button" className="btn btn-primary btn-block" style={{ marginTop: 16 }} onClick={() => showToast('Joining live session...')}>
              Join Live Session
            </button>
          </div>
        </div>
      </div>
    </MemberShell>
  );
}
