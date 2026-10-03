import useAdminAuth from '../../auth/useAdminAuth';

export default function AdminDataState({ loading, error, onRetry, operation }) {
  const { logout } = useAdminAuth();
  if (loading) return <div className="admin-review-state" role="status">Loading current records…</div>;
  if (!error) return null;
  const sessionUnavailable = error.status === 401 || error.status === 403;
  return (
    <div className="admin-review-state error" role="alert">
      <strong>{sessionUnavailable ? 'Administrator access unavailable' : operation ? `${operation} result is unconfirmed` : 'Records could not be loaded'}</strong>
      <span>{sessionUnavailable
        ? 'Your session has expired or this account no longer has access.'
        : operation ? 'Check the current balance, case status and ledger before retrying this operation.' : 'Check your connection and reload the current records.'}</span>
      {error.traceId ? <small>Reference: {error.traceId}</small> : null}
      <button type="button" className="btn btn-ghost" onClick={sessionUnavailable ? logout : onRetry}>
        {sessionUnavailable ? 'Return to sign-in' : operation ? 'Check current records' : 'Try again'}
      </button>
    </div>
  );
}
