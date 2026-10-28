import { useEffect, useRef, useState } from 'react';
import { Link, NavLink } from 'react-router-dom';
import Logo from '../components/Logo';
import SkipToContent, { MAIN_CONTENT_ID } from '../components/SkipToContent';
import useActiveNavInView from '../components/useActiveNavInView';
import UserAvatar from '../components/UserAvatar';
import { AccessNotice } from './shellAccessNotice';
import '../styles/account-menu.css';

function AccountMenu({ role, identityName, avatarUrl, token, logout }) {
  const [open, setOpen] = useState(false);
  const menuRef = useRef(null);

  useEffect(() => {
    if (!open) return undefined;
    function onPointerDown(event) {
      if (!menuRef.current?.contains(event.target)) setOpen(false);
    }
    function onKeyDown(event) {
      if (event.key === 'Escape') setOpen(false);
    }
    document.addEventListener('pointerdown', onPointerDown);
    document.addEventListener('keydown', onKeyDown);
    return () => {
      document.removeEventListener('pointerdown', onPointerDown);
      document.removeEventListener('keydown', onKeyDown);
    };
  }, [open]);

  const accountPath = `/${role}/account`;
  return (
    <div className="account-menu" ref={menuRef} onBlur={(event) => {
      if (!event.currentTarget.contains(event.relatedTarget)) setOpen(false);
    }}>
      <button type="button" className="account-menu-trigger" aria-label="Account menu" aria-expanded={open} onClick={() => setOpen((value) => !value)}>
        <UserAvatar name={identityName} avatarUrl={avatarUrl} token={token} />
      </button>
      {open ? (
        <div className="account-menu-panel" aria-label="Account shortcuts">
          <Link to="/forgot-password" onClick={() => setOpen(false)}>Change password</Link>
          <Link to={`${accountPath}#edit-profile`} onClick={() => setOpen(false)}>Edit Profile</Link>
          <Link to={accountPath} onClick={() => setOpen(false)}>My Account</Link>
          <Link to={`${accountPath}#applications`} onClick={() => setOpen(false)}>Identity applications &amp; status</Link>
          <button type="button" onClick={() => { setOpen(false); logout(); }}>Log out</button>
        </div>
      ) : null}
    </div>
  );
}

export function ShellChrome({
  shellClassName = 'shell',
  role,
  identityName,
  avatarUrl,
  token,
  logout,
  search = null,
  extraActions = null,
  identityMeta = null,
  navLabel,
  navHintId,
  navItems,
  children,
  title,
  subtitle,
  accessNotice,
  onDismissAccessNotice,
}) {
  const navRef = useActiveNavInView();

  return (
    <div className={shellClassName}>
      <SkipToContent />
      <header className="topbar">
        <div className="topbar-brand">
          <Logo />
        </div>
        <div className={`topbar-search${search ? ' search' : ''}`}>{search}</div>
        <div className="topbar-actions">
          {extraActions}
          {!['member', 'trainer', 'creator'].includes(role) ? <button type="button" className="btn btn-ghost" onClick={logout}>Log out</button> : null}
          <div className="user-chip" data-role={role || undefined}>
            {['member', 'trainer', 'creator'].includes(role)
              ? <AccountMenu role={role} identityName={identityName} avatarUrl={avatarUrl} token={token} logout={logout} />
              : <UserAvatar name={identityName} avatarUrl={avatarUrl} token={token} />}
            <div>
              <div className="user-chip-name">{identityName}</div>
              {identityMeta}
            </div>
          </div>
        </div>
      </header>
      <div className="shell-body">
        <nav ref={navRef} className="sidebar" aria-label={navLabel} aria-describedby={navHintId}>
          {navItems}
        </nav>
        <p id={navHintId} className="nav-scroll-hint">More navigation → Swipe or scroll sideways</p>
        <main id={MAIN_CONTENT_ID} className="content" tabIndex={-1}>
          <AccessNotice notice={accessNotice} onDismiss={onDismissAccessNotice} />
          {title ? <h1 className="page-title">{title}</h1> : null}
          {subtitle ? <p className="page-sub">{subtitle}</p> : null}
          {children}
        </main>
      </div>
    </div>
  );
}

export function ShellNavLink({ to, end, className, ariaLabel, children }) {
  return (
    <NavLink to={to} end={end} className={className} aria-label={ariaLabel}>
      {children}
    </NavLink>
  );
}
