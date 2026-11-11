import { useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';

export default function useAccountShortcuts() {
  const location = useLocation();
  const navigate = useNavigate();
  const [manualEditOpen, setEditOpen] = useState(false);
  const editOpen = manualEditOpen || location.hash === '#edit-profile';

  function closeEdit() {
    setEditOpen(false);
    if (location.hash === '#edit-profile') navigate(`${location.pathname}${location.search}`, { replace: true });
  }

  return { editOpen, setEditOpen, closeEdit };
}
