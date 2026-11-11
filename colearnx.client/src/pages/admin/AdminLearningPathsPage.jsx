import { useState } from 'react';
import { learningPathsApi } from '../../api';
import { userFacingError } from '../businessPresentation';
import AdminDataState from './AdminDataState';
import useAdminQuery from './useAdminQuery';
import useAdminAuth from '../../auth/useAdminAuth';
import '../../styles/learning-paths.css';

const loadPaths = (token, _key, signal) => learningPathsApi.adminList(token, signal);

export default function AdminLearningPathsPage() {
  const query = useAdminQuery(loadPaths, 'learning-path-settings');
  const { token } = useAdminAuth();
  const [drafts, setDrafts] = useState({});
  const [busy, setBusy] = useState('');
  const [message, setMessage] = useState('');

  async function save(path, stage) {
    const key = `${path.id}:${stage.number}`;
    const value = Number(drafts[key] ?? stage.requiredCourseCount);
    if (!Number.isInteger(value) || value < 1 || value > 100) { setMessage('Enter a whole number between 1 and 100.'); return; }
    setBusy(key); setMessage('');
    try {
      await learningPathsApi.updateStage(token, path.id, stage.number, value);
      setDrafts((current) => { const next = { ...current }; delete next[key]; return next; });
      query.refresh();
      setMessage(`${path.name}: ${stage.name} requirement saved.`);
    } catch (error) { setMessage(userFacingError(error, 'Could not save the requirement.')); }
    finally { setBusy(''); }
  }

  return <section className="admin-review-page">
    <header className="admin-review-header"><div><p className="admin-form-eyebrow">Learning</p><h1>Learning paths</h1>
      <p>Set the number of certified courses needed in each stage. Previously issued certificates remain valid.</p></div></header>
    <AdminDataState loading={query.loading} error={query.error} onRetry={query.refresh} />
    {message ? <p className="callout" role="status">{message}</p> : null}
    <div className="learning-path-admin">{(query.data || []).map((path) => <section className="admin-review-workspace" key={path.id}>
      <h2>{path.name}</h2><div className="learning-path-admin-stages">{path.stages.map((stage) => {
        const key = `${path.id}:${stage.number}`;
        return <div key={stage.number} className="learning-path-admin-stage">
          <label htmlFor={`requirement-${key}`}>{stage.name} <span>courses required</span></label>
          <div><input id={`requirement-${key}`} type="number" min="1" max="100" step="1" value={drafts[key] ?? stage.requiredCourseCount}
            onChange={(event) => setDrafts((current) => ({ ...current, [key]: event.target.value }))} />
            <button type="button" className="btn btn-primary btn-sm" disabled={Boolean(busy)} onClick={() => save(path, stage)}>{busy === key ? 'Saving…' : 'Save'}</button></div>
        </div>;
      })}</div>
    </section>)}</div>
  </section>;
}
