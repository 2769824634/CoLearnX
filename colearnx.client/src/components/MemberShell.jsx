import { useEffect, useRef, useState } from 'react';
import { NavLink, useLocation, useNavigate } from 'react-router-dom';
import Logo from './Logo';
import WorkspaceSwitcher from './WorkspaceSwitcher';
import MemberNotifications from './MemberNotifications';
import UserAvatar from './UserAvatar';
import { MEMBER_NAV } from '../data/memberMock';
import { useAuth } from '../auth/AuthContext';

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

// Member chrome (sidebar + topbar).
export default function MemberShell({
  title,
  subtitle,
  onSearch,
  children,
}) {
  const { user, token, logout } = useAuth();
  const { notice: accessNotice, dismiss: dismissAccessNotice } = useAccessNotice();

  return (
    <div className="shell member-shell">
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
          <NavLink to="/member/payment" className="member-wallet" aria-label="Credit wallet">
            <span><strong>{user?.creditBalance ?? '—'}</strong> Available credits</span>
            <small>On hold: {user?.heldCredits ?? '—'}</small>
          </NavLink>
          <MemberNotifications />
          <button type="button" className="btn btn-ghost" onClick={logout}>
            Log out
          </button>
          <div className="user-chip">
            <UserAvatar name={user?.fullName} avatarUrl={user?.avatarUrl} token={token} />
            <div>
              <div className="user-chip-name">{user?.fullName}</div>
              <WorkspaceSwitcher />
            </div>
          </div>
        </div>
      </div>
      <div className="shell-body">
        <nav className="sidebar" aria-label="Member navigation" aria-describedby="member-nav-scroll-hint">
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
        <p id="member-nav-scroll-hint" className="nav-scroll-hint">More navigation → Swipe or scroll sideways</p>
        <main className="content">
          <AccessNotice notice={accessNotice} onDismiss={dismissAccessNotice} />
          <h1 className="page-title">{title}</h1>
          {subtitle ? <p className="page-sub">{subtitle}</p> : null}
          {children}
        </main>
      </div>
    </div>
  );
}
