import { NavLink } from 'react-router-dom';
import Logo from './Logo';
import WorkspaceSwitcher from './WorkspaceSwitcher';
import { MEMBER_NAV } from '../data/memberMock';
import { useAuth } from '../auth/AuthContext';

// Member chrome (sidebar + topbar).
export default function MemberShell({
  title,
  subtitle,
  onSearch,
  onNotify,
  children,
}) {
  const { user, logout } = useAuth();

  return (
    <div className="shell">
      <div className="topbar">
        <div className="topbar-brand">
          <Logo />
        </div>
        <div className="topbar-search search">
          <input
            type="search"
            placeholder="Search training programs..."
            onKeyDown={(e) => {
              if (e.key === 'Enter' && e.target.value.trim()) {
                onSearch?.(e.target.value.trim());
              }
            }}
          />
        </div>
        <div className="topbar-actions">
          <button type="button" className="notif-badge" onClick={onNotify} aria-label="Notifications" />
          <button type="button" className="btn btn-ghost" onClick={logout}>
            Log out
          </button>
          <div className="user-chip">
            <div className="avatar" />
            <div>
              <div className="user-chip-name">{user?.fullName}</div>
              <WorkspaceSwitcher />
            </div>
          </div>
        </div>
      </div>
      <div className="shell-body">
        <nav className="sidebar" aria-label="Member navigation">
          {MEMBER_NAV.map((item) => (
            <NavLink
              key={item.id}
              to={`/member/${item.id === 'catalog' ? 'courses' : item.id === 'my-programs' ? 'programs' : item.id === 'profile' ? 'account' : item.id}`}
              className={({ isActive }) => `nav-item${isActive ? ' active' : ''}`}
              end={item.id === 'home'}
            >
              {item.label}
            </NavLink>
          ))}
        </nav>
        <main className="content">
          <h1 className="page-title">{title}</h1>
          {subtitle ? <p className="page-sub">{subtitle}</p> : null}
          {children}
        </main>
      </div>
    </div>
  );
}
