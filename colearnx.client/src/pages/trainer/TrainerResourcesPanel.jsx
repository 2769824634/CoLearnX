import { useCallback, useEffect, useState } from 'react';
import { trainerLaterPhaseApi } from '../../api/trainerLaterPhase';
import { TrainerError } from './TrainerUi';

export default function TrainerResourcesPanel({ token, intake }) {
  const [data, setData] = useState(null);
  const [error, setError] = useState(null);
  const [materialVersionId, setMaterialVersionId] = useState('');
  const [sessionId, setSessionId] = useState(intake.sessions[0]?.id || '');
  const [recording, setRecording] = useState({ title: '', recordingUrl: '' });
  const [busy, setBusy] = useState(false);
  const [revision, setRevision] = useState(0);
  const sessions = intake.sessions;

  const load = useCallback((signal) => Promise.all([
    trainerLaterPhaseApi.availableMaterials(token, signal, intake.courseId),
    trainerLaterPhaseApi.intakeMaterials(token, intake.id, signal),
    Promise.all(sessions.map(async (session) => ({
      sessionId: session.id,
      items: await trainerLaterPhaseApi.recordings(token, intake.id, session.id, signal),
    }))),
  ]).then(([available, attached, recordings]) => ({ available, attached, recordings })), [token, intake.id, intake.courseId, sessions]);

  useEffect(() => {
    const controller = new AbortController();
    load(controller.signal).then((result) => { setData(result); setError(null); }, (failure) => {
      if (!controller.signal.aborted) setError(failure);
    });
    return () => controller.abort();
  }, [load, revision]);

  async function mutate(action, done) {
    if (busy) return;
    setBusy(true);
    setError(null);
    try {
      await action();
      done?.();
      setRevision((value) => value + 1);
    } catch (failure) {
      setError(failure);
    } finally {
      setBusy(false);
    }
  }

  const attachedIds = new Set(data?.attached.map((item) => item.materialVersionId) || []);
  const selectableMaterials = data?.available.filter((item) => item.courseId === intake.courseId && !attachedIds.has(item.versionId)) || [];
  return <section className="later-panel trainer-resource-panel">
    <div className="trainer-section-heading"><div><p className="trainer-eyebrow">Delivery resources</p><h2>Materials & recordings</h2></div></div>
    <TrainerError error={error} />
    {!data && !error ? <div className="trainer-empty">Loading delivery resources…</div> : data ? <div className="later-two-column">
      <div><h3>Approved learning materials</h3>{data.attached.length ? <ul className="later-resource-list">{data.attached.map((item) => <li key={item.materialVersionId}><div><strong>{item.title}</strong><small>{item.format} · version #{item.materialVersionId}</small></div><a href={item.filePath}>Open</a></li>)}</ul> : <p className="trainer-help">No material version is attached yet.</p>}
        <form className="later-inline-form" onSubmit={(event) => { event.preventDefault(); mutate(() => trainerLaterPhaseApi.attachMaterial(token, intake.id, Number(materialVersionId)), () => setMaterialVersionId('')); }}><label htmlFor="attach-material">Attach approved version</label><select id="attach-material" required value={materialVersionId} onChange={(event) => setMaterialVersionId(event.target.value)}><option value="">Select…</option>{selectableMaterials.map((item) => <option key={item.versionId} value={item.versionId}>{item.title} · v{item.versionNumber}</option>)}</select><button className="btn btn-ghost" disabled={busy || !selectableMaterials.length}>Attach</button></form>
      </div>
      <div><h3>Session recordings</h3>{data.recordings.flatMap((group) => group.items).length ? <ul className="later-resource-list">{data.recordings.flatMap((group) => group.items).map((item) => <li key={item.id}><div><strong>{item.title}</strong><small>Session #{item.courseSessionId}</small></div><a href={item.recordingUrl} target="_blank" rel="noreferrer">Watch</a></li>)}</ul> : <p className="trainer-help">No recording link has been added.</p>}
        <form className="later-stack-form" onSubmit={(event) => { event.preventDefault(); mutate(() => trainerLaterPhaseApi.addRecording(token, intake.id, Number(sessionId), recording), () => setRecording({ title: '', recordingUrl: '' })); }}><div className="form-group"><label htmlFor="recording-session">Session</label><select id="recording-session" required value={sessionId} onChange={(event) => setSessionId(event.target.value)}>{intake.sessions.map((item) => <option key={item.id} value={item.id}>{item.label}</option>)}</select></div><div className="form-group"><label htmlFor="recording-title">Recording title</label><input id="recording-title" required maxLength="160" value={recording.title} onChange={(event) => setRecording({ ...recording, title: event.target.value })} /></div><div className="form-group"><label htmlFor="recording-url">HTTPS recording URL</label><input id="recording-url" type="url" required maxLength="1024" value={recording.recordingUrl} onChange={(event) => setRecording({ ...recording, recordingUrl: event.target.value })} /></div><button className="btn btn-ghost" disabled={busy || !intake.sessions.length}>Add recording</button></form>
      </div>
    </div> : null}
  </section>;
}
