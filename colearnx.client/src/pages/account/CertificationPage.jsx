import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { authApi, roleRequestsApi } from '../../api';
import { useAuth } from '../../auth/AuthContext';
import { countStatementWords } from './roleApplicationUtils';
import '../../styles/certification.css';

const MAX_STATEMENT_WORDS = 300;
const MAX_SPECIALTIES = 5;
const AREA_LABELS = {
  DigitalTechnology: 'Digital Technology',
  BusinessManagement: 'Business and Management',
  HumanitiesHealthCreative: 'Humanities, Health and Creative',
};

// Trainers and Creators submit and check their certification from their workspace.
export default function CertificationPage({ role }) {
  const { user, refreshUser } = useAuth();
  const navigate = useNavigate();
  const [requests, setRequests] = useState(null);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const [resume, setResume] = useState(null);
  const [idDocument, setIdDocument] = useState(null);
  const [statement, setStatement] = useState('');
  const [notice, setNotice] = useState('');
  const [options, setOptions] = useState([]);
  const [picked, setPicked] = useState([]);
  const wordCount = countStatementWords(statement);
  const canSubmit = Boolean(resume && idDocument && statement.trim() && wordCount > 0 && wordCount <= MAX_STATEMENT_WORDS
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
      const created = await roleRequestsApi.create(role, resume, idDocument, statement.trim(), picked);
      setRequests((current) => [created, ...(current || []).filter((item) => item.id !== created.id)]);
      setResume(null);
      setIdDocument(null);
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
              <div className="form-group">
                <label htmlFor="cert-resume">Resume / degree</label>
                <input id="cert-resume" type="file" accept=".pdf,.docx,.png,.jpg,.jpeg" onChange={(e) => setResume(e.target.files?.[0] || null)} />
              </div>
              <div className="form-group">
                <label htmlFor="cert-id-document">Identity document</label>
                <input id="cert-id-document" type="file" accept=".pdf,.docx,.png,.jpg,.jpeg" onChange={(e) => setIdDocument(e.target.files?.[0] || null)} />
              </div>
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
