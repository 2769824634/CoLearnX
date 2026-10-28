import { Outlet, useLocation } from 'react-router-dom';
import WorkspaceSwitcher from '../components/WorkspaceSwitcher';
import MemberNotifications from '../components/MemberNotifications';
import { MemberNotificationsProvider } from '../components/MemberNotificationsProvider';
import { useAuth } from '../auth/AuthContext';
import useAdminAuth from '../auth/useAdminAuth';
import useStaffAuth from '../auth/useStaffAuth';
import { AdminPendingProvider } from '../pages/admin/AdminPendingProvider';
import { approvalTotal, useAdminPending } from '../pages/admin/adminPending';
import { ShellChrome, ShellNavLink } from './ShellChrome';
import { useAccessNotice } from './useAccessNotice';
import '../styles/admin.css';
import '../styles/admin-operations.css';
import '../styles/later-phase.css';

const NAV = {
  trainer: [
    { to: '/trainer/home', label: 'Homepage' },
    { to: '/trainer/courses', label: 'Course' },
    { to: '/trainer/invitations', label: 'Teaching invitations' },
    { to: '/trainer/account', label: 'My Account' },
  ],
  creator: [
    { to: '/creator/home', label: 'Home' },
    { to: '/creator/courses', label: 'Courses', match: 'courses' },
    { to: '/creator/courses/new', label: 'Create Course', end: true },
    { to: '/creator/approvals', label: 'Approval status' },
    { to: '/creator/intakes', label: 'Classes', match: 'intakes' },
    { to: '/creator/usage', label: 'Usage Records' },
    { to: '/creator/account', label: 'My Account' },
  ],
  admin: [
    { to: '/admin/home', label: 'Homepage' },
    { to: '/admin/approvals', label: 'Approvals', pending: 'approvals' },
    { to: '/admin/users', label: 'Users' },
    { to: '/admin/staff', label: 'Staff' },
    { to: '/admin/ledger', label: 'Credit Ledger' },
    { to: '/admin/disputes', label: 'Disputes', pending: 'disputes' },
    { to: '/admin/pricing', label: 'Pricing' },
    { to: '/admin/audit', label: 'Audit Log' },
    { to: '/admin/account', label: 'My Account' },
  ],
  staff: [
    { to: '/staff/home', label: 'Homepage' },
    { to: '/staff/courses', label: 'Platform courses' },
    { to: '/staff/intakes', label: 'Classes' },
  ],
};

function isNavActive(item, pathname, isActive) {
  if (item.match === 'courses') {
    return pathname === '/creator/courses' || /^\/creator\/courses\/\d+$/.test(pathname);
  }
  if (item.to === '/creator/courses/new') return pathname === item.to;
  if (item.match === 'intakes') return pathname === '/creator/intakes' || pathname.startsWith('/creator/intakes/');
  return isActive;
}

function pendingCount(kind, counts) {
  if (!kind || !counts) return 0;
  return kind === 'disputes' ? counts.disputes : approvalTotal(counts);
}

function ShellFrame({ identityName, avatarUrl, token, role, logout, showWorkspaceSwitcher = false, identityLabel = 'Administrator', title, subtitle, accessNotice, onDismissAccessNotice }) {
  const location = useLocation();
  const items = NAV[role] || [];
  const pendingCounts = useAdminPending()?.counts;

  return (
    <ShellChrome
      shellClassName={`shell${role === 'admin' ? ' admin-shell' : ''}`}
      role={role}
      identityName={identityName}
      avatarUrl={avatarUrl}
      token={token}
      logout={logout}
      extraActions={role === 'trainer' || role === 'creator' ? <MemberNotifications /> : null}
      identityMeta={showWorkspaceSwitcher ? <WorkspaceSwitcher /> : <div className="role">{identityLabel}</div>}
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

export function StaffRoleShell({ title, subtitle }) {
  const { staff, token, logout } = useStaffAuth();
  return <ShellFrame
    identityName={staff?.displayName}
    token={token}
    role="staff"
    logout={logout}
    identityLabel="Platform staff"
    title={title}
    subtitle={subtitle}
  />;
}
