import { NavLink } from 'react-router-dom';
import WorkspaceSwitcher from './WorkspaceSwitcher';
import MemberNotifications from './MemberNotifications';
import { MEMBER_NAV } from '../data/memberMock';
import { useAuth } from '../auth/AuthContext';
import { ShellChrome, ShellNavLink } from '../layouts/ShellChrome';
import { useAccessNotice } from '../layouts/shellAccessNotice';

export default function MemberShell({
  title,
  subtitle,
  onSearch,
  children,
}) {
  const { user, token, logout } = useAuth();
  const { notice: accessNotice, dismiss: dismissAccessNotice } = useAccessNotice();

  return (
    <ShellChrome
      shellClassName="shell member-shell"
      identityName={user?.fullName}
      avatarUrl={user?.avatarUrl}
      token={token}
      logout={logout}
      search={(
        <input
          type="search"
          placeholder="Search training programs..."
          onKeyDown={(e) => {
            if (e.key === 'Enter' && e.target.value.trim()) {
              onSearch?.(e.target.value.trim());
            }
          }}
        />
      )}
      extraActions={(
        <>
          <NavLink to="/member/payment" className="member-wallet" aria-label="Credit wallet">
            <span><strong>{user?.creditBalance ?? '—'}</strong> Available credits</span>
            <small>On hold: {user?.heldCredits ?? '—'}</small>
          </NavLink>
          <MemberNotifications />
        </>
      )}
      identityMeta={<WorkspaceSwitcher />}
      navLabel="Member navigation"
      navHintId="member-nav-scroll-hint"
      navItems={MEMBER_NAV.map((item) => (
        <ShellNavLink
          key={item.id}
          to={item.id === 'home' ? '/' : `/member/${item.id === 'catalog' ? 'courses' : item.id === 'my-programs' ? 'programs' : item.id === 'profile' ? 'account' : item.id}`}
          className={({ isActive }) => `nav-item${isActive ? ' active' : ''}`}
          end={item.id === 'home'}
        >
          {item.label}
        </ShellNavLink>
      ))}
      title={title}
      subtitle={subtitle}
      accessNotice={accessNotice}
      onDismissAccessNotice={dismissAccessNotice}
    >
      {children}
    </ShellChrome>
  );
}
