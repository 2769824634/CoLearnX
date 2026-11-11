// Each account holds one role. Teaching or creating courses needs a separate, certified account.
export default function OneRoleNotice() {
  return (
    <div className="card" style={{ marginTop: 12 }}>
      <div className="card-header">Account role</div>
      <div className="card-body">
        <p style={{ fontSize: 13, margin: 0 }}>
          Each account holds one role. To teach or create courses, sign out and register a separate Trainer or Creator
          account. It is certified by an administrator before its workspace opens.
        </p>
      </div>
    </div>
  );
}
