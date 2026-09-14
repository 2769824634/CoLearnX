import { useEffect, useRef } from 'react';
import { Link } from 'react-router-dom';
import '../../styles/creator-intakes.css';

export function CreatorHeader({ eyebrow = 'Creator review desk', title, children, action }) {
  useEffect(() => {
    const previous = document.title;
    document.title = 'CoLearnX — Creator review';
    return () => { document.title = previous; };
  }, []);
  return <header className="creator-header"><div><p className="creator-eyebrow">{eyebrow}</p><h1>{title}</h1><p>{children}</p></div>{action}</header>;
}

export function CreatorError({ error, onRetry }) {
  const ref = useRef(null);
  useEffect(() => { if (error) ref.current?.focus(); }, [error]);
  if (!error) return null;
  return <div className="creator-error" role="alert" tabIndex={-1} ref={ref}><strong>{error.message || 'The request could not be completed.'}</strong><code>{error.code || 'NETWORK_ERROR'}</code>{onRetry ? <button type="button" className="btn btn-ghost" onClick={onRetry}>Try again</button> : null}</div>;
}

export function CreatorApplicationRows({ applications }) {
  if (!applications.length) return <div className="creator-empty"><strong>No session approvals waiting</strong><p>After a Trainer submits an Intake with Sessions, it will appear here for you to approve.</p></div>;
  return <div className="creator-application-list">{applications.map((item) => <article key={item.applicationId} className="creator-application-row">
    <div className="creator-application-index">#{item.courseIntakeId}</div><div><p className="creator-eyebrow">{item.courseCode} · {item.kind === 'Change' ? 'Change request' : 'New sessions'}</p><h2>{item.courseTitle}</h2><p>{item.trainerName} · submitted {new Date(item.submittedAt).toLocaleString()}</p></div><span className={`creator-status ${item.status.toLowerCase()}`}>{item.status}</span><Link to={`/creator/courses/intake-applications/${item.courseIntakeId}`}>Approve sessions →</Link>
  </article>)}</div>;
}
