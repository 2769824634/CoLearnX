import { useCallback, useEffect, useMemo, useState } from 'react';
import { trainerLaterPhaseApi } from '../../api/trainerLaterPhase';
import { formatUtcDate } from '../businessPresentation';
import { TrainerError } from './TrainerUi';

const MAX_FILE_BYTES = 20 * 1024 * 1024;
const ALLOWED_EXTENSIONS = new Set(['.pdf', '.pptx', '.docx', '.png', '.jpg', '.jpeg']);
const ACCEPT = '.pdf,.pptx,.docx,.png,.jpg,.jpeg';

function sessionKey(session) {
  return String(session?.id ?? '');
}

function sessionLabel(session) {
  return session?.label || `Session ${session?.id ?? ''}`;
}

function downloadName(material) {
  const extension = String(material?.format || 'bin').replace(/^\./, '').toLowerCase().replace('jpeg', 'jpg');
  return `${material?.title || 'session-material'}.${extension}`;
}

function validationError(message, field) {
  return { code: 'INVALID_SESSION_MATERIAL', status: 400, message, fieldErrors: field ? { [field]: [message] } : {} };
}

function fileError(file) {
  if (!file) return validationError('Choose a PDF, PPTX, DOCX, PNG, JPG or JPEG file.', 'file');
  const name = String(file.name || '');
  const extension = name.includes('.') ? `.${name.split('.').pop().toLowerCase()}` : '';
  if (!ALLOWED_EXTENSIONS.has(extension)) return validationError('Choose a PDF, PPTX, DOCX, PNG, JPG or JPEG file.', 'file');
  if (file.size > MAX_FILE_BYTES) return validationError('File must be 20 MB or smaller.', 'file');
  return null;
}

export default function TrainerSessionMaterialsPanel({ token, intake }) {
  const sessions = useMemo(() => intake?.sessions || [], [intake?.sessions]);
  const sessionIds = useMemo(() => sessions.map(sessionKey).filter(Boolean), [sessions]);
  const [materials, setMaterials] = useState({});
  const [drafts, setDrafts] = useState({});
  const [formVersion, setFormVersion] = useState({});
  const [loadedKey, setLoadedKey] = useState('');
  const [error, setError] = useState(null);
  const [notice, setNotice] = useState('');
  const [busy, setBusy] = useState(null);
  const [revision, setRevision] = useState(0);
  const intakeId = intake?.id;
  const hasSessions = sessionIds.length > 0;
  const requestKey = `${intakeId ?? ''}:${sessionIds.join(',')}:${revision}`;
  const loading = hasSessions && loadedKey !== requestKey;

  const load = useCallback(async (signal) => {
    const result = await Promise.all(sessionIds.map(async (id) => {
      const list = await trainerLaterPhaseApi.sessionMaterials(token, intakeId, Number(id), signal);
      return [id, Array.isArray(list) ? list : []];
    }));
    return Object.fromEntries(result);
  }, [intakeId, sessionIds, token]);

  useEffect(() => {
    if (!hasSessions) return undefined;
    const controller = new AbortController();
    load(controller.signal).then((result) => {
      if (!controller.signal.aborted) {
        setMaterials(result);
        setError(null);
        setLoadedKey(requestKey);
      }
    }).catch((failure) => {
      if (!controller.signal.aborted) {
        setError(failure);
        setLoadedKey(requestKey);
      }
    });
    return () => controller.abort();
  }, [hasSessions, load, requestKey]);

  function draftFor(id) {
    return drafts[id] || { title: '', file: null };
  }

  function updateDraft(id, changes) {
    setDrafts((current) => ({ ...current, [id]: { ...draftFor(id), ...changes } }));
  }

  async function upload(session, event) {
    event.preventDefault();
    const id = sessionKey(session);
    const draft = draftFor(id);
    const title = draft.title.trim();
    if (!title) {
      setError(validationError('A material title is required.', 'title'));
      return;
    }
    const invalidFile = fileError(draft.file);
    if (invalidFile) {
      setError(invalidFile);
      return;
    }
    if (busy) return;
    setBusy({ type: 'upload', sessionId: id });
    setError(null);
    setNotice('');
    try {
      await trainerLaterPhaseApi.uploadSessionMaterial(token, intakeId, Number(id), title, draft.file);
      setDrafts((current) => ({ ...current, [id]: { title: '', file: null } }));
      setFormVersion((current) => ({ ...current, [id]: (current[id] || 0) + 1 }));
      setNotice(`Material uploaded for ${sessionLabel(session)}.`);
      setRevision((current) => current + 1);
    } catch (failure) {
      setError(failure);
    } finally {
      setBusy(null);
    }
  }

  async function download(session, material) {
    if (busy) return;
    const id = sessionKey(session);
    setBusy({ type: 'download', materialId: String(material.id) });
    setError(null);
    setNotice('');
    try {
      await trainerLaterPhaseApi.downloadSessionMaterial(
        token, intakeId, Number(material.courseSessionId || id), material.id, downloadName(material),
      );
      setNotice(`Download started for “${material.title}”.`);
    } catch (failure) {
      setError(failure);
    } finally {
      setBusy(null);
    }
  }

  return (
    <section className="later-panel trainer-session-materials" aria-labelledby="trainer-session-materials-title">
      <div className="trainer-section-heading">
        <div>
          <p className="trainer-eyebrow">Per-Session resources</p>
          <h2 id="trainer-session-materials-title">Session materials</h2>
        </div>
      </div>
      <p className="trainer-help">Upload a PDF, PPTX, DOCX, PNG or JPG/JPEG file for the Session where learners will use it. Files must be 20 MB or smaller.</p>
      <TrainerError error={error} onRetry={() => { setNotice(''); setRevision((current) => current + 1); }} retryLabel="Refresh Session materials" />
      {notice ? <p className="trainer-notice" role="status">{notice}</p> : null}
      {loading ? <div className="trainer-empty" role="status">Loading Session materials…</div> : !sessions.length ? <div className="trainer-empty">No Sessions are configured for this Intake.</div> : (
        <div className="trainer-sessions">
          {sessions.map((session) => {
            const id = sessionKey(session);
            const label = sessionLabel(session);
            const list = materials[id] || [];
            const draft = draftFor(id);
            const formId = formVersion[id] || 0;
            const uploadBusy = busy?.type === 'upload' && busy.sessionId === id;
            return (
              <article key={id} className="trainer-session-card">
                <div className="trainer-section-heading">
                  <div><p className="trainer-eyebrow">Session #{id}</p><h3>{label}</h3></div>
                </div>
                {list.length ? (
                  <ul className="later-resource-list">
                    {list.map((material) => {
                      const downloading = busy?.type === 'download' && busy.materialId === String(material.id);
                      return <li key={material.id}>
                        <div><strong>{material.title}</strong><small>{material.format || 'File'} · uploaded {formatUtcDate(material.uploadedAt)}</small></div>
                        <button type="button" className="btn btn-ghost" aria-label={`Download ${material.title}`} disabled={Boolean(busy)} onClick={() => download(session, material)}>
                          {downloading ? 'Downloading…' : 'Download'}
                        </button>
                      </li>;
                    })}
                  </ul>
                ) : <p className="trainer-help">No materials uploaded for this Session.</p>}
                <form key={`${id}:${formId}`} className="later-stack-form" onSubmit={(event) => upload(session, event)}>
                  <div className="form-group">
                    <label htmlFor={`session-material-title-${id}`}>Material title for {label}</label>
                    <input id={`session-material-title-${id}`} maxLength="160" required value={draft.title} disabled={Boolean(busy)} onChange={(event) => updateDraft(id, { title: event.target.value })} />
                  </div>
                  <div className="form-group">
                    <label htmlFor={`session-material-file-${id}`}>Material file for {label}</label>
                    <input id={`session-material-file-${id}`} type="file" accept={ACCEPT} required disabled={Boolean(busy)} onChange={(event) => updateDraft(id, { file: event.target.files?.[0] || null })} />
                  </div>
                  <button className="btn btn-primary" type="submit" disabled={Boolean(busy)}>{uploadBusy ? 'Uploading…' : `Upload material for ${label}`}</button>
                </form>
              </article>
            );
          })}
        </div>
      )}
    </section>
  );
}
