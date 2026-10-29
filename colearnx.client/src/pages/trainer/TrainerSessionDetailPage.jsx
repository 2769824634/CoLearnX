import { useMemo, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { trainerDeliveryApi } from '../../api/trainerDelivery';
import { trainerIntakesApi } from '../../api/trainerIntakes';
import { trainerLaterPhaseApi } from '../../api/trainerLaterPhase';
import { formatDate, safeMeetingLink } from './intakeForm';
import { TrainerError, TrainerHeader, TrainerLoading } from './TrainerUi';
import TrainerSessionMaterialsPanel from './TrainerSessionMaterialsPanel';
import useTrainerQuery from './useTrainerQuery';

async function loadSessionWorkspace(token, key, signal) {
  const [intakeId, sessionId] = key.split(':').map(Number);
  const [intake, courses, learners, recordings] = await Promise.all([
    trainerIntakesApi.get(token, intakeId, signal),
    trainerIntakesApi.publishedCourses(token, signal),
    trainerLaterPhaseApi.learners(token, intakeId, signal),
    trainerLaterPhaseApi.recordings(token, intakeId, sessionId, signal),
  ]);
  const session = intake.sessions.find((item) => item.id === sessionId);
  if (!session) throw new Error('Session not found.');
  return { intake, course: courses.find((item) => item.id === intake.courseId), session, learners, recordings };
}

function currentAttendance(learner, sessionId) {
  return learner.sessionAttendances?.find((item) => item.courseSessionId === sessionId)?.status;
}

function AttendancePanel({ query, api }) {
  const { intake, session } = query.data;
  const learners = useMemo(() => query.data.learners.filter((item) => ['Active', 'Completed'].includes(item.enrollmentStatus)), [query.data.learners]);
  const [draft, setDraft] = useState({});
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);
  const [notice, setNotice] = useState('');
  async function save() {
    if (!learners.length || busy) return;
    setBusy(true); setError(null); setNotice('');
    try {
      await api.saveAttendance(query.token, intake.id, session.id, learners.map((learner) => ({
        enrollmentId: learner.enrollmentId,
        status: draft[learner.enrollmentId] || currentAttendance(learner, session.id) || 'Present',
      })));
      setNotice('Attendance saved.');
      query.refresh();
    } catch (failure) { setError(failure); }
    finally { setBusy(false); }
  }
  return <section className="later-panel"><div className="trainer-section-heading"><div><p className="trainer-eyebrow">Session check-in</p><h2>Attendance</h2></div></div>
    <TrainerError error={error} />{notice ? <p className="trainer-notice" role="status">{notice}</p> : null}
    {!learners.length ? <div className="trainer-empty">No active or completed learners are enrolled in this Intake.</div> : <><div className="later-table-wrap"><table className="later-table"><thead><tr><th>Learner</th><th>Attendance</th></tr></thead><tbody>{learners.map((learner) => <tr key={learner.enrollmentId}><td><strong>{learner.fullName}</strong><small>{learner.email}</small></td><td><select aria-label={`Attendance for ${learner.fullName}`} value={draft[learner.enrollmentId] || currentAttendance(learner, session.id) || 'Present'} onChange={(event) => setDraft((value) => ({ ...value, [learner.enrollmentId]: event.target.value }))}><option>Present</option><option>Late</option><option>Absent</option></select></td></tr>)}</tbody></table></div><button className="btn btn-primary" type="button" disabled={busy} onClick={save}>{busy ? 'Saving…' : 'Save attendance'}</button></>}
  </section>;
}

function RecordingPanel({ query, api }) {
  const { intake, session, recordings } = query.data;
  const [title, setTitle] = useState('');
  const [file, setFile] = useState(null);
  const [version, setVersion] = useState(0);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);
  const [notice, setNotice] = useState('');
  async function upload(event) {
    event.preventDefault();
    if (!file || busy) return;
    setBusy(true); setError(null); setNotice('');
    try {
      await api.uploadRecording(query.token, intake.id, session.id, title.trim(), file);
      setTitle(''); setFile(null); setVersion((value) => value + 1); setNotice('Recording uploaded.'); query.refresh();
    } catch (failure) { setError(failure); }
    finally { setBusy(false); }
  }
  return <section className="later-panel"><div className="trainer-section-heading"><div><p className="trainer-eyebrow">Session replay</p><h2>Recording video</h2></div></div>
    <TrainerError error={error} />{notice ? <p className="trainer-notice" role="status">{notice}</p> : null}
    {recordings.length ? <ul className="later-resource-list">{recordings.map((recording) => <li key={recording.id}><div><strong>{recording.title}</strong></div>{recording.isUploadedFile ? <button type="button" className="btn btn-ghost" onClick={() => api.downloadRecording(query.token, intake.id, session.id, recording.id, recording.downloadName || recording.title)}>Download</button> : <a href={recording.recordingUrl} target="_blank" rel="noreferrer">Watch</a>}</li>)}</ul> : <p className="trainer-help">No recording uploaded for this Session.</p>}
    <form key={version} className="later-stack-form" onSubmit={upload}><div className="form-group"><label htmlFor="recording-title">Recording title</label><input id="recording-title" required maxLength="160" value={title} onChange={(event) => setTitle(event.target.value)} /></div><div className="form-group"><label htmlFor="recording-file">Video file</label><input id="recording-file" type="file" accept=".mp4,.webm,.mov,video/mp4,video/webm,video/quicktime" required onChange={(event) => setFile(event.target.files?.[0] || null)} /></div><button className="btn btn-primary" disabled={busy}>{busy ? 'Uploading…' : 'Upload recording'}</button></form>
  </section>;
}

function SessionWorkspace({ query, api, deliveryApi }) {
  const { intake, course, session } = query.data;
  const [editing, setEditing] = useState(false);
  const [meetingLink, setMeetingLink] = useState(session.meetingLink || '');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);
  const link = safeMeetingLink(session.meetingLink);
  async function save(event) {
    event.preventDefault(); setBusy(true); setError(null);
    try {
      const updated = await deliveryApi.updateMeetingLink(query.token, intake.id, session.id, meetingLink.trim() || null, intake.version);
      query.setData({ ...query.data, intake: updated, session: updated.sessions.find((item) => item.id === session.id) }); setEditing(false);
    } catch (failure) { setError(failure); }
    finally { setBusy(false); }
  }
  return <><TrainerHeader eyebrow={`${course?.code || `Course #${intake.courseId}`} / Intake #${intake.id}`} title={session.label}>All delivery work for this Session is managed here.</TrainerHeader>
    <p>{formatDate(session.startsAt)} → {formatDate(session.endsAt)}</p>
    <section className="later-panel"><div className="trainer-section-heading"><div><p className="trainer-eyebrow">Live delivery</p><h2>Meeting link</h2></div><button className="btn btn-ghost" type="button" onClick={() => setEditing(true)}>Update meeting link</button></div>
      {link ? <a href={link} target="_blank" rel="noopener noreferrer">{link}</a> : <p className="trainer-help">No online meeting link.</p>}
      <TrainerError error={error} />{editing ? <form className="later-stack-form" onSubmit={save}><div className="form-group"><label htmlFor="session-meeting-link">Online meeting link</label><input id="session-meeting-link" type="url" required={!session.physicalAddress} value={meetingLink} onChange={(event) => setMeetingLink(event.target.value)} /></div><div className="trainer-actions"><button className="btn btn-primary" disabled={busy}>Save delivery link</button><button className="btn btn-ghost" type="button" onClick={() => setEditing(false)}>Cancel</button></div></form> : null}
    </section>
    <AttendancePanel query={query} api={api} />
    <TrainerSessionMaterialsPanel token={query.token} intake={{ ...intake, sessions: [session] }} api={api} singleSession />
    <RecordingPanel query={query} api={api} />
  </>;
}

export default function TrainerSessionDetailPage({
  token = null,
  laterPhaseApi = trainerLaterPhaseApi,
  deliveryApi = trainerDeliveryApi,
  loadWorkspace = loadSessionWorkspace,
  backPath = null,
}) {
  const { courseIntakeId, courseSessionId } = useParams();
  const query = useTrainerQuery(loadWorkspace, `${courseIntakeId}:${courseSessionId}`, token);
  return <section className="trainer-page later-page"><div className="trainer-detail-nav"><Link className="trainer-back" to={backPath || `/trainer/courses/intakes/${courseIntakeId}`}>← Intake #{courseIntakeId}</Link></div>{query.loading ? <TrainerLoading /> : query.error ? <TrainerError error={query.error} onRetry={query.refresh} /> : <SessionWorkspace query={query} api={laterPhaseApi} deliveryApi={deliveryApi} />}</section>;
}
