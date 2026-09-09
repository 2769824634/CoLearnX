import { useEffect } from 'react';
import { NavLink, Outlet } from 'react-router-dom';
import Logo from '../components/Logo';
import WorkspaceSwitcher from '../components/WorkspaceSwitcher';
import { useAuth } from '../auth/AuthContext';
import useAdminAuth from '../auth/useAdminAuth';
import '../styles/admin.css';
import '../styles/admin-operations.css';
import '../styles/later-phase.css';

const NAV = {
  trainer: [
    { to: '/trainer/home', label: 'Homepage' },
    { to: '/trainer/courses', label: 'Course' },
    { to: '/trainer/attendance', label: 'Attendance' },
    { to: '/trainer/learners', label: 'Learner List' },
    { to: '/trainer/account', label: 'My Account' },
  ],
  creator: [
    { to: '/creator/home', label: 'Home' },
    { to: '/creator/courses', label: 'Courses' },
    { to: '/creator/upload', label: 'Upload Material' },
    { to: '/creator/usage', label: 'Usage Records' },
    { to: '/creator/account', label: 'My Account' },
  ],
  admin: [
    { to: '/admin/home', label: 'Homepage' },
    { to: '/admin/approvals', label: 'Approvals' },
    { to: '/admin/users', label: 'Users' },
    { to: '/admin/ledger', label: 'Credit Ledger' },
    { to: '/admin/disputes', label: 'Disputes' },
    { to: '/admin/audit', label: 'Audit Log' },
    { to: '/admin/account', label: 'My Account' },
  ],
};

function ShellFrame({ identityName, role, logout, showWorkspaceSwitcher = false, title, subtitle }) {
  const items = NAV[role] || [];

  return (
    <div className={`shell${role === 'admin' ? ' admin-shell' : ''}`}>
      <div className="topbar">
        <div className="topbar-brand">
          <Logo />
        </div>
        <div className="topbar-search" />
        <div className="topbar-actions">
          <button type="button" className="btn btn-ghost" onClick={logout}>
            Log out
          </button>
          <div className="user-chip">
            <div className="avatar" />
            <div>
              <div className="user-chip-name">{identityName}</div>
              {showWorkspaceSwitcher ? <WorkspaceSwitcher /> : <div className="role">Administrator</div>}
            </div>
          </div>
        </div>
      </div>
      <div className="shell-body">
        <nav className="sidebar">
          {items.map((item) => item.unavailable ? (
            <span key={item.to} className="nav-item admin-nav-unavailable" aria-disabled="true">
              {item.label}<small>Coming later</small>
            </span>
          ) : (
            <NavLink
              key={item.to}
              to={item.to}
              className={({ isActive }) => `nav-item${isActive ? ' active' : ''}`}
            >
              {item.label}
            </NavLink>
          ))}
        </nav>
        <main className="content">
          {title ? <h1 className="page-title">{title}</h1> : null}
          {subtitle ? <p className="page-sub">{subtitle}</p> : null}
          <Outlet />
        </main>
      </div>
    </div>
  );
}

// Trainer / Creator shell. Shared: RoleShell
export default function RoleShell({ role, title, subtitle }) {
  const { user, logout } = useAuth();
  return (
    <ShellFrame
      identityName={user?.fullName}
      role={role}
      logout={logout}
      showWorkspaceSwitcher
      title={title}
      subtitle={subtitle}
    />
  );
}

export function AdminRoleShell({ title, subtitle }) {
  const { admin, logout } = useAdminAuth();
  useEffect(() => {
    const previousTitle = document.title;
    document.title = 'CoLearnX — Administration';
    return () => { document.title = previousTitle; };
  }, []);
  return (
    <ShellFrame
      identityName={admin?.email}
      role="admin"
      logout={logout}
      title={title}
      subtitle={subtitle}
    />
  );
}

export function RolePlaceholder({ heading, body }) {
  return (
    <div className="callout info">
      <div className="callout-title">{heading}</div>
      {body}
    </div>
  );
}
