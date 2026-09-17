import { useEffect, useState } from 'react';
import { authApi, roleRequestsApi } from '../../api';
import { useAuth } from '../../auth/AuthContext';
import Modal from '../../components/Modal';

const APPLY_ROLES = [
  { id: 'Trainer', blurb: 'Run intakes, attendance and certificates.' },
  { id: 'Creator', blurb: 'Publish courses and learning materials.' },
];
const MAX_STATEMENT_WORDS = 300;

function roleSet(user) {
  return new Set((user?.roles || []).map((role) => String(role)));
}

function latestFor(requests, role) {
  return requests.find((item) => item.requestedRole === role);
}

export function countStatementWords(text) {
  if (!text) return 0;
  let count = 0;
  let inLatin = false;
  for (const ch of text) {
    const code = ch.codePointAt(0);
    const isHan = (code >= 0x3400 && code <= 0x4dbf)
      || (code >= 0x4e00 && code <= 0x9fff)
      || (code >= 0xf900 && code <= 0xfaff);
    if (isHan) {
      if (inLatin) {
        count += 1;
        inLatin = false;
      }
      count += 1;
    } else if (/\s/.test(ch)) {
      if (inLatin) {
        count += 1;
        inLatin = false;
      }
    } else if (/[\p{L}\p{N}]/u.test(ch)) {
      inLatin = true;
    }
  }
  if (inLatin) count += 1;
  return count;
}

export default function RoleApplicationsPanel() {
  const { user, refreshUser } = useAuth();
  const [requests, setRequests] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [busyRole, setBusyRole] = useState('');
  const [applyRole, setApplyRole] = useState('');
  const [resume, setResume] = useState(null);
  const [idDocument, setIdDocument] = useState(null);
  const [statement, setStatement] = useState('');
  const granted = roleSet(user);
  const wordCount = countStatementWords(statement);
  const canSubmit = Boolean(resume && idDocument && statement.trim() && wordCount > 0 && wordCount <= MAX_STATEMENT_WORDS);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      setLoading(true);
      setError('');
      try {
        const items = await roleRequestsApi.my();
        if (cancelled) return;
        setRequests(items);
        const owned = roleSet(user);
        if (items.some((item) => item.status === 'Approved' && !owned.has(item.requestedRole))) {
          const me = await authApi.me();
          if (!cancelled) refreshUser(me);
        }
      } catch (err) {
        if (!cancelled) setError(err.message || 'Role requests could not be loaded.');
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [user?.id]);

  function openApply(role) {
    if (busyRole) return;
    setApplyRole(role);
    setResume(null);
    setIdDocument(null);
    setStatement('');
    setError('');
  }

  function closeApply() {
    if (busyRole) return;
    setApplyRole('');
  }

  async function submitApplication(event) {
    event.preventDefault();
    if (!applyRole || busyRole || !canSubmit) return;
    setBusyRole(applyRole);
    setError('');
    try {
      const created = await roleRequestsApi.create(applyRole, resume, idDocument, statement.trim());
      setRequests((current) => [created, ...current.filter((item) => item.id !== created.id)]);
      setApplyRole('');
      setResume(null);
      setIdDocument(null);
      setStatement('');
    } catch (err) {
      setError(err.message || 'The request could not be submitted.');
    } finally {
      setBusyRole('');
    }
  }

  return (
    <div className="card" style={{ marginTop: 12 }}>
      <div className="card-header">Workspace applications</div>
      <div className="card-body">
        <p style={{ fontSize: 12, color: 'var(--slate)', marginTop: 0 }}>
          Apply for Trainer or Creator. After you click Apply, upload a resume, identity document and a short statement.
          Files go to the role-requests store, not course materials. Approved roles appear in the workspace switcher.
        </p>
        {loading ? <p className="page-sub">Loading applications…</p> : null}
        {error && !applyRole ? <p className="trainer-error" role="alert">{error}</p> : null}
        {APPLY_ROLES.map((role) => {
          const latest = latestFor(requests, role.id);
          const owned = granted.has(role.id);
          let statusText = 'Not requested';
          let action = !owned ? (
            <button
              type="button"
              className="btn btn-primary btn-sm"
              disabled={Boolean(busyRole)}
              onClick={() => openApply(role.id)}
            >
              Apply for {role.id}
            </button>
          ) : null;

          if (owned) {
            statusText = `${role.id} workspace is active`;
            action = <span className="pill">Active</span>;
          } else if (latest?.status === 'Pending') {
            statusText = `${role.id} request is pending`;
            action = <span className="pill">Pending</span>;
          } else if (latest?.status === 'Approved') {
            statusText = `${role.id} access approved. Switch workspace from the top bar.`;
            action = <span className="pill">Approved</span>;
          } else if (latest?.status === 'Rejected') {
            statusText = latest.reviewNote
              ? `Last ${role.id} request was rejected: ${latest.reviewNote}`
              : `Last ${role.id} request was rejected.`;
          }

          return (
            <div key={role.id} style={{ display: 'flex', gap: 12, alignItems: 'flex-start', marginBottom: 10, fontSize: 13 }}>
              <div style={{ flex: 1, minWidth: 0 }}>
                <strong>{role.id}</strong>
                <div style={{ color: 'var(--slate)', marginTop: 2 }}>{role.blurb}</div>
                <div style={{ marginTop: 4 }}>{statusText}</div>
              </div>
              {action}
            </div>
          );
        })}
      </div>

      <Modal
        open={Boolean(applyRole)}
        title={applyRole ? `Apply for ${applyRole}` : 'Apply'}
        onClose={closeApply}
        width={560}
      >
        {applyRole ? (
          <form onSubmit={submitApplication}>
            <p className="page-sub" style={{ marginTop: 0 }}>
              Upload your resume and identity document, then add a short statement. Keep it to {MAX_STATEMENT_WORDS} words.
            </p>
            <div className="form-group">
              <label htmlFor="role-resume">Resume / degree</label>
              <input
                id="role-resume"
                type="file"
                accept=".pdf,.docx,.png,.jpg,.jpeg"
                onChange={(event) => setResume(event.target.files?.[0] || null)}
              />
              <div style={{ fontSize: 11, color: 'var(--slate)', marginTop: 4 }}>
                {resume ? resume.name : 'PDF, DOCX, PNG or JPG'}
              </div>
            </div>
            <div className="form-group">
              <label htmlFor="role-id-document">Identity document</label>
              <input
                id="role-id-document"
                type="file"
                accept=".pdf,.docx,.png,.jpg,.jpeg"
                onChange={(event) => setIdDocument(event.target.files?.[0] || null)}
              />
              <div style={{ fontSize: 11, color: 'var(--slate)', marginTop: 4 }}>
                {idDocument ? idDocument.name : 'PDF, DOCX, PNG or JPG'}
              </div>
            </div>
            <div className="form-group">
              <label htmlFor="role-statement">Application statement</label>
              <textarea
                id="role-statement"
                rows={5}
                value={statement}
                onChange={(event) => setStatement(event.target.value)}
                placeholder="Briefly explain why you are applying."
              />
              <div style={{ fontSize: 11, color: wordCount > MAX_STATEMENT_WORDS ? 'var(--danger, #b42318)' : 'var(--slate)', marginTop: 4 }}>
                {wordCount}/{MAX_STATEMENT_WORDS} words
              </div>
            </div>
            {error ? <p className="trainer-error" role="alert">{error}</p> : null}
            <div className="modal-actions">
              <button type="button" className="btn btn-ghost" onClick={closeApply} disabled={Boolean(busyRole)}>
                Cancel
              </button>
              <button type="submit" className="btn btn-primary" disabled={!canSubmit || Boolean(busyRole)}>
                {busyRole ? 'Submitting…' : 'Submit application'}
              </button>
            </div>
          </form>
        ) : null}
      </Modal>
    </div>
  );
}
