import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { authApi, roleRequestsApi } from '../../api';
import { useAuth } from '../../auth/AuthContext';
import { countStatementWords } from './roleApplicationUtils';
import '../../styles/certification.css';

const MAX_STATEMENT_WORDS = 300;
const MAX_SPECIALTIES = 5;
const MAX_FILES = 5;
const MAX_FILE_MB = 10;
const MAX_TOTAL_MB = 25;
const ACCEPT = '.pdf,.docx,.png,.jpg,.jpeg';
const AREA_LABELS = {
  DigitalTechnology: 'Digital Technology',
  BusinessManagement: 'Business and Management',
  HumanitiesHealthCreative: 'Humanities, Health and Creative',
};

// Adds newly picked files to a field, keeping the 5-file and 10 MB limits the server checks.
function addFiles(current, picked) {
  const tooBig = picked.filter((file) => file.size > MAX_FILE_MB * 1024 * 1024);
  const next = [...current, ...picked.filter((file) => !tooBig.includes(file))];
  const problems = [];
  if (tooBig.length) problems.push(`${tooBig.map((file) => file.name).join(', ')} is larger than ${MAX_FILE_MB} MB.`);
  if (next.length > MAX_FILES) problems.push(`You can upload at most ${MAX_FILES} files here.`);
  return { files: next.slice(0, MAX_FILES), problem: problems.join(' ') };
}

function FilePicker({ id, label, files, problem, onChange }) {
  function pick(event) {
    const picked = Array.from(event.target.files || []);
    event.target.value = '';
    if (picked.length) onChange(addFiles(files, picked));
  }
  return (
    <div className="form-group">
      <label htmlFor={id}>{label}</label>
      <input id={id} type="file" multiple accept={ACCEPT} onChange={pick} disabled={files.length >= MAX_FILES} />
      <div className="certification-file-hint">
        Up to {MAX_FILES} files ({files.length}/{MAX_FILES}). PDF, DOCX, PNG or JPG, {MAX_FILE_MB} MB each.
      </div>
      {files.length ? (
        <ul className="certification-file-list">
          {files.map((file, index) => (
            <li key={`${file.name}-${index}`}>
              <span>{file.name}</span>
              <button
                type="button"
                className="btn btn-ghost btn-sm"
                aria-label={`Remove ${file.name}`}
                onClick={() => onChange({ files: files.filter((_, i) => i !== index), problem: '' })}
              >
                Remove
              </button>
            </li>
          ))}
        </ul>
      ) : null}
      {problem ? <p className="trainer-error" role="alert">{problem}</p> : null}
    </div>
  );
}

// Trainers and Creators submit and check their certification from their workspace.
export default function CertificationPage({ role }) {
  const { user, refreshUser } = useAuth();
  const navigate = useNavigate();
  const [requests, setRequests] = useState(null);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const [resume, setResume] = useState({ files: [], problem: '' });
  const [idDocument, setIdDocument] = useState({ files: [], problem: '' });
  const [statement, setStatement] = useState('');
  const [notice, setNotice] = useState('');
  const [options, setOptions] = useState([]);
  const [picked, setPicked] = useState([]);
  const wordCount = countStatementWords(statement);
  const totalBytes = [...resume.files, ...idDocument.files].reduce((sum, file) => sum + file.size, 0);
  const tooLarge = totalBytes > MAX_TOTAL_MB * 1024 * 1024;
  const canSubmit = Boolean(resume.files.length && idDocument.files.length && !tooLarge && statement.trim() && wordCount > 0 && wordCount <= MAX_STATEMENT_WORDS
    && picked.length > 0 && picked.length <= MAX_SPECIALTIES);
  const areas = Object.keys(AREA_LABELS).map((area) => ({ area, items: options.filter((item) => item.area === area) }))
    .filter((group) => group.items.length);
  const home = `/${role.toLowerCase()}/home`;

  useEffect(() => {
    let cancelled = false;
    roleRequestsApi.my().then(
      (items) => { if (!cancelled) setRequests(Array.isArray(items) ? items : []); },
      (err) => { if (!cancelled) { setRequests([]); setError(err.message || 'Applications could not be loaded.'); } },
    );
    roleRequestsApi.specialties().then(
      (items) => { if (!cancelled) setOptions(Array.isArray(items) ? items : []); },
      () => {},
    );
    return () => { cancelled = true; };
  }, []);

  function toggle(id) {
    setPicked((current) => (current.includes(id) ? current.filter((item) => item !== id)
      : current.length >= MAX_SPECIALTIES ? current : [...current, id]));
  }

  const latest = (requests || []).find((item) => item.requestedRole === role);
  const pending = latest?.status === 'Pending';

  async function submit(event) {
    event.preventDefault();
    if (!canSubmit || busy) return;
    setBusy(true);
    setError('');
    try {
      const created = await roleRequestsApi.create(role, resume.files, idDocument.files, statement.trim(), picked);
      setRequests((current) => [created, ...(current || []).filter((item) => item.id !== created.id)]);
      setResume({ files: [], problem: '' });
      setIdDocument({ files: [], problem: '' });
      setStatement('');
      setPicked([]);
    } catch (err) {
      setError(err.message || 'The application could not be submitted.');
    } finally {
      setBusy(false);
    }
  }

  async function enterWorkspace() {
    setBusy(true);
    setNotice('');
    try {
      const me = await authApi.me();
      refreshUser(me);
      if (me.certifiedAt) navigate(home, { replace: true });
      else setNotice('Your certification has not been approved yet.');
    } catch (err) {
      setNotice(err.message || 'Could not check your certification.');
    } finally {
      setBusy(false);
    }
  }

  return (
    <section className={`${role.toLowerCase()}-page`}>
      <div className="card certification-panel">
          <h1>{role} certification</h1>
          <p className="auth-lead">
            Upload your resume, identity document and a short statement. An administrator reviews them before your {role} features open.
          </p>
          {user?.specialties?.length ? (
            <p><strong>Certified specialties:</strong> {user.specialties.join(', ')}</p>
          ) : null}
          {latest?.specialties?.length ? (
            <p><strong>Specialties in this application:</strong> {latest.specialties.join(', ')}</p>
          ) : null}

          {requests === null ? <p className="page-sub">Loading application…</p> : (
            <p role="status">
              <strong>Status:</strong>{' '}
              {!latest ? 'No application yet'
                : pending ? 'Submitted. Waiting for administrator review.'
                  : latest.status === 'Approved' ? 'Approved. Check approval to unlock your workspace features.'
                    : `Not approved${latest.reviewNote ? `: ${latest.reviewNote}` : '.'} You can submit again.`}
            </p>
          )}
          {error ? <p className="trainer-error" role="alert">{error}</p> : null}

          {requests !== null && !pending && latest?.status !== 'Approved' ? (
            <form onSubmit={submit}>
              <fieldset className="form-group" style={{ border: 0, padding: 0 }}>
                <legend>Specialties ({picked.length}/{MAX_SPECIALTIES})</legend>
                {areas.map((group) => (
                  <div key={group.area} style={{ marginBottom: 8 }}>
                    <div style={{ fontSize: 12, color: 'var(--slate)' }}>{AREA_LABELS[group.area]}</div>
                    {group.items.map((item) => (
                      <label key={item.id} style={{ display: 'inline-flex', gap: 4, marginRight: 12, fontSize: 13 }}>
                        <input
                          type="checkbox"
                          checked={picked.includes(item.id)}
                          disabled={!picked.includes(item.id) && picked.length >= MAX_SPECIALTIES}
                          onChange={() => toggle(item.id)}
                        />
                        {item.name}
                      </label>
                    ))}
                  </div>
                ))}
              </fieldset>
              <FilePicker id="cert-resume" label="Resume / degree" {...resume} onChange={setResume} />
              <FilePicker id="cert-id-document" label="Identity document" {...idDocument} onChange={setIdDocument} />
              {tooLarge ? <p className="trainer-error" role="alert">All files together must be {MAX_TOTAL_MB} MB or smaller.</p> : null}
              <div className="form-group">
                <label htmlFor="cert-statement">Application statement</label>
                <textarea id="cert-statement" rows={5} value={statement} onChange={(e) => setStatement(e.target.value)} placeholder="Briefly explain your experience." />
                <div style={{ fontSize: 11, color: wordCount > MAX_STATEMENT_WORDS ? 'var(--danger, #b42318)' : 'var(--slate)', marginTop: 4 }}>
                  {wordCount}/{MAX_STATEMENT_WORDS} words
                </div>
              </div>
              <button type="submit" className="btn btn-primary btn-block" disabled={!canSubmit || busy}>
                {busy ? 'Submitting…' : 'Submit for certification'}
              </button>
            </form>
          ) : null}

          {notice ? <p className="callout" role="status">{notice}</p> : null}
          <div style={{ display: 'flex', gap: 8, marginTop: 16 }}>
            <button type="button" className="btn btn-primary" disabled={busy} onClick={enterWorkspace}>Check approval</button>
          </div>
      </div>
    </section>
  );
}
