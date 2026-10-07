import { useState } from 'react';
import { Link, NavLink, useNavigate } from 'react-router-dom';
import Logo from './Logo';
import AuthRequiredModal from './AuthRequiredModal';
import { MEMBER_NAV } from '../data/memberMock';
import { SITE_TITLE } from '../siteTitle';

export default function PublicSiteShell({ title, children }) {
  const navigate = useNavigate();
  const [authOpen, setAuthOpen] = useState(false);

  return (
    <div className="shell member-shell public-site-shell">
      <div className="topbar">
        <div className="topbar-brand">
          <Link to="/" aria-label={SITE_TITLE}>
            <Logo />
          </Link>
        </div>
        <div className="topbar-search search">
          <input
            type="search"
            placeholder="Search training programs..."
            onKeyDown={(event) => {
              if (event.key === 'Enter' && event.target.value.trim()) {
                const query = encodeURIComponent(event.target.value.trim());
                navigate(`/courses?q=${query}`);
              }
            }}
          />
        </div>
        <div className="topbar-actions">
          <Link className="btn btn-ghost" to="/login">Log in</Link>
          <Link className="btn btn-primary" to="/register">Sign up</Link>
        </div>
      </div>
      <div className="shell-body">
        <nav className="sidebar" aria-label="Site navigation">
          {MEMBER_NAV.map((item) => {
            const to = item.id === 'home' ? '/' : item.id === 'catalog' ? '/courses' : null;
            if (!to) {
              return (
                <button key={item.id} type="button" className="nav-item" onClick={() => setAuthOpen(true)}>
                  {item.label}
                </button>
              );
            }
            return (
              <NavLink
                key={item.id}
                to={to}
                end={item.id === 'home'}
                className={({ isActive }) => `nav-item${isActive ? ' active' : ''}`}
              >
                {item.label}
              </NavLink>
            );
          })}
        </nav>
        <main className="content">
          {title ? <h1 className="page-title">{title}</h1> : null}
          {children}
        </main>
      </div>
      <AuthRequiredModal open={authOpen} onClose={() => setAuthOpen(false)} />
    </div>
  );
}

export function useGuestGate() {
  const [authOpen, setAuthOpen] = useState(false);
  return {
    authOpen,
    openAuth: () => setAuthOpen(true),
    closeAuth: () => setAuthOpen(false),
  };
}
