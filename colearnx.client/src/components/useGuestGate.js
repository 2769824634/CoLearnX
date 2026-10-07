import { useState } from 'react';

export function useGuestGate() {
  const [authOpen, setAuthOpen] = useState(false);
  return {
    authOpen,
    openAuth: () => setAuthOpen(true),
    closeAuth: () => setAuthOpen(false),
  };
}
