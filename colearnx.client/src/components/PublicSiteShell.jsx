import { useEffect, useState } from 'react';
import { Link, NavLink, useLocation, useNavigate } from 'react-router-dom';
import '../styles/guest.css';
import Logo from './Logo';
import { SITE_TITLE } from '../siteTitle';

const GUEST_NAV = [
  { to: '/', label: 'Home', end: true },
  { to: '/courses', label: 'Courses' },
];

export default function PublicSiteShell({ title, children }) {
  const navigate = useNavigate();
  const location = useLocation();
  const [scrolled, setScrolled] = useState(false);
  const [menuOpen, setMenuOpen] = useState(false);
  const [menuPath, setMenuPath] = useState(location.pathname);

  if (menuPath !== location.pathname) {
    setMenuPath(location.pathname);
    setMenuOpen(false);
  }

  useEffect(() => {
    const onScroll = () => setScrolled(window.scrollY > 12);
    onScroll();
    window.addEventListener('scroll', onScroll, { passive: true });
    return () => window.removeEventListener('scroll', onScroll);
  }, []);

  useEffect(() => {
    if (!menuOpen) return undefined;
    const onKey = (event) => { if (event.key === 'Escape') setMenuOpen(false); };
    document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
  }, [menuOpen]);

  function search(event) {
    event.preventDefault();
    const query = new FormData(event.currentTarget).get('q')?.toString().trim();
    navigate(query ? `/courses?q=${encodeURIComponent(query)}` : '/courses');
  }

  return (
    <div className="guest">
      <a className="skip-link" href="#guest-main">Skip to content</a>
      <header className={`g-header${scrolled ? ' is-scrolled' : ''}${menuOpen ? ' is-open' : ''}`}>
        <Link to="/" className="g-brand" aria-label={`${SITE_TITLE} home`}>
          <Logo />
        </Link>
        <button
          type="button"
          className="g-menu-toggle"
          aria-expanded={menuOpen}
          aria-controls="guest-panel"
          onClick={() => setMenuOpen((open) => !open)}
        >
          <span className="g-menu-lines" aria-hidden="true"><i /><i /></span>
          <span className="g-menu-label">{menuOpen ? 'Close' : 'Menu'}</span>
        </button>
        <div className="g-panel" id="guest-panel">
          <nav className="g-nav" aria-label="Site navigation">
            {GUEST_NAV.map((item) => (
              <NavLink key={item.to} to={item.to} end={item.end} className={({ isActive }) => `g-nav-link${isActive ? ' is-active' : ''}`}>
                {item.label}
              </NavLink>
            ))}
          </nav>
          {location.pathname === '/courses' ? null : (
            <form className="g-search" role="search" onSubmit={search}>
              <svg viewBox="0 0 20 20" aria-hidden="true" focusable="false"><circle cx="9" cy="9" r="5.5" fill="none" stroke="currentColor" strokeWidth="1.7" /><path d="m13.2 13.2 3.8 3.8" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" /></svg>
              <input type="search" name="q" placeholder="Search training programs..." aria-label="Search training programs" />
            </form>
          )}
          <div className="g-auth">
            <Link className="g-btn g-btn-quiet" to="/login">Log in</Link>
            <Link className="g-btn g-btn-primary" to="/register">Sign up</Link>
          </div>
        </div>
      </header>
      {menuOpen ? <div className="g-scrim" aria-hidden="true" onClick={() => setMenuOpen(false)} /> : null}

      <main id="guest-main" className="g-main" tabIndex={-1}>
        {title ? <h1 className="g-page-title">{title}</h1> : null}
        {children}
      </main>

      <section className="g-cta-band" aria-labelledby="g-cta-title">
        <div>
          <h2 id="g-cta-title">Ready for what&rsquo;s next?</h2>
          <p>Create a free account to reserve places, save programs and follow your progress.</p>
        </div>
        <Link to="/register" className="g-btn g-btn-white g-btn-lg">Get started <span aria-hidden="true">→</span></Link>
      </section>

      <footer className="g-footer">
        <div className="g-footer-grid">
          <div className="g-footer-brand">
            <Logo />
            <p>Scheduled, small-group programs led by working trainers.</p>
          </div>
          <div>
            <h2>Learn</h2>
            <Link to="/courses">Browse all programs</Link>
            <Link to="/#how">How it works</Link>
          </div>
          <div>
            <h2>Teach</h2>
            <Link to="/register">Become a Trainer</Link>
            <Link to="/register">Publish as a Creator</Link>
          </div>
          <div>
            <h2>Account</h2>
            <Link to="/login">Sign in to your account</Link>
            <Link to="/register">Create a free account</Link>
          </div>
        </div>
        <div className="g-footer-base">
          <span>&copy; {new Date().getFullYear()} {SITE_TITLE}</span>
          <span>Your path to what&rsquo;s neXt.</span>
        </div>
      </footer>
    </div>
  );
}
