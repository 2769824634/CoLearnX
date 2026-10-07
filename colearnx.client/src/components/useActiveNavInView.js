import { useLayoutEffect, useRef } from 'react';
import { useLocation } from 'react-router-dom';

// Narrow screens turn the sidebar into a horizontal strip; keep the current page's item visible.
export default function useActiveNavInView() {
  const navRef = useRef(null);
  const { pathname } = useLocation();

  useLayoutEffect(() => {
    const nav = navRef.current;
    const active = nav?.querySelector('.nav-item.active');
    if (!active || nav.scrollWidth <= nav.clientWidth) return;
    nav.scrollTo({ left: active.offsetLeft - (nav.clientWidth - active.offsetWidth) / 2 });
  }, [pathname]);

  return navRef;
}
