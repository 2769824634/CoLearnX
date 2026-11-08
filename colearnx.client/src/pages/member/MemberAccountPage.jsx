import { useNavigate } from 'react-router-dom';
import MemberShell from '../../components/MemberShell';
import { useAuth } from '../../auth/AuthContext';
import { ProfileEditor, ProfileDetails, IdentityVisibility } from '../account/ProfileEditor';
import { useMemberData } from './memberDataState';
import RoleApplicationsPanel from '../account/RoleApplicationsPanel';
import AvatarEditor from '../account/AvatarEditor';
import useAccountShortcuts from '../account/useAccountShortcuts';

export default function MemberAccountPage() {
  const navigate = useNavigate();
  const { state, showToast } = useMemberData();
  const { user } = useAuth();
  const { editOpen, setEditOpen, closeEdit } = useAccountShortcuts();

  return (
    <>
      <MemberShell
        title="Profile / My Account"
      >
        <div className="grid-2-1 member-account-grid cx-account">
          <div className="cx-account-side">
            <div className="card cx-account-profile">
              <div className="card-body">
                <AvatarEditor role="member" />
                <div className="cx-account-name">{state.user.fullName}</div>
                <div className="cx-account-display">Display name · {state.user.displayName}</div>
                <div className="cx-account-hint">Shown in greetings and workspace navigation.</div>
              </div>
            </div>
            <div className="cx-account-credits">
              <span className="cx-wallet-balance-label">Credit Balance</span>
              <strong>{state.credits}</strong>
              <button type="button" className="btn btn-ghost btn-sm" onClick={() => navigate('/member/payment')}>
                View Credit Ledger →
              </button>
            </div>
          </div>
          <div>
            <div className="member-account-heading">
              <div className="member-account-heading-copy">
                <h3>Account details</h3>
                <p>Update profile details from Edit Profile.</p>
              </div>
              <div className="account-profile-actions">
                <button type="button" className="btn btn-primary account-profile-button" onClick={() => setEditOpen(true)}>Edit Profile</button>
                <button type="button" className="btn btn-ghost account-profile-button" onClick={() => navigate('/member/onboarding')}>Edit interests</button>
              </div>
            </div>
            <div className="card" style={{ marginBottom: 12 }}>
              <div className="card-header">Personal info</div>
              <div className="card-body">
                <div className="cx-account-fields">
                  <div className="masked-field"><div className="label">Full Name</div><div className="value">{state.user.fullName}</div></div>
                  <div className="masked-field"><div className="label">Display name</div><div className="value">{state.user.displayName}</div></div>
                  <div className="masked-field"><div className="label">Email</div><div className="value">{user?.email || '—'}</div></div>
                  <div className="masked-field"><div className="label">Phone</div><div className="value">{user?.phone || '—'}</div></div>
                  <ProfileDetails user={user} />
                </div>
              </div>
            </div>
            <div className="card">
              <div className="card-header">Identity visibility</div>
              <div className="card-body cx-account-rows">
                <IdentityVisibility user={user} />
              </div>
            </div>
            <RoleApplicationsPanel />
          </div>
        </div>
      </MemberShell>

      {editOpen && <ProfileEditor onClose={(saved) => { closeEdit(); if (saved) showToast('Profile saved'); }} />}
    </>
  );
}
