import { useState } from 'react';
import { useAuth } from '../../auth/AuthContext';
import RoleApplicationsPanel from './RoleApplicationsPanel';
import AvatarEditor from './AvatarEditor';
import { ProfileEditor, ProfileDetails, IdentityVisibility } from './ProfileEditor';
import useAccountShortcuts from './useAccountShortcuts';

export default function UserAccountPage({ eyebrow, extraKind, summaryTitle, summaryStats = [] }) {
  const { user } = useAuth();
  const { editOpen, setEditOpen, closeEdit: closeShortcut } = useAccountShortcuts();
  const [toast, setToast] = useState('');

  function closeEdit(saved) {
    closeShortcut();
    if (saved) {
      setToast('Profile saved');
      window.setTimeout(() => setToast(''), 2200);
    }
  }

  const headline = extraKind === 'trainer' ? user?.trainerHeadline : user?.creatorHeadline;
  return (
    <>
      <div className="account-page">
        <div className="account-page-head">
          <div>
            <p className="account-eyebrow">{eyebrow}</p>
            <h1>Profile / My Account</h1>
            <p>Profile updates are available from Edit Profile</p>
          </div>
          <button type="button" className="btn btn-primary" onClick={() => setEditOpen(true)}>Edit Profile</button>
        </div>
        <div className="grid-2-1 account-page-grid">
          <div style={{ textAlign: 'center' }}>
            <AvatarEditor role={extraKind} />
            <div style={{ fontWeight: 600 }}>{user?.fullName}</div>
            <div style={{ fontSize: 12, color: 'var(--slate)', marginTop: 4 }}>
              Display name · {user?.displayName || user?.fullName}
            </div>
            <div style={{ fontSize: 11, color: 'var(--slate)', marginTop: 4 }}>Shown in greetings and workspace navigation.</div>
            {headline ? (
              <div style={{ fontSize: 12, color: 'var(--slate)', marginTop: 8 }}>{headline}</div>
            ) : null}
            <div className="card" style={{ marginTop: 16, textAlign: 'left' }}>
              <div className="card-body">
                <div style={{ fontSize: 12, color: 'var(--slate)' }}>{summaryTitle}</div>
                <div className="account-summary-stats">
                  {summaryStats.map((item) => (
                    <div key={item.label}>
                      <div style={{ fontSize: 11, color: 'var(--slate)' }}>{item.label}</div>
                      <div style={{ fontSize: 22, fontWeight: 700, color: 'var(--purple)' }}>{item.value}</div>
                    </div>
                  ))}
                </div>
              </div>
            </div>
          </div>
          <div>
            <div className="card" style={{ marginBottom: 12 }}>
              <div className="card-header">Personal info</div>
              <div className="card-body">
                <div className="grid-2">
                  <div className="masked-field"><div className="label">Full Name</div><div className="value">{user?.fullName || '—'}</div></div>
                  <div className="masked-field"><div className="label">Display name</div><div className="value">{user?.displayName || '—'}</div></div>
                  <div className="masked-field"><div className="label">Active identities</div><div className="value">{(user?.roles || []).join(' · ') || '—'}</div></div>
                  <div className="masked-field"><div className="label">Email</div><div className="value">{user?.email || '—'}</div></div>
                  <div className="masked-field"><div className="label">Phone</div><div className="value">{user?.phone || '—'}</div></div>
                </div>
                <ProfileDetails user={user} />
              </div>
            </div>
            <div className="card">
              <div className="card-header">Identity visibility</div>
              <div className="card-body">
                <p style={{ fontSize: 12, color: 'var(--slate)', marginTop: 0 }}>
                  Update visibility and role details from Edit Profile.
                </p>
                <IdentityVisibility user={user} />
              </div>
            </div>
            <RoleApplicationsPanel />
          </div>
        </div>
      </div>

      {editOpen && <ProfileEditor onClose={closeEdit} />}
      <div className={`toast${toast ? ' show' : ''}`}>{toast}</div>
    </>
  );
}
