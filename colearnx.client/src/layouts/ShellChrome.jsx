import { NavLink } from 'react-router-dom';
import Logo from '../components/Logo';
import SkipToContent, { MAIN_CONTENT_ID } from '../components/SkipToContent';
import useActiveNavInView from '../components/useActiveNavInView';
import UserAvatar from '../components/UserAvatar';
import { AccessNotice } from './shellAccessNotice';

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
          <button type="button" className="btn btn-ghost" onClick={logout}>Log out</button>
          <div className="user-chip" data-role={role || undefined}>
            <UserAvatar name={identityName} avatarUrl={avatarUrl} token={token} />
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

export function ShellNavLink({ to, end, className, children }) {
  return (
    <NavLink to={to} end={end} className={className}>
      {children}
    </NavLink>
  );
}
