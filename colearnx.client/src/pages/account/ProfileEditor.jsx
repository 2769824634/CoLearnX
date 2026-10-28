import { useState } from 'react';
import { usersApi } from '../../api';
import { useAuth } from '../../auth/AuthContext';
import Modal from '../../components/Modal';

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

export function ProfileEditor({ onClose }) {
  const { user, refreshUser } = useAuth();
  const [draft, setDraft] = useState(() => profileDraft(user));
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const granted = roleSet(user);

  async function save() {
    if (!draft || saving) return;
    setSaving(true);
    setError('');
    try {
      const updated = await usersApi.update(user.id, {
        fullName: draft.fullName, displayName: draft.displayName, phone: draft.phone, bio: draft.bio,
        identityVisibility: draft.identityVisibility,
        ...(granted.has('trainer') ? { trainerHeadline: draft.trainerHeadline, specialisations: draft.specialisations } : {}),
        ...(granted.has('creator') ? { creatorHeadline: draft.creatorHeadline, expertiseTags: draft.expertiseTags } : {}),
      });
      refreshUser(updated);
      onClose(true);
    } catch (e) {
      setError(e.message || 'Profile could not be saved.');
    } finally {
      setSaving(false);
    }
  }

  return (
      <Modal open title="Edit Profile" onClose={() => !saving && onClose(false)} width={640}>
        {draft ? (
          <>
            <div className="grid-2">
              <AccountField id="account-fullName" label="Full Name" value={draft.fullName} onChange={(fullName) => setDraft({ ...draft, fullName })} />
              <AccountField id="account-displayName" label="Display name" value={draft.displayName} onChange={(displayName) => setDraft({ ...draft, displayName })} />
              <AccountField id="account-phone" label="Phone" value={draft.phone} onChange={(phone) => setDraft({ ...draft, phone })} />
              <AccountField id="account-bio" label="Bio" value={draft.bio} onChange={(bio) => setDraft({ ...draft, bio })} />
              {granted.has('trainer') ? (
                <>
                  <AccountField id="account-trainerHeadline" label="Trainer headline" value={draft.trainerHeadline} onChange={(trainerHeadline) => setDraft({ ...draft, trainerHeadline })} />
                  <AccountField id="account-specialisations" label="Specialisations" value={draft.specialisations} onChange={(specialisations) => setDraft({ ...draft, specialisations })} />
                </>
              ) : null}
              {granted.has('creator') ? (
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
              <button type="button" className="btn btn-ghost" onClick={() => onClose(false)} disabled={saving}>Cancel</button>
              <button type="button" className="btn btn-primary" onClick={save} disabled={saving}>{saving ? 'Saving…' : 'Save Changes'}</button>
            </div>
          </>
        ) : null}
      </Modal>
  );
}

export function ProfileDetails({ user }) {
  const roles = roleSet(user);
  return (<>
    <div className="masked-field" style={{ marginTop: 12 }}><div className="label">Bio</div><div className="value">{user?.bio || 'No bio yet.'}</div></div>
    {roles.has('trainer') && <><div className="masked-field"><div className="label">Trainer headline</div><div className="value">{user?.trainerHeadline || '—'}</div></div><div className="masked-field"><div className="label">Specialisations</div><div className="value">{user?.specialisations || '—'}</div></div></>}
    {roles.has('creator') && <><div className="masked-field"><div className="label">Creator headline</div><div className="value">{user?.creatorHeadline || '—'}</div></div><div className="masked-field"><div className="label">Expertise tags</div><div className="value">{user?.expertiseTags || '—'}</div></div></>}
  </>);
}

export function IdentityVisibility({ user }) {
  const granted = roleSet(user);
  return IDENTITY_ROWS.map((row) => {
    const hasRole = granted.has(row.key);
    return <div key={row.key} style={{ display: 'flex', marginBottom: 8, fontSize: 13, gap: 12 }}>
      <span style={{ minWidth: 72, fontWeight: 600 }}>{row.label}</span>
      <span style={{ flex: 1, color: 'var(--slate)' }}>{hasRole ? row.grantedHint : 'Not activated'}</span>
      <span className={`pill${hasRole && isVisible(user, row.key) ? '' : ' muted'}`}>{!hasRole ? 'Hidden / pending' : isVisible(user, row.key) ? 'Visible' : 'Hidden'}</span>
    </div>;
  });
}
