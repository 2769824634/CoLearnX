import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import MemberShell from '../../components/MemberShell';
import { useAuth } from '../../auth/AuthContext';
import { ProfileEditor, ProfileDetails, IdentityVisibility } from '../account/ProfileEditor';
import { maskEmail, maskPhone } from '../../data/memberMock';
import { useMemberData } from './memberDataState';
import RoleApplicationsPanel from '../account/RoleApplicationsPanel';
import AvatarEditor from '../account/AvatarEditor';

// Masked account + Edit Profile.
export default function MemberAccountPage() {
  const navigate = useNavigate();
  const { state, showToast } = useMemberData();
  const { user } = useAuth();
  const [editOpen, setEditOpen] = useState(false);

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
            <div style={{ display: 'flex', gap: 12, marginBottom: 16, flexWrap: 'wrap' }}>
              <div style={{ flex: 1 }}>
                <h3 style={{ fontSize: 15, marginBottom: 4 }}>Account details</h3>
                <p style={{ fontSize: 12, color: 'var(--slate)', margin: 0 }}>Privacy protected. Update profile details from Edit Profile.</p>
              </div>
              <button type="button" className="btn btn-primary" onClick={() => setEditOpen(true)}>Edit Profile</button>
              <button type="button" className="btn btn-ghost" onClick={() => navigate('/member/onboarding')}>Edit interests</button>
            </div>
            <div className="card" style={{ marginBottom: 12 }}>
              <div className="card-header">Personal info <span className="pill" style={{ marginLeft: 8 }}>Masked</span></div>
              <div className="card-body">
                <div className="grid-2">
                  <div className="masked-field"><div className="label">Full Name</div><div className="value">{state.user.fullName}</div></div>
                  <div className="masked-field"><div className="label">Display name</div><div className="value">{state.user.displayName}</div></div>
                  <div className="masked-field"><div className="label">Email</div><div className="value">{maskEmail(state.user.email)}</div></div>
                  <div className="masked-field"><div className="label">Phone</div><div className="value">{maskPhone(state.user.phone || '')}</div></div>
                </div>
                <ProfileDetails user={user} />
                <div className="callout" style={{ marginTop: 12 }}>
                  <div className="callout-title">Privacy</div>
                  Email and phone are masked on My Account.
                </div>
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

      {editOpen && <ProfileEditor onClose={(saved) => { setEditOpen(false); if (saved) showToast('Profile saved'); }} />}
    </>
  );
}
