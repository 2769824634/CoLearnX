import { useAuth } from '../../auth/AuthContext';
import CreatorPageFrame from './CreatorPageFrame';

function maskEmail(email = '') {
  const [localPart, domain] = email.split('@');
  if (!localPart || !domain) return '';
  if (localPart.length === 1) return `${localPart}***@${domain}`;
  return `${localPart[0]}${'*'.repeat(Math.max(3, localPart.length - 2))}${localPart.at(-1)}@${domain}`;
}

export default function CreatorAccountPage() {
  const { user } = useAuth();

  return (
    <CreatorPageFrame
      eyebrow="Profile"
      title="Creator account"
      description="Review the identity currently active in the Creator workspace."
    >
      <div className="card">
        <div className="card-header">Account details</div>
        <div className="card-body">
          <p><strong>Name:</strong> {user?.fullName}</p>
          <p><strong>Email:</strong> {maskEmail(user?.email)}</p>
        </div>
      </div>
    </CreatorPageFrame>
  );
}
