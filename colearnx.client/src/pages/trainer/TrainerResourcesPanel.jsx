import { useCallback, useEffect, useState } from 'react';
import { canPreviewMaterial, trainerLaterPhaseApi } from '../../api/trainerLaterPhase';
import { TrainerError } from './TrainerUi';

function downloadName(item) {
  const ext = String(item.format || 'bin').replace(/^\./, '').toLowerCase().replace(/jpeg/, 'jpg');
  return `${item.title}.${ext}`;
}

export default function TrainerResourcesPanel({ token, intake }) {
  const [data, setData] = useState(null);
  const [error, setError] = useState(null);
  const [materialVersionId, setMaterialVersionId] = useState('');
  const [sessionId, setSessionId] = useState(intake.sessions[0]?.id || '');
  const [recording, setRecording] = useState({ title: '', recordingUrl: '' });
  const [busy, setBusy] = useState(false);
  const [download, setDownload] = useState(null);
  const [preview, setPreview] = useState(null);
  const [revision, setRevision] = useState(0);
  const sessions = intake.sessions;
  const downloadBusy = Boolean(download?.busy);

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

  useEffect(() => () => { if (preview?.url) URL.revokeObjectURL(preview.url); }, [preview?.url]);

  async function mutate(action, done) {
    if (busy || downloadBusy) return;
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

  async function openMaterial(item) {
    if (busy || downloadBusy) return;
    setDownload({ id: item.materialVersionId, busy: true });
    setError(null);
    try {
      const blob = await trainerLaterPhaseApi.downloadMaterial(token, intake.id, item.materialVersionId, downloadName(item));
      if (canPreviewMaterial(item.format) && blob) {
        setPreview({
            id: item.materialVersionId,
            title: item.title,
            format: item.format,
            url: URL.createObjectURL(blob),
        });
      } else {
        setPreview(null);
      }
      setDownload({
        id: item.materialVersionId,
        message: canPreviewMaterial(item.format)
          ? 'Opened the attached version. The file was also saved to your downloads.'
          : 'File download started. In-browser preview is available for PNG, JPG, GIF, WebP and PDF.',
      });
    } catch (failure) {
      setDownload({ id: item.materialVersionId });
      setError(failure);
    }
  }

  const attachedIds = new Set(data?.attached.map((item) => item.materialVersionId) || []);
  const selectableMaterials = data?.available.filter((item) => item.courseId === intake.courseId && !attachedIds.has(item.versionId)) || [];
  return <section className="later-panel trainer-resource-panel">
    <div className="trainer-section-heading"><div><p className="trainer-eyebrow">Delivery resources</p><h2>Materials & recordings</h2></div></div>
    <TrainerError error={error} />
    {!data && !error ? <div className="trainer-empty">Loading delivery resources…</div> : data ? <div className="later-two-column">
      <div><h3>Approved learning materials</h3>{data.attached.length ? <ul className="later-resource-list">{data.attached.map((item) => <li key={item.materialVersionId}><div><strong>{item.title}</strong><small>{item.format} · approved version{item.versionNumber ? ` v${item.versionNumber}` : ''} · record #{item.materialVersionId}</small></div><button type="button" className="btn btn-ghost" disabled={busy || downloadBusy} onClick={() => openMaterial(item)}>{downloadBusy && download.id === item.materialVersionId ? 'Opening…' : 'Open'}</button></li>)}</ul> : <p className="trainer-help">No material version is attached yet.</p>}
        {download?.message ? <p className="trainer-help" role="status">{download.message}</p> : null}
        {preview?.url ? (String(preview.format).replace(/^\./, '').toUpperCase() === 'PDF'
          ? <iframe title={preview.title} src={preview.url} className="trainer-material-preview" />
          : <img alt={preview.title} src={preview.url} className="trainer-material-preview" />) : null}
        <form className="later-inline-form" onSubmit={(event) => { event.preventDefault(); mutate(() => trainerLaterPhaseApi.attachMaterial(token, intake.id, Number(materialVersionId)), () => setMaterialVersionId('')); }}><label htmlFor="attach-material">Attach approved version</label><select id="attach-material" required value={materialVersionId} onChange={(event) => setMaterialVersionId(event.target.value)}><option value="">Select…</option>{selectableMaterials.map((item) => <option key={item.versionId} value={item.versionId}>{item.title} · v{item.versionNumber}</option>)}</select><button className="btn btn-ghost" disabled={busy || downloadBusy || !selectableMaterials.length}>Attach</button></form>
        {!selectableMaterials.length ? <p className="trainer-help">No more approved versions are available to attach.</p> : null}
      </div>
      <div><h3>Session recordings</h3>{data.recordings.flatMap((group) => group.items).length ? <ul className="later-resource-list">{data.recordings.flatMap((group) => group.items).map((item) => <li key={item.id}><div><strong>{item.title}</strong><small>Session #{item.courseSessionId}</small></div><a href={item.recordingUrl} target="_blank" rel="noreferrer">Watch</a></li>)}</ul> : <p className="trainer-help">No recording link has been added.</p>}
        <form className="later-stack-form" onSubmit={(event) => { event.preventDefault(); mutate(() => trainerLaterPhaseApi.addRecording(token, intake.id, Number(sessionId), recording), () => setRecording({ title: '', recordingUrl: '' })); }}><div className="form-group"><label htmlFor="recording-session">Session</label><select id="recording-session" required value={sessionId} onChange={(event) => setSessionId(event.target.value)}>{intake.sessions.map((item) => <option key={item.id} value={item.id}>{item.label}</option>)}</select></div><div className="form-group"><label htmlFor="recording-title">Recording title</label><input id="recording-title" required maxLength="160" value={recording.title} onChange={(event) => setRecording({ ...recording, title: event.target.value })} /></div><div className="form-group"><label htmlFor="recording-url">HTTPS recording URL</label><input id="recording-url" type="url" required maxLength="1024" value={recording.recordingUrl} onChange={(event) => setRecording({ ...recording, recordingUrl: event.target.value })} /></div><button className="btn btn-ghost" disabled={busy || downloadBusy || !intake.sessions.length}>Add recording</button></form>
      </div>
    </div> : null}
  </section>;
}
