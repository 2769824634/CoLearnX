import { useId, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { usersApi } from '../../api';
import { useAuth } from '../../auth/AuthContext';
import Modal from '../../components/Modal';
import { COUNTRIES } from '../../data/countries';

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

function AccountField({ id, label, value, onChange, maxLength }) {
  return (
    <div className="form-group">
      <label htmlFor={id}>{label}</label>
      <input id={id} value={value} maxLength={maxLength} onChange={(e) => onChange(e.target.value)} />
    </div>
  );
}

function profileDraft(user) {
  return {
    fullName: user?.fullName ?? '',
    displayName: user?.displayName ?? '',
    phone: user?.phone ?? '',
    bio: user?.bio ?? '',
    country: user?.country ?? '',
    occupation: user?.occupation || 'unknown',
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
  const [activeSection, setActiveSection] = useState('personal');
  const tabsId = useId();
  const tabRefs = useRef({});
  const granted = roleSet(user);
  const sections = [
    { key: 'personal', label: 'Personal info' },
    ...IDENTITY_ROWS.filter((row) => row.key !== 'member' && granted.has(row.key)),
  ];

  function navigateTabs(event, index) {
    let next;
    if (event.key === 'ArrowRight') next = (index + 1) % sections.length;
    else if (event.key === 'ArrowLeft') next = (index - 1 + sections.length) % sections.length;
    else if (event.key === 'Home') next = 0;
    else if (event.key === 'End') next = sections.length - 1;
    else return;
    event.preventDefault();
    const key = sections[next].key;
    setActiveSection(key);
    tabRefs.current[key]?.focus();
  }

  async function save() {
    if (!draft || saving) return;
    setSaving(true);
    setError('');
    try {
      const updated = await usersApi.update(user.id, {
        fullName: draft.fullName, displayName: draft.displayName, phone: draft.phone, bio: draft.bio, country: draft.country, occupation: draft.occupation,
        identityVisibility: Object.fromEntries(
          IDENTITY_ROWS.filter((row) => granted.has(row.key)).map((row) => [row.key, draft.identityVisibility[row.key]]),
        ),
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

  return createPortal(
      <Modal open title="Edit Profile" onClose={() => !saving && onClose(false)} width={640}>
        {draft ? (
          <>
            <div className="tabs" role="tablist" aria-label="Profile sections">
              {sections.map((section, index) => (
                <button key={section.key} type="button" role="tab"
                  id={`${tabsId}-tab-${section.key}`} aria-controls={`${tabsId}-panel-${section.key}`}
                  aria-selected={activeSection === section.key} tabIndex={activeSection === section.key ? 0 : -1}
                  className={`tab${activeSection === section.key ? ' active' : ''}`}
                  ref={(element) => { tabRefs.current[section.key] = element; }}
                  onClick={() => setActiveSection(section.key)} onKeyDown={(event) => navigateTabs(event, index)}>
                  {section.label}
                </button>
              ))}
            </div>
            {sections.map((section) => {
              const visibilityKey = section.key === 'personal' ? 'member' : section.key;
              const visibilityRow = IDENTITY_ROWS.find((row) => row.key === visibilityKey && granted.has(row.key));
              return (
                <section key={section.key} role="tabpanel" id={`${tabsId}-panel-${section.key}`}
                  aria-labelledby={`${tabsId}-tab-${section.key}`} hidden={activeSection !== section.key}>
                  {activeSection === section.key && <>
                    <div className="grid-2">
                      {section.key === 'personal' && <>
                        <AccountField id="account-fullName" label="Full Name" value={draft.fullName} onChange={(fullName) => setDraft({ ...draft, fullName })} />
                        <AccountField id="account-displayName" label="Display name" value={draft.displayName} onChange={(displayName) => setDraft({ ...draft, displayName })} />
                        <div className="form-group">
                          <label htmlFor="account-email">Email</label>
                          <input id="account-email" type="email" value={user?.email ?? ''} disabled />
                        </div>
                        <div className="form-group">
                          <label htmlFor="account-country">Country / Region</label>
                          <input id="account-country" list="account-countries" maxLength={120} value={draft.country} onChange={(event) => setDraft({ ...draft, country: event.target.value })} />
                          <datalist id="account-countries">{COUNTRIES.map((name) => <option key={name} value={name} />)}</datalist>
                        </div>
                        <AccountField id="account-phone" label="Phone" value={draft.phone} onChange={(phone) => setDraft({ ...draft, phone })} />
                        <AccountField id="account-occupation" label="Occupation" maxLength={120} value={draft.occupation} onChange={(occupation) => setDraft({ ...draft, occupation })} />
                        <AccountField id="account-bio" label="Bio" value={draft.bio} onChange={(bio) => setDraft({ ...draft, bio })} />
                      </>}
                      {section.key === 'trainer' && <>
                        <AccountField id="account-trainerHeadline" label="Trainer headline" value={draft.trainerHeadline} onChange={(trainerHeadline) => setDraft({ ...draft, trainerHeadline })} />
                        <AccountField id="account-specialisations" label="Specialisations" value={draft.specialisations} onChange={(specialisations) => setDraft({ ...draft, specialisations })} />
                      </>}
                      {section.key === 'creator' && <>
                        <AccountField id="account-creatorHeadline" label="Creator headline" value={draft.creatorHeadline} onChange={(creatorHeadline) => setDraft({ ...draft, creatorHeadline })} />
                        <AccountField id="account-expertiseTags" label="Expertise tags" value={draft.expertiseTags} onChange={(expertiseTags) => setDraft({ ...draft, expertiseTags })} />
                      </>}
                    </div>
                    {visibilityRow && <div className="identity-block">
                      <div className="ib-head">
                        <span className="grow">{visibilityRow.label} visibility</span>
                        <button type="button" className={`switch${draft.identityVisibility[visibilityKey] ? ' on' : ''}`}
                          aria-pressed={draft.identityVisibility[visibilityKey]} aria-label={`${visibilityRow.label} visibility`}
                          onClick={() => setDraft({ ...draft, identityVisibility: { ...draft.identityVisibility, [visibilityKey]: !draft.identityVisibility[visibilityKey] } })} />
                      </div>
                    </div>}
                  </>}
                </section>
              );
            })}
            {error ? <p className="trainer-error" role="alert">{error}</p> : null}
            <div className="modal-actions">
              <button type="button" className="btn btn-ghost" onClick={() => onClose(false)} disabled={saving}>Cancel</button>
              <button type="button" className="btn btn-primary" onClick={save} disabled={saving}>{saving ? 'Saving…' : 'Save Changes'}</button>
            </div>
          </>
        ) : null}
      </Modal>,
      document.body,
  );
}

export function ProfileDetails({ user }) {
  const roles = roleSet(user);
  return (<>
    <div className="masked-field" style={{ marginTop: 12 }}><div className="label">Country / Region</div><div className="value">{user?.country || '—'}</div></div>
    <div className="masked-field"><div className="label">Occupation</div><div className="value">{user?.occupation || 'unknown'}</div></div>
    <div className="masked-field"><div className="label">Bio</div><div className="value">{user?.bio || 'No bio yet.'}</div></div>
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
