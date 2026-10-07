import { useEffect, useRef, useState } from 'react';
import { NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom';
import Logo from '../components/Logo';
import WorkspaceSwitcher from '../components/WorkspaceSwitcher';
import UserAvatar from '../components/UserAvatar';
import MemberNotifications from '../components/MemberNotifications';
import SkipToContent, { MAIN_CONTENT_ID } from '../components/SkipToContent';
import useActiveNavInView from '../components/useActiveNavInView';
import { MemberNotificationsProvider } from '../components/MemberNotificationsProvider';
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

function useAccessNotice() {
  const location = useLocation();
  const navigate = useNavigate();
  const initialNotice = location.state?.accessNotice || '';
  const [notice, setNotice] = useState(initialNotice);
  const noticeRef = useRef(initialNotice);
  const originPathRef = useRef(initialNotice ? `${location.pathname}${location.search}${location.hash}` : null);
  const replacedRef = useRef(false);

  useEffect(() => {
    const incomingNotice = location.state?.accessNotice || '';

    if (incomingNotice && incomingNotice !== noticeRef.current) {
      noticeRef.current = incomingNotice;
      originPathRef.current = `${location.pathname}${location.search}${location.hash}`;
      replacedRef.current = false;
      queueMicrotask(() => setNotice(incomingNotice));
      return;
    }

    if (incomingNotice && !replacedRef.current) {
      replacedRef.current = true;
      navigate(`${location.pathname}${location.search}${location.hash}`, { replace: true, state: null });
      return;
    }

    if (!incomingNotice && replacedRef.current) {
      replacedRef.current = false;
      return;
    }

    const currentPath = `${location.pathname}${location.search}${location.hash}`;
    if (!incomingNotice && noticeRef.current && originPathRef.current !== currentPath) {
      noticeRef.current = '';
      originPathRef.current = null;
      queueMicrotask(() => setNotice(''));
    }
  }, [location, navigate, notice]);

  const dismiss = () => {
    noticeRef.current = '';
    originPathRef.current = null;
    setNotice('');
  };

  return { notice, dismiss };
}

function AccessNotice({ notice, onDismiss }) {
  if (!notice) return null;
  return (
    <div className="callout warn" role="status" aria-label="Access notice">
      <div className="callout-title">Workspace access</div>
      <div>{notice}</div>
      <button type="button" className="btn btn-ghost" onClick={onDismiss}>Dismiss</button>
    </div>
  );
}

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
  const navRef = useActiveNavInView();

  return (
    <div className={`shell${role === 'admin' ? ' admin-shell' : ''}`}>
      <SkipToContent />
      <header className="topbar">
        <div className="topbar-brand">
          <Logo />
        </div>
        <div className="topbar-search" />
        <div className="topbar-actions">
          {role === 'trainer' || role === 'creator' ? <MemberNotifications /> : null}
          <button type="button" className="btn btn-ghost" onClick={logout}>
            Log out
          </button>
          <div className="user-chip" data-role={role}>
            <UserAvatar name={identityName} avatarUrl={avatarUrl} token={token} />
            <div>
              <div className="user-chip-name">{identityName}</div>
              {showWorkspaceSwitcher ? <WorkspaceSwitcher /> : <div className="role">Administrator</div>}
            </div>
          </div>
        </div>
      </header>
      <div className="shell-body">
        <nav ref={navRef} className="sidebar" aria-label={`${role} navigation`} aria-describedby={`${role}-nav-scroll-hint`}>
          {items.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              end={Boolean(item.end)}
              className={({ isActive }) => `nav-item${isNavActive(item, location.pathname, isActive) ? ' active' : ''}`}
            >
              {item.label}
            </NavLink>
          ))}
        </nav>
        <p id={`${role}-nav-scroll-hint`} className="nav-scroll-hint">More navigation → Swipe or scroll sideways</p>
        <main id={MAIN_CONTENT_ID} className="content" tabIndex={-1}>
          <AccessNotice notice={accessNotice} onDismiss={onDismissAccessNotice} />
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
