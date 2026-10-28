import { useEffect, useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';

export default function useAccountShortcuts() {
  const location = useLocation();
  const navigate = useNavigate();
  const [manualEditOpen, setEditOpen] = useState(false);
  const editOpen = manualEditOpen || location.hash === '#edit-profile';

  useEffect(() => {
    if (location.hash === '#applications') {
      document.getElementById('account-applications')?.scrollIntoView?.({ block: 'start' });
    }
  }, [location.hash, location.key]);

  function closeEdit() {
    setEditOpen(false);
    if (location.hash === '#edit-profile') navigate(`${location.pathname}${location.search}`, { replace: true });
  }

  return { editOpen, setEditOpen, closeEdit };
}
