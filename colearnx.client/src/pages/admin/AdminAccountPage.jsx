import useAdminAuth from '../../auth/useAdminAuth';
import { maskEmail } from '../../data/memberMock';

export default function AdminAccountPage() {
  const { admin } = useAdminAuth();
  return <div className="admin-overview">
    <header className="admin-review-header"><div>
      <p className="admin-form-eyebrow">Administrator identity</p>
      <h1>Profile / My Account</h1>
      <p>This operations account is separate from learner, trainer and creator identities. Contact details stay masked here.</p>
    </div></header>
    <dl className="admin-account-details">
      <div><dt>Account</dt><dd>Admin #{admin.id}</dd></div>
      <div><dt>Email</dt><dd>{maskEmail(admin.email)}</dd></div>
      <div><dt>Workspace</dt><dd>Administration</dd></div>
      <div><dt>Profile edits</dt><dd>Administrator identity is provisioned, not self-edited.</dd></div>
    </dl>
  </div>;
}
