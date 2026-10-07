export function AccessNotice({ notice, onDismiss }) {
  if (!notice) return null;
  return (
    <div className="callout warn" role="status" aria-label="Access notice">
      <div className="callout-title">Workspace access</div>
      <div>{notice}</div>
      <button type="button" className="btn btn-ghost" onClick={onDismiss}>Dismiss</button>
    </div>
  );
}
