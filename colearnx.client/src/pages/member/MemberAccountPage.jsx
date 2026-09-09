import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import MemberShell from '../../components/MemberShell';
import Modal from '../../components/Modal';
import { maskEmail, maskPhone } from '../../data/memberMock';
import { useMemberData } from './memberDataState';

// Masked account + Edit Profile.
export default function MemberAccountPage() {
  const navigate = useNavigate();
  const { state, showToast, saveProfile } = useMemberData();
  const [editOpen, setEditOpen] = useState(false);
  const [draft, setDraft] = useState(null);
  const vis = state.identityVisibility;

  function openEdit() {
    setDraft({
      user: { ...state.user },
      identityVisibility: { ...state.identityVisibility },
      prefs: { ...state.prefs },
    });
    setEditOpen(true);
  }

  async function save() {
    await saveProfile(draft);
    setEditOpen(false);
  }

  return (
    <>
      <MemberShell
        title="Profile / My Account"
        onNotify={() => showToast('No new notifications')}
      >
        <div className="grid-2-1" style={{ gridTemplateColumns: '240px 1fr' }}>
          <div style={{ textAlign: 'center' }}>
            <div className="avatar lg" style={{ margin: '0 auto 12px' }} />
            <div style={{ fontWeight: 600 }}>{state.user.fullName}</div>
            <div style={{ fontSize: 12, color: 'var(--slate)', marginTop: 4 }}>Display name · {state.user.displayName}</div>
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
            <div style={{ display: 'flex', gap: 12, marginBottom: 16 }}>
              <div style={{ flex: 1 }}>
                <h3 style={{ fontSize: 15, marginBottom: 4 }}>Account details</h3>
                <p style={{ fontSize: 12, color: 'var(--slate)', margin: 0 }}>Privacy protected. Edit via modal.</p>
              </div>
              <button type="button" className="btn btn-primary" onClick={openEdit}>Edit Profile</button>
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
                <div className="callout" style={{ marginTop: 12 }}>
                  <div className="callout-title">Privacy</div>
                  Email and phone are masked on My Account.
                </div>
              </div>
            </div>
            <div className="card">
              <div className="card-header">Identity visibility</div>
              <div className="card-body">
                <div style={{ display: 'flex', marginBottom: 8, fontSize: 13 }}><span>Member</span><span style={{ flex: 1 }} /><span className="pill">{vis.member ? 'Visible' : 'Hidden'}</span></div>
                <div style={{ display: 'flex', marginBottom: 8, fontSize: 13 }}><span>Trainer</span><span style={{ flex: 1 }} /><span className="pill">{vis.trainer ? 'Visible' : 'Hidden'}</span></div>
                <div style={{ display: 'flex', fontSize: 13 }}><span>Creator</span><span style={{ flex: 1 }} /><span className="pill muted">{vis.creator ? 'Visible' : 'Hidden / pending'}</span></div>
              </div>
            </div>
          </div>
        </div>
      </MemberShell>

      <Modal open={editOpen && draft} title="Edit Profile" onClose={() => setEditOpen(false)} width={640}>
        {draft ? (
          <>
            <div className="grid-2">
              <div className="form-group"><label>Full Name</label><input value={draft.user.fullName} onChange={(e) => setDraft({ ...draft, user: { ...draft.user, fullName: e.target.value } })} /></div>
              <div className="form-group"><label>Display name</label><input value={draft.user.displayName} onChange={(e) => setDraft({ ...draft, user: { ...draft.user, displayName: e.target.value } })} /></div>
              <div className="form-group"><label>Phone</label><input value={draft.user.phone} onChange={(e) => setDraft({ ...draft, user: { ...draft.user, phone: e.target.value } })} /></div>
              <div className="form-group"><label>Bio</label><input value={draft.user.bio} onChange={(e) => setDraft({ ...draft, user: { ...draft.user, bio: e.target.value } })} /></div>
            </div>
            <div className="identity-block">
              <div className="ib-head">
                <span className="grow">Member visibility</span>
                <button type="button" className={`switch${draft.identityVisibility.member ? ' on' : ''}`} onClick={() => setDraft({ ...draft, identityVisibility: { ...draft.identityVisibility, member: !draft.identityVisibility.member } })} />
              </div>
            </div>
            <div className="identity-block">
              <div className="ib-head">
                <span className="grow">Trainer visibility</span>
                <button type="button" className={`switch${draft.identityVisibility.trainer ? ' on' : ''}`} onClick={() => setDraft({ ...draft, identityVisibility: { ...draft.identityVisibility, trainer: !draft.identityVisibility.trainer } })} />
              </div>
            </div>
            <div className="modal-actions">
              <button type="button" className="btn btn-ghost" onClick={() => setEditOpen(false)}>Cancel</button>
              <button type="button" className="btn btn-primary" onClick={save}>Save Changes</button>
            </div>
          </>
        ) : null}
      </Modal>
    </>
  );
}
