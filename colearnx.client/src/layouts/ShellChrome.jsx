import { NavLink } from 'react-router-dom';
import Logo from '../components/Logo';
import UserAvatar from '../components/UserAvatar';
import { AccessNotice } from './shellAccessNotice';

export function ShellChrome({
  shellClassName = 'shell',
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
  return (
    <div className={shellClassName}>
      <div className="topbar">
        <div className="topbar-brand">
          <Logo />
        </div>
        <div className={`topbar-search${search ? ' search' : ''}`}>{search}</div>
        <div className="topbar-actions">
          {extraActions}
          <button type="button" className="btn btn-ghost" onClick={logout}>Log out</button>
          <div className="user-chip">
            <UserAvatar name={identityName} avatarUrl={avatarUrl} token={token} />
            <div>
              <div className="user-chip-name">{identityName}</div>
              {identityMeta}
            </div>
          </div>
        </div>
      </div>
      <div className="shell-body">
        <nav className="sidebar" aria-label={navLabel} aria-describedby={navHintId}>
          {navItems}
        </nav>
        <p id={navHintId} className="nav-scroll-hint">More navigation → Swipe or scroll sideways</p>
        <main className="content">
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
