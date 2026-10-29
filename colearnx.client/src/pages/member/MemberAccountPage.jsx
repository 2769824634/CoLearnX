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
        <div className="grid-2-1 member-account-grid">
          <div style={{ textAlign: 'center' }}>
            <AvatarEditor role="member" />
            <div style={{ fontWeight: 600 }}>{state.user.fullName}</div>
            <div style={{ fontSize: 12, color: 'var(--slate)', marginTop: 4 }}>Display name · {state.user.displayName}</div>
            <div style={{ fontSize: 11, color: 'var(--slate)', marginTop: 4 }}>Shown in greetings and workspace navigation.</div>
            <div className="card" style={{ marginTop: 16, textAlign: 'left' }}>
              <div className="card-body">
                <div style={{ fontSize: 12, color: 'var(--slate)' }}>Credit Balance</div>
                <div style={{ fontSize: 22, fontWeight: 700, color: 'var(--purple)' }}>{state.credits}</div>
                <button type="button" className="btn btn-ghost btn-sm teal" style={{ marginTop: 8, width: '100%' }} onClick={() => navigate('/member/payment')}>
                  View Credit Ledger →
                </button>
              </div>
            </div>
          </div>
          <div>
            <div className="member-account-heading">
              <div className="member-account-heading-copy">
                <h3 style={{ fontSize: 15, marginBottom: 4 }}>Account details</h3>
                <p style={{ fontSize: 12, color: 'var(--slate)', margin: 0 }}>Update profile details from Edit Profile.</p>
              </div>
              <div className="member-account-actions">
                <button type="button" className="btn btn-primary" onClick={() => setEditOpen(true)}>Edit Profile</button>
                <button type="button" className="btn btn-ghost" onClick={() => navigate('/member/onboarding')}>Edit interests</button>
              </div>
            </div>
            <div className="card" style={{ marginBottom: 12 }}>
              <div className="card-header">Personal info</div>
              <div className="card-body">
                <div className="grid-2">
                  <div className="masked-field"><div className="label">Full Name</div><div className="value">{state.user.fullName}</div></div>
                  <div className="masked-field"><div className="label">Display name</div><div className="value">{state.user.displayName}</div></div>
                  <div className="masked-field"><div className="label">Email</div><div className="value">{user?.email || '—'}</div></div>
                  <div className="masked-field"><div className="label">Phone</div><div className="value">{user?.phone || '—'}</div></div>
                </div>
                <ProfileDetails user={user} />
              </div>
            </div>
            <div className="card">
              <div className="card-header">Identity visibility</div>
              <div className="card-body">
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
