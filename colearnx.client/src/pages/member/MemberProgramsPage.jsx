import { useEffect, useState } from 'react';
import { useLocation, useNavigate, useSearchParams } from 'react-router-dom';
import MemberShell from '../../components/MemberShell';
import Modal from '../../components/Modal';
import { useMemberData } from './memberDataState';
import { enrollmentsApi } from '../../api';
import useTrainerQuery from '../trainer/useTrainerQuery';
import { formatCount, formatUtcDateTime, userFacingError } from '../businessPresentation';

const tabForEnrollmentStatus = (status) => ({
  reserved: 'reserved', active: 'active', completed: 'completed', cancelled: 'history', refunded: 'history',
}[status] || null);

async function loadHub(token, enrollmentId, signal) {
  if (!enrollmentId) return { materials: [], sessionMaterials: [], recordings: [] };
  const [materials, sessionMaterials, recordings] = await Promise.all([
    enrollmentsApi.materials(enrollmentId, token, signal),
    enrollmentsApi.sessionMaterials(enrollmentId, token, signal),
    enrollmentsApi.recordings(enrollmentId, token, signal),
  ]);
  return { materials, sessionMaterials, recordings };
}

function sessionMaterialDownloadName(item) {
  const extension = String(item?.format || 'bin').replace(/^\./, '').toLowerCase().replace('jpeg', 'jpg');
  return `${item?.title || 'session-material'}.${extension}`;
}

function PostponementPicker({ enrollment, busy, onReserve }) {
  const options = enrollment.postponementOptions || [];
  const [sessionId, setSessionId] = useState('');
  const selected = options.find((option) => String(option.courseSessionId) === sessionId) || options[0];
  if (!selected) return null;
  return <section aria-label="Replacement Intake">
    <h3>Replacement class available</h3>
    <div className="form-group"><label htmlFor="replacement-session">Replacement Session</label>
      <select id="replacement-session" value={selected.courseSessionId} disabled={busy} onChange={(event) => setSessionId(event.target.value)}>
        {options.map((option) => <option key={option.courseSessionId} value={option.courseSessionId}>{option.label} - {formatUtcDateTime(option.startsAt)}{option.seatsLeft === 0 ? ' (Full)' : ''}</option>)}
      </select>
    </div>
    <p>Intake #{selected.intakeId} · {formatUtcDateTime(selected.startsAt)} to {formatUtcDateTime(selected.endsAt)}</p>
    <p>Registration closes {formatUtcDateTime(selected.registrationClosesAt)}. {selected.seatsLeft == null ? 'Online places available.' : `${formatCount(selected.seatsLeft, 'place')} left.`}</p>
    <p>A new reservation holds {formatCount(selected.creditsRequired, 'credit')} from your available balance.</p>
    <button type="button" className="btn btn-primary" disabled={busy || selected.seatsLeft === 0} onClick={() => onReserve({ enrollmentId: enrollment.enrollmentId, action: 'postpone', ...selected })}>Reserve replacement place</button>
  </section>;
}

// Active / completed enrollments.
export default function MemberProgramsPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const [params, setParams] = useSearchParams();
  const { state, showToast, changeEnrollment, acceptPostponement, reload } = useMemberData();
  const requestedTab = ['active', 'reserved', 'completed', 'history'].includes(params.get('tab')) ? params.get('tab') : 'active';
  const selectedId = Number.parseInt(params.get('enrollmentId') || '', 10) || null;
  const [refreshFor, setRefreshFor] = useState(null);
  const [pendingAction, setPendingAction] = useState(null);
  const [busy, setBusy] = useState(false);
  const requestedEnrollment = selectedId ? state.enrolled.find((item) => item.enrollmentId === selectedId) : null;
  const requestedEnrollmentTab = tabForEnrollmentStatus(requestedEnrollment?.status);
  const tab = requestedEnrollmentTab || requestedTab;
  const selectionMissing = Boolean(selectedId && !requestedEnrollment);
  const selectionMoved = Boolean(requestedEnrollment && requestedEnrollmentTab && requestedEnrollmentTab !== requestedTab);
  const list = state.enrolled.filter((e) => tab === 'history'
    ? ['cancelled', 'refunded'].includes(e.status) : e.status === tab);
  const courseId = Number.parseInt(params.get('courseId') || '', 10) || null;
  const hub = requestedEnrollment || (!selectedId && (list.find((item) => item.id === courseId) || list[0]));
  const selectionNotice = selectionMoved || (selectedId && requestedEnrollment
    && location.state?.programSelectionMoved?.enrollmentId === selectedId
    && location.state?.programSelectionMoved?.tab === tab)
    ? `This program is now in the ${tab} tab, so its current enrollment is shown here.`
    : '';
  useEffect(() => {
    if (!selectionMoved) return;
    const next = new URLSearchParams(params);
    next.set('tab', tab);
    setParams(next, { replace: true, state: {
      ...location.state, programSelectionMoved: { enrollmentId: selectedId, tab },
    } });
  }, [location.state, params, selectedId, selectionMoved, setParams, tab]);
  const query = useTrainerQuery(loadHub, ['active', 'completed'].includes(tab) ? hub?.enrollmentId || '' : '');

  function setTab(nextTab) {
    const next = new URLSearchParams(params);
    next.set('tab', nextTab); next.delete('enrollmentId'); next.delete('courseId');
    setParams(next); setRefreshFor(null);
  }

  function selectEnrollment(enrollment) {
    const next = new URLSearchParams(params);
    next.set('tab', tab); next.set('enrollmentId', enrollment.enrollmentId); next.set('courseId', enrollment.id);
    setParams(next); setRefreshFor(null);
  }

  async function confirmChange() {
    if (!pendingAction || busy) return;
    setBusy(true);
    try {
      if (pendingAction.action === 'postpone') {
        await acceptPostponement(pendingAction.enrollmentId, pendingAction.courseSessionId);
        showToast('Replacement place reserved; credits are on hold again.');
        setTab('reserved');
      } else {
        await changeEnrollment(pendingAction.enrollmentId, pendingAction.action);
        showToast(pendingAction.action === 'cancel' ? 'Reservation cancelled; credits released.' : 'Withdrawal processed.');
      }
      setPendingAction(null);
    } catch (error) { showToast(userFacingError(error, 'Could not update enrollment. Check its current status before retrying.')); }
    finally { setBusy(false); }
  }

  async function download(item) {
    try { await enrollmentsApi.downloadMaterial(hub.enrollmentId, item.id, item.title, query.token); }
    catch (error) { showToast(userFacingError(error, 'Could not download this material. Please try again.')); }
  }

  async function downloadSessionMaterial(item) {
    try {
      await enrollmentsApi.downloadSessionMaterial(hub.enrollmentId, item.id, sessionMaterialDownloadName(item), query.token);
    }
    catch (error) { showToast(userFacingError(error, 'Could not download this Session material. Please try again.')); }
  }

  const sessionMaterialGroups = (query.data?.sessionMaterials || []).reduce((groups, item) => {
    const key = String(item.courseSessionId);
    const existing = groups.find((group) => group.key === key);
    if (existing) existing.items.push(item);
    else groups.push({ key, label: item.sessionLabel || `Session ${item.courseSessionId}`, items: [item] });
    return groups;
  }, []);

  return (
    <MemberShell
      title="My Programs"
    >
      {state.loading ? <p role="status">Loading your programs…</p> : state.loadError ? <div role="alert" className="callout warn">{state.loadError} <button type="button" className="btn btn-ghost" onClick={reload}>Retry</button></div> : null}
      {!state.loading && !state.loadError ? <>
      <div className="tabs">
        <button type="button" className={`tab${tab === 'reserved' ? ' active' : ''}`} onClick={() => setTab('reserved')}>
          Reserved ({state.enrolled.filter((e) => e.status === 'reserved').length})
        </button>
        <button type="button" className={`tab${tab === 'active' ? ' active' : ''}`} onClick={() => setTab('active')}>
          Active ({state.enrolled.filter((e) => e.status === 'active').length})
        </button>
        <button type="button" className={`tab${tab === 'completed' ? ' active' : ''}`} onClick={() => setTab('completed')}>
          Completed ({state.enrolled.filter((e) => e.status === 'completed').length})
        </button>
        <button type="button" className={`tab${tab === 'history' ? ' active' : ''}`} onClick={() => setTab('history')}>History</button>
      </div>
      <button type="button" className="btn btn-ghost" onClick={() => navigate('/member/disputes')} style={{ marginBottom: 16 }}>My Disputes</button>

      {selectionNotice ? <div className="callout" role="status">{selectionNotice}</div> : null}
      {selectionMissing ? <div className="callout warn" role="status">The requested program is not in your current records. Select a program below to continue.</div> : null}

      <div className="grid-2-1">
        <div>
          {list.length === 0 ? (
            <div className="callout">
              No {tab} programs yet.{' '}
              <button type="button" className="btn btn-ghost" onClick={() => navigate('/member/courses')}>Browse catalog</button>
            </div>
          ) : (
            list.map((e) => (
              <button type="button" className="card" key={e.enrollmentId || e.id} onClick={() => selectEnrollment(e)} aria-pressed={hub?.enrollmentId === e.enrollmentId} style={{ marginBottom: 8, width: '100%', textAlign: 'left', borderColor: hub?.enrollmentId === e.enrollmentId ? 'var(--purple)' : undefined }}>
                <div className="card-body">
                  <strong>{e.courseCode} — {e.courseTitle}</strong>
                  <div style={{ fontSize: 12, color: 'var(--slate)' }}>Trainer: {e.trainer}</div>
                  {e.status === 'reserved' ? <div style={{ marginTop: 6 }}>On hold: {formatCount(e.heldCredits, 'credit')} · Registration closes {formatUtcDateTime(e.registrationClosesAt)}</div> : null}
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
            {tab === 'reserved' && hub ? <div className="callout"><strong>Place reserved</strong><p>{formatCount(hub.heldCredits, 'credit')} {hub.heldCredits === 1 ? 'is' : 'are'} on hold until this class is confirmed. You can cancel before confirmation for a full release.</p><button type="button" className="btn btn-ghost" onClick={() => setPendingAction({ enrollmentId: hub.enrollmentId, action: 'cancel' })}>Cancel reservation</button></div> : null}
            {tab === 'active' && hub ? <><button type="button" className="btn btn-ghost" disabled={busy || hub.withdrawalRefundCredits == null} onClick={() => setPendingAction({ enrollmentId: hub.enrollmentId, action: 'withdraw', refundCredits: hub.withdrawalRefundCredits })}>Withdraw from class</button><p>{hub.withdrawalRefundCredits != null ? `Available refund: ${formatCount(hub.withdrawalRefundCredits, 'credit')} (70%).` : 'Self-withdrawal is available only 6-10 calendar days before the confirmed class starts, using UTC dates.'}</p></> : null}
            {tab === 'history' ? <p>Cancelled and refunded classes are kept here for your records.</p> : null}
            {tab === 'history' && hub ? <PostponementPicker key={hub.enrollmentId} enrollment={hub} busy={busy} onReserve={setPendingAction} /> : null}
            {['active', 'completed'].includes(tab) ? <>
            <p style={{ fontSize: 12, color: 'var(--slate)' }}>
              {hub ? `Session materials for ${hub.courseTitle}` : 'Select a program'}
            </p>
            {hub ? <button type="button" className="btn btn-ghost" disabled={query.loading} onClick={() => { setRefreshFor(hub.enrollmentId); query.refresh(); }}>Refresh resources</button> : null}
            {query.loading && hub ? <p role="status">Loading resources…</p> : null}
            {refreshFor === hub?.enrollmentId && query.data && !query.loading ? <p role="status">Resources refreshed.</p> : null}
            {query.error && hub ? <p role="alert">{userFacingError(query.error, 'Could not load resources. Please refresh to retry.')}</p> : null}
            {query.data && hub ? <>
               <h3>Materials</h3>
               {query.data.materials.length ? query.data.materials.map((item) => <button key={item.id} type="button" className="btn btn-ghost btn-block" onClick={() => download(item)}>{item.title} · {item.format}</button>) : <p>No materials attached yet.</p>}
              <h3>Session materials</h3>
              {sessionMaterialGroups.length ? sessionMaterialGroups.map((group) => <section key={group.key} aria-label={`${group.label} materials`}><h4>{group.label}</h4>{group.items.map((item) => <button key={item.id} type="button" className="btn btn-ghost btn-block" onClick={() => downloadSessionMaterial(item)}>{item.title} · {item.format}</button>)}</section>) : <p>No Session materials uploaded yet.</p>}
               <h3>Recordings</h3>
              {query.data.recordings.length ? query.data.recordings.map((item) => <a key={item.id} className="btn btn-ghost btn-block" href={item.recordingUrl} target="_blank" rel="noopener noreferrer">{item.title}</a>) : <p>No recordings added yet.</p>}
              {hub.meetingLink ? <a className="btn btn-primary btn-block" href={hub.meetingLink} target="_blank" rel="noopener noreferrer">Join Live Session</a> : null}
            </> : null}
            {hub ? <><p>For an enrollment issue or refund review, ask Admin through My Disputes.</p><button type="button" className="btn btn-ghost btn-block" onClick={() => navigate(`/member/disputes?enrollmentId=${hub.enrollmentId}`)}>View or submit a dispute</button></> : null}
            </> : null}
          </div>
        </div>
      </div>
      </> : null}
      <Modal open={Boolean(pendingAction)} title={pendingAction?.action === 'postpone' ? 'Reserve replacement place?' : pendingAction?.action === 'cancel' ? 'Cancel reservation?' : 'Withdraw from class?'} onClose={() => { if (!busy) setPendingAction(null); }}>
        <p>{pendingAction?.action === 'cancel'
          ? 'Your held credits will return to your available balance.'
          : pendingAction?.action === 'postpone' ? `${formatCount(pendingAction.creditsRequired, 'credit')} will be placed on hold again for ${pendingAction.label}. This reserves a new place in Intake #${pendingAction.intakeId}.`
            : `${formatCount(pendingAction?.refundCredits, 'credit')} will be refunded (70%). Your confirmed place will be cancelled. Eligibility uses UTC calendar dates and is checked again when you confirm.`}</p>
        <div className="modal-actions"><button type="button" className="btn btn-ghost" disabled={busy} onClick={() => setPendingAction(null)}>Keep place</button><button type="button" className="btn btn-primary" disabled={busy} onClick={confirmChange}>{busy ? 'Processing…' : 'Confirm'}</button></div>
      </Modal>
    </MemberShell>
  );
}
