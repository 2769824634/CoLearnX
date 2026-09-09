import useAdminAuth from '../../auth/useAdminAuth';

export default function AdminAccountPage() {
  const { admin } = useAdminAuth();
  const [name, domain] = admin.email.split('@');
  return <div className="admin-overview">
    <header className="admin-review-header"><div>
      <p className="admin-form-eyebrow">Administrator identity</p>
      <h1>My Account</h1><p>Your administrator account is separate from learner, trainer and creator identities.</p>
    </div></header>
    <dl className="admin-account-details">
      <div><dt>Account</dt><dd>Admin #{admin.id}</dd></div>
      <div><dt>Email</dt><dd>{name.slice(0, 1)}***@{domain}</dd></div>
      <div><dt>Workspace</dt><dd>Administration</dd></div>
    </dl>
  </div>;
}
