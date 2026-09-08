import { useState } from 'react';
import { buildChangeRequest, initialChangeProposal } from './b4Workflow';
import { ScheduleForm, SessionForm } from './IntakeForms';

const nextKey = () => globalThis.crypto?.randomUUID?.() || `${Date.now()}-${Math.random()}`;

export default function ChangeRequestEditor({ intake, busy, blocked, error, onError, onCancel, onSubmit }) {
  const [proposal, setProposal] = useState(() => initialChangeProposal(intake));
  const [editor, setEditor] = useState(null);
  const [saved, setSaved] = useState('');
  const editing = editor?.session;
  const intakeShape = { ...proposal, version: intake.version };

  function saveSchedule(body) {
    const schedule = Object.fromEntries(Object.entries(body).filter(([key]) => key !== 'version'));
    setProposal((current) => ({ ...current, ...schedule }));
    setSaved('Proposed dates updated locally. Submit the full proposal when ready.');
  }

  function saveSession(body) {
    const session = Object.fromEntries(Object.entries(body).filter(([key]) => key !== 'version'));
    if (editing) {
      setProposal((current) => ({ ...current, sessions: current.sessions.map((item) => item._key === editing._key ? { ...session, id: item.id ?? null, _key: item._key } : item) }));
    } else {
      setProposal((current) => ({ ...current, sessions: [...current.sessions, { ...session, id: null, _key: nextKey() }] }));
    }
    setEditor(null);
    setSaved('Proposed Session updated locally.');
  }

  return <section className="trainer-change-editor" aria-labelledby="change-request-title">
    <div className="trainer-section-heading"><div><p className="trainer-eyebrow">Controlled change</p><h2 id="change-request-title">Request Creator reconfirmation</h2></div><button type="button" className="btn btn-ghost" disabled={busy} onClick={onCancel}>Close editor</button></div>
    <p className="trainer-help">Your confirmed schedule stays live until the Creator approves this complete replacement proposal. Meeting-link-only changes belong in each Session’s delivery action.</p>
    {saved ? <div className="trainer-notice" role="status">{saved}</div> : null}
    <ScheduleForm key={`${proposal.startsAt}:${proposal.endsAt}`} intake={intakeShape} error={error} onError={onError} busy={busy} blocked={blocked} onSave={saveSchedule} />
    <div className="trainer-section-heading"><h3>Proposed Sessions <span className="trainer-count">{proposal.sessions.length}</span></h3>{!editor ? <button type="button" className="btn btn-ghost" onClick={() => setEditor({ type: 'session' })}>+ Add proposed Session</button> : null}</div>
    {editor ? <SessionForm key={editing?._key || 'new-change-session'} session={editing} intake={intakeShape} error={error} onError={onError} busy={busy} blocked={blocked} onCancel={() => setEditor(null)} onSave={saveSession} /> : null}
    <div className="trainer-change-session-list">{proposal.sessions.map((session) => <article key={session._key}>
      <div><strong>{session.label}</strong><small>{new Date(session.startsAt).toLocaleString()} → {new Date(session.endsAt).toLocaleString()}</small></div>
      {!editor ? <div className="trainer-actions"><button type="button" className="btn btn-ghost" onClick={() => setEditor({ type: 'session', session })}>Edit proposal</button><button type="button" className="btn btn-ghost" onClick={() => setProposal((current) => ({ ...current, sessions: current.sessions.filter((item) => item._key !== session._key) }))}>Remove</button></div> : null}
    </article>)}</div>
    <div className="trainer-submit-panel"><div><h3>Submit complete proposal</h3><p>The Creator will compare this proposal against the currently confirmed schedule.</p></div><button type="button" className="btn btn-primary" disabled={busy || blocked || Boolean(editor) || !proposal.sessions.length} onClick={() => onSubmit(buildChangeRequest(intake, proposal))}>{busy ? 'Submitting…' : 'Send change request'}</button></div>
  </section>;
}
