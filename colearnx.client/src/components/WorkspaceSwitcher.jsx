import { useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';

const ROLE_META = {
  Member: { label: 'Member', desc: 'Learn & enrol' },
  Trainer: { label: 'Trainer', desc: 'Run courses' },
  Creator: { label: 'Creator', desc: 'Upload materials' },
};

// Switch active_role without re-login. Shared: WorkspaceSwitcher
export default function WorkspaceSwitcher() {
  const { user, activeRole, switchRole } = useAuth();
  const navigate = useNavigate();
  const [open, setOpen] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const rootRef = useRef(null);

  const roles = (user?.roles || []).filter((r) => ROLE_META[r]);
  const current = ROLE_META[user?.activeRole] || ROLE_META.Member;

  useEffect(() => {
    function onDocClick(e) {
      if (!rootRef.current?.contains(e.target)) setOpen(false);
    }
    document.addEventListener('mousedown', onDocClick);
    return () => document.removeEventListener('mousedown', onDocClick);
  }, []);

  if (roles.length <= 1) {
    return <div className="role">{current.label}</div>;
  }

  async function onPick(role) {
    if (role.toLowerCase() === activeRole || busy) return;
    setBusy(true);
    setError('');
    try {
      const next = await switchRole(role);
      setOpen(false);
      navigate(`/${next.activeRole.toLowerCase()}/home`);
    } catch (e) {
      setError(e.message || 'Switch failed');
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="workspace-switcher" ref={rootRef}>
      <button
        type="button"
        className="workspace-switcher-trigger"
        onClick={() => setOpen((v) => !v)}
        aria-expanded={open}
        aria-haspopup="listbox"
      >
        <span className="role">{current.label}</span>
        <span className="workspace-switcher-caret">▾</span>
      </button>
      {open ? (
        <div className="workspace-switcher-menu" role="listbox">
          <div className="workspace-switcher-title">Switch workspace</div>
          {roles.map((role) => {
            const meta = ROLE_META[role];
            const selected = role.toLowerCase() === activeRole;
            return (
              <button
                key={role}
                type="button"
                role="option"
                aria-selected={selected}
                className={`workspace-switcher-item${selected ? ' selected' : ''}`}
                disabled={busy}
                onClick={() => onPick(role)}
              >
                <strong>{meta.label}</strong>
                <span>{meta.desc}</span>
              </button>
            );
          })}
          {error ? <div className="workspace-switcher-error">{error}</div> : null}
        </div>
      ) : null}
    </div>
  );
}
