import useAdminAuth from '../../auth/useAdminAuth';

export default function AdminDataState({ loading, error, onRetry }) {
  const { logout } = useAdminAuth();
  if (loading) return <div className="admin-review-state" role="status">Loading current records…</div>;
  if (!error) return null;
  const sessionUnavailable = error.status === 401 || error.status === 403;
  return (
    <div className="admin-review-state error" role="alert">
      <strong>{sessionUnavailable ? 'Administrator access unavailable' : 'Records could not be loaded'}</strong>
      <span>{sessionUnavailable
        ? 'Your session has expired or this account no longer has access.'
        : 'Check your connection and try again. No changes have been made.'}</span>
      <button type="button" className="btn btn-ghost" onClick={sessionUnavailable ? logout : onRetry}>
        {sessionUnavailable ? 'Return to sign-in' : 'Try again'}
      </button>
    </div>
  );
}
