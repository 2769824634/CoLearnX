import { Outlet, useLocation } from 'react-router-dom';
import WorkspaceSwitcher from '../components/WorkspaceSwitcher';
import MemberNotifications from '../components/MemberNotifications';
import { MemberNotificationsProvider } from '../components/MemberNotificationsProvider';
import { useAuth } from '../auth/AuthContext';
import useAdminAuth from '../auth/useAdminAuth';
import { ShellChrome, ShellNavLink } from './ShellChrome';
import { useAccessNotice } from './shellAccessNotice';
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
    { to: '/creator/courses', label: 'Courses', match: 'courses' },
    { to: '/creator/courses/new', label: 'Create Course', end: true },
    { to: '/creator/courses/intake-applications', label: 'Session approvals' },
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

function isNavActive(item, pathname, isActive) {
  if (item.match === 'courses') {
    return pathname === '/creator/courses' || /^\/creator\/courses\/\d+$/.test(pathname);
  }
  if (item.to === '/creator/courses/new') return pathname === item.to;
  if (item.to === '/creator/courses/intake-applications') {
    return pathname === item.to || pathname.startsWith(`${item.to}/`);
  }
  return isActive;
}

function ShellFrame({ identityName, avatarUrl, token, role, logout, showWorkspaceSwitcher = false, title, subtitle, accessNotice, onDismissAccessNotice }) {
  const location = useLocation();
  const items = NAV[role] || [];

  return (
    <ShellChrome
      shellClassName={`shell${role === 'admin' ? ' admin-shell' : ''}`}
      identityName={identityName}
      avatarUrl={avatarUrl}
      token={token}
      logout={logout}
      extraActions={role === 'trainer' || role === 'creator' ? <MemberNotifications /> : null}
      identityMeta={showWorkspaceSwitcher ? <WorkspaceSwitcher /> : <div className="role">Administrator</div>}
      navLabel={`${role} navigation`}
      navHintId={`${role}-nav-scroll-hint`}
      navItems={items.map((item) => (
        <ShellNavLink
          key={item.to}
          to={item.to}
          end={Boolean(item.end)}
          className={({ isActive }) => `nav-item${isNavActive(item, location.pathname, isActive) ? ' active' : ''}`}
        >
          {item.label}
        </ShellNavLink>
      ))}
      title={title}
      subtitle={subtitle}
      accessNotice={accessNotice}
      onDismissAccessNotice={onDismissAccessNotice}
    >
      <Outlet />
    </ShellChrome>
  );
}

export default function RoleShell({ role, title, subtitle }) {
  const { user, token, logout } = useAuth();
  const { notice: accessNotice, dismiss: dismissAccessNotice } = useAccessNotice();
  return (
    <MemberNotificationsProvider>
    <ShellFrame
      identityName={user?.fullName}
      avatarUrl={user?.avatarUrl}
      token={token}
      role={role}
      logout={logout}
      showWorkspaceSwitcher
      title={title}
      subtitle={subtitle}
      accessNotice={accessNotice}
      onDismissAccessNotice={dismissAccessNotice}
    />
    </MemberNotificationsProvider>
  );
}

export function AdminRoleShell({ title, subtitle }) {
  const { admin, logout } = useAdminAuth();
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
