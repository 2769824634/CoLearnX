import { useEffect, useRef } from 'react';
import { Link } from 'react-router-dom';
import '../../styles/creator-intakes.css';
import { formatUtcDateTime, userFacingError } from '../businessPresentation';

export function CreatorHeader({ eyebrow = 'Creator review desk', title, children, action }) {
  useEffect(() => {
    const previous = document.title;
    document.title = `CoLearnX — Creator · ${title}`;
    return () => { document.title = previous; };
  }, [title]);
  return <header className="creator-header"><div><p className="creator-eyebrow">{eyebrow}</p><h1>{title}</h1><p>{children}</p></div>{action}</header>;
}

export function CreatorError({ error, onRetry }) {
  const ref = useRef(null);
  useEffect(() => { if (error) ref.current?.focus(); }, [error]);
  if (!error) return null;
  return <div className="creator-error" role="alert" tabIndex={-1} ref={ref}><strong>{userFacingError(error)}</strong><p>{onRetry ? 'This view could not load. Retry to reload it.' : 'Your requested change could not be confirmed. Check the current state before trying again.'}</p>{onRetry ? <button type="button" className="btn btn-ghost" onClick={onRetry}>Retry loading</button> : null}</div>;
}

export function CreatorApplicationRows({ applications }) {
  if (!applications.length) return <div className="creator-empty"><strong>No session approvals waiting</strong><p>After a Trainer submits an Intake with Sessions, it will appear here for you to approve.</p></div>;
  return <div className="creator-application-list">{applications.map((item) => <article key={item.applicationId} className="creator-application-row">
    <div className="creator-application-index">#{item.courseIntakeId}</div><div><p className="creator-eyebrow">{item.courseCode} · {item.kind === 'Change' ? 'Change request' : 'New sessions'}</p><h2>{item.courseTitle}</h2><p>{item.trainerName} · submitted {formatUtcDateTime(item.submittedAt)}</p></div><span className={`creator-status ${item.status.toLowerCase()}`}>{item.status}</span><Link to={`/creator/courses/intake-applications/${item.courseIntakeId}`}>Approve sessions →</Link>
  </article>)}</div>;
}
