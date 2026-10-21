import { useEffect, useRef } from 'react';
import '../../styles/creator-intakes.css';
import { userFacingError } from '../businessPresentation';

export function CreatorHeader({ eyebrow = 'Creator review desk', title, children, action }) {
  return <header className="creator-header"><div><p className="creator-eyebrow">{eyebrow}</p><h1>{title}</h1><p>{children}</p></div>{action}</header>;
}

export function CreatorError({ error, onRetry }) {
  const ref = useRef(null);
  useEffect(() => { if (error) ref.current?.focus(); }, [error]);
  if (!error) return null;
  return <div className="creator-error" role="alert" tabIndex={-1} ref={ref}><strong>{userFacingError(error)}</strong>{error.code === 'UNSAVED_CHANGES' ? <p>Use Save changes, then submit the saved version.</p> : <p>{onRetry ? 'This view could not load. Retry to reload it.' : 'Your requested change could not be confirmed. Check the current state before trying again.'}</p>}{onRetry ? <button type="button" className="btn btn-ghost" onClick={onRetry}>Retry loading</button> : null}</div>;
}

