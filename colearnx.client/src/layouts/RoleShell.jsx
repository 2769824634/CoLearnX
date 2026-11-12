import { Outlet, useLocation } from 'react-router-dom';
import MemberNotifications from '../components/MemberNotifications';
import { MemberNotificationsProvider } from '../components/MemberNotificationsProvider';
import { useAuth } from '../auth/AuthContext';
import useAdminAuth from '../auth/useAdminAuth';
import { AdminPendingProvider } from '../pages/admin/AdminPendingProvider';
import { pendingTotal, useAdminPending } from '../pages/admin/adminPending';
import { ShellChrome, ShellNavLink } from './ShellChrome';
import '../styles/admin.css';
import '../styles/admin-operations.css';
import '../styles/later-phase.css';
import '../styles/admin-v10.css';
import '../styles/trainer-v10.css';
import '../styles/creator-v10.css';

const NAV = {
  trainer: [
    { to: '/trainer/home', label: 'Homepage' },
    { to: '/trainer/courses', label: 'Course' },
    { to: '/trainer/invitations', label: 'Teaching invitations' },
    { to: '/trainer/earnings', label: 'Earnings' },
    { to: '/trainer/account', label: 'My Account' },
  ],
  creator: [
    { to: '/creator/home', label: 'Home' },
    { to: '/creator/courses', label: 'Courses', match: 'courses' },
    { to: '/creator/courses/new', label: 'Create Course', end: true },
    { to: '/creator/approvals', label: 'Approval status' },
    { to: '/creator/usage', label: 'Usage Records' },
    { to: '/creator/earnings', label: 'Earnings' },
    { to: '/creator/account', label: 'My Account' },
  ],
  admin: [
    { to: '/admin/home', label: 'Home' },
    { to: '/admin/approvals', label: 'Approvals', pending: 'approvals' },
    { to: '/admin/users', label: 'Users' },
    { to: '/admin/classes', label: 'Intakes' },
    { to: '/admin/pricing', label: 'Pricing' },
    { to: '/admin/learning-paths', label: 'Learning paths' },
    { to: '/admin/audit', label: 'Audit Log' },
    { to: '/admin/account', label: 'My Account' },
  ],
};

function isNavActive(item, pathname, isActive) {
  if (item.match === 'courses') {
    return pathname === '/creator/courses' || /^\/creator\/courses\/\d+(\/intakes\/(?:\d+|new))?$/.test(pathname);
  }
  if (item.to === '/creator/courses/new') return pathname === item.to;
  return isActive;
}

function pendingCount(kind, counts) {
  if (!kind || !counts) return 0;
  return kind === 'approvals' ? pendingTotal(counts) : 0;
}

function ShellFrame({ identityName, avatarUrl, token, role, logout, identityLabel = 'Administrator', title, subtitle }) {
  const location = useLocation();
  const items = NAV[role] || [];
  const pendingCounts = useAdminPending()?.counts;

  return (
    <ShellChrome
      shellClassName={`shell${role === 'admin' ? ' admin-shell' : ''}${role === 'trainer' ? ' trainer-shell' : ''}${role === 'creator' ? ' creator-shell' : ''}`}
      role={role}
      identityName={identityName}
      avatarUrl={avatarUrl}
      token={token}
      logout={logout}
      extraActions={role === 'trainer' || role === 'creator' ? <MemberNotifications /> : null}
      identityMeta={<div className="role">{identityLabel}</div>}
      navLabel={`${role} navigation`}
      navHintId={`${role}-nav-scroll-hint`}
      navItems={items.map((item) => {
        const pending = pendingCount(item.pending, pendingCounts);
        return (
          <ShellNavLink
            key={item.to}
            to={item.to}
            end={Boolean(item.end)}
            ariaLabel={pending > 0 ? `${item.label}, ${pending} pending` : undefined}
            className={({ isActive }) => `nav-item${isNavActive(item, location.pathname, isActive) ? ' active' : ''}`}
          >
            {item.label}
            {pending > 0 ? <span className="admin-nav-count" aria-hidden="true">{pending > 99 ? '99+' : pending}</span> : null}
          </ShellNavLink>
        );
      })}
      title={title}
      subtitle={subtitle}
    >
      <Outlet />
    </ShellChrome>
  );
}

export default function RoleShell({ role, title, subtitle }) {
  const { user, token, logout } = useAuth();
  return (
    <MemberNotificationsProvider>
    <ShellFrame
      identityName={user?.fullName}
      avatarUrl={user?.avatarUrl}
      token={token}
      role={role}
      logout={logout}
      identityLabel={role.charAt(0).toUpperCase() + role.slice(1)}
      title={title}
      subtitle={subtitle}
    />
    </MemberNotificationsProvider>
  );
}

export function AdminRoleShell({ title, subtitle }) {
  const { admin, token, logout } = useAdminAuth();
  return (
    <AdminPendingProvider token={token}>
      <ShellFrame
        identityName={admin?.email}
        role="admin"
        logout={logout}
        title={title}
        subtitle={subtitle}
      />
    </AdminPendingProvider>
  );
}
