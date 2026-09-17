import { useState } from 'react';
import { usersApi } from '../../api';
import { useAuth } from '../../auth/AuthContext';
import Modal from '../../components/Modal';
import { maskEmail, maskPhone } from '../../data/memberMock';
import RoleApplicationsPanel from './RoleApplicationsPanel';

const IDENTITY_ROWS = [
  { key: 'member', label: 'Member', grantedHint: 'Learner profile & certificates' },
  { key: 'trainer', label: 'Trainer', grantedHint: 'Specialisations · programs taught' },
  { key: 'creator', label: 'Creator', grantedHint: 'Expertise · materials' },
];

function roleSet(user) {
  return new Set((user?.roles || []).map((role) => String(role).toLowerCase()));
}

function isVisible(user, key) {
  const vis = user?.identityVisibility || {};
  if (typeof vis[key] === 'boolean') return vis[key];
  const titled = key.charAt(0).toUpperCase() + key.slice(1);
  return Boolean(vis[titled]);
}

function AccountField({ id, label, value, onChange }) {
  return (
    <div className="form-group">
      <label htmlFor={id}>{label}</label>
      <input id={id} value={value} onChange={(e) => onChange(e.target.value)} />
    </div>
  );
}

function profileDraft(user) {
  return {
    fullName: user?.fullName ?? '',
    displayName: user?.displayName ?? '',
    phone: user?.phone ?? '',
    bio: user?.bio ?? '',
    trainerHeadline: user?.trainerHeadline ?? '',
    specialisations: user?.specialisations ?? '',
    creatorHeadline: user?.creatorHeadline ?? '',
    expertiseTags: user?.expertiseTags ?? '',
    identityVisibility: {
      member: isVisible(user, 'member'),
      trainer: isVisible(user, 'trainer'),
      creator: isVisible(user, 'creator'),
    },
  };
}

export default function UserAccountPage({ eyebrow, extraKind, summaryTitle, summaryStats = [] }) {
  const { user, refreshUser } = useAuth();
  const [editOpen, setEditOpen] = useState(false);
  const [draft, setDraft] = useState(null);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [toast, setToast] = useState('');
  const granted = roleSet(user);

  function openEdit() {
    setError('');
    setDraft(profileDraft(user));
    setEditOpen(true);
  }

  async function save() {
    if (!draft || saving) return;
    setSaving(true);
    setError('');
    try {
      const updated = await usersApi.update(user.id, {
        fullName: draft.fullName,
        displayName: draft.displayName,
        phone: draft.phone,
        bio: draft.bio,
        identityVisibility: draft.identityVisibility,
        specialisations: extraKind === 'trainer' ? draft.specialisations : undefined,
        trainerHeadline: extraKind === 'trainer' ? draft.trainerHeadline : undefined,
        expertiseTags: extraKind === 'creator' ? draft.expertiseTags : undefined,
        creatorHeadline: extraKind === 'creator' ? draft.creatorHeadline : undefined,
      });
      refreshUser(updated);
      setEditOpen(false);
      setToast('Profile saved');
      window.setTimeout(() => setToast(''), 2200);
    } catch (e) {
      setError(e.message || 'Profile could not be saved.');
    } finally {
      setSaving(false);
    }
  }

  const headline = extraKind === 'trainer' ? user?.trainerHeadline : user?.creatorHeadline;
  const initials = (user?.fullName || 'U')
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0])
    .join('')
    .toUpperCase();

  return (
    <>
      <div className="account-page">
        <div className="account-page-head">
          <div>
            <p className="account-eyebrow">{eyebrow}</p>
            <h1>Profile / My Account</h1>
            <p>Privacy protected · email/phone masked · edit via Edit Profile modal</p>
          </div>
          <button type="button" className="btn btn-primary" onClick={openEdit}>Edit Profile</button>
        </div>
        <div className="grid-2-1" style={{ gridTemplateColumns: '240px 1fr' }}>
          <div style={{ textAlign: 'center' }}>
            <div className="avatar lg" style={{ margin: '0 auto 12px' }}>{initials}</div>
            <div style={{ fontWeight: 600 }}>{user?.fullName}</div>
            <div style={{ fontSize: 12, color: 'var(--slate)', marginTop: 4 }}>
              Display name · {user?.displayName || user?.fullName}
            </div>
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
              <div className="card-header">Personal info <span className="pill" style={{ marginLeft: 8 }}>Masked</span></div>
              <div className="card-body">
                <div className="grid-2">
                  <div className="masked-field"><div className="label">Full Name</div><div className="value">{user?.fullName || '—'}</div></div>
                  <div className="masked-field"><div className="label">Active identities</div><div className="value">{(user?.roles || []).join(' · ') || '—'}</div></div>
                  <div className="masked-field"><div className="label">Email</div><div className="value">{maskEmail(user?.email || '')}</div></div>
                  <div className="masked-field"><div className="label">Phone</div><div className="value">{maskPhone(user?.phone || '')}</div></div>
                </div>
                <div className="masked-field" style={{ marginTop: 12 }}>
                  <div className="label">Shared Bio</div>
                  <div className="value">{user?.bio || 'No bio yet.'}</div>
                </div>
                {extraKind === 'trainer' ? (
                  <div className="masked-field" style={{ marginTop: 12 }}>
                    <div className="label">Specialisations</div>
                    <div className="value">{user?.specialisations || '—'}</div>
                  </div>
                ) : null}
                {extraKind === 'creator' ? (
                  <div className="masked-field" style={{ marginTop: 12 }}>
                    <div className="label">Expertise</div>
                    <div className="value">{user?.expertiseTags || '—'}</div>
                  </div>
                ) : null}
                <div className="callout" style={{ marginTop: 12 }}>
                  <div className="callout-title">Privacy</div>
                  Email and phone are masked on My Account.
                </div>
              </div>
            </div>
            <div className="card">
              <div className="card-header">Identity visibility</div>
              <div className="card-body">
                <p style={{ fontSize: 12, color: 'var(--slate)', marginTop: 0 }}>
                  Edit Show/Hide and role fields only inside Edit Profile modal.
                </p>
                {IDENTITY_ROWS.map((row) => {
                  const hasRole = granted.has(row.key);
                  return (
                    <div key={row.key} style={{ display: 'flex', marginBottom: 8, fontSize: 13, gap: 12 }}>
                      <span style={{ minWidth: 72, fontWeight: 600 }}>{row.label}</span>
                      <span style={{ flex: 1, color: 'var(--slate)' }}>
                        {hasRole ? row.grantedHint : 'Not activated'}
                      </span>
                      <span className={`pill${hasRole && isVisible(user, row.key) ? '' : ' muted'}`}>
                        {!hasRole ? 'Hidden / pending' : isVisible(user, row.key) ? 'Visible' : 'Hidden'}
                      </span>
                    </div>
                  );
                })}
              </div>
            </div>
            <RoleApplicationsPanel />
          </div>
        </div>
      </div>

      <Modal open={editOpen && draft} title="Edit Profile" onClose={() => !saving && setEditOpen(false)} width={640}>
        {draft ? (
          <>
            <div className="grid-2">
              <AccountField id="account-fullName" label="Full Name" value={draft.fullName} onChange={(fullName) => setDraft({ ...draft, fullName })} />
              <AccountField id="account-displayName" label="Display name" value={draft.displayName} onChange={(displayName) => setDraft({ ...draft, displayName })} />
              <AccountField id="account-phone" label="Phone" value={draft.phone} onChange={(phone) => setDraft({ ...draft, phone })} />
              <AccountField id="account-bio" label="Bio" value={draft.bio} onChange={(bio) => setDraft({ ...draft, bio })} />
              {extraKind === 'trainer' ? (
                <>
                  <AccountField id="account-trainerHeadline" label="Trainer headline" value={draft.trainerHeadline} onChange={(trainerHeadline) => setDraft({ ...draft, trainerHeadline })} />
                  <AccountField id="account-specialisations" label="Specialisations" value={draft.specialisations} onChange={(specialisations) => setDraft({ ...draft, specialisations })} />
                </>
              ) : null}
              {extraKind === 'creator' ? (
                <>
                  <AccountField id="account-creatorHeadline" label="Creator headline" value={draft.creatorHeadline} onChange={(creatorHeadline) => setDraft({ ...draft, creatorHeadline })} />
                  <AccountField id="account-expertiseTags" label="Expertise tags" value={draft.expertiseTags} onChange={(expertiseTags) => setDraft({ ...draft, expertiseTags })} />
                </>
              ) : null}
            </div>
            {IDENTITY_ROWS.filter((row) => granted.has(row.key)).map((row) => (
              <div className="identity-block" key={row.key}>
                <div className="ib-head">
                  <span className="grow">{row.label} visibility</span>
                  <button
                    type="button"
                    className={`switch${draft.identityVisibility[row.key] ? ' on' : ''}`}
                    aria-pressed={draft.identityVisibility[row.key]}
                    aria-label={`${row.label} visibility`}
                    onClick={() => setDraft({
                      ...draft,
                      identityVisibility: { ...draft.identityVisibility, [row.key]: !draft.identityVisibility[row.key] },
                    })}
                  />
                </div>
              </div>
            ))}
            {error ? <p className="trainer-error" role="alert">{error}</p> : null}
            <div className="modal-actions">
              <button type="button" className="btn btn-ghost" onClick={() => setEditOpen(false)} disabled={saving}>Cancel</button>
              <button type="button" className="btn btn-primary" onClick={save} disabled={saving}>{saving ? 'Saving…' : 'Save Changes'}</button>
            </div>
          </>
        ) : null}
      </Modal>
      <div className={`toast${toast ? ' show' : ''}`}>{toast}</div>
    </>
  );
}
