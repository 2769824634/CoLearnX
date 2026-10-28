import { useEffect, useState } from 'react';
import { staffAuthApi } from '../api';
import { getStoredStaffToken, setStoredStaffToken, SESSION_REPLACED_EVENT } from '../api/client';
import StaffAuthContext from './StaffAuthContext';

export default function StaffAuthProvider({ children }) {
  const [token, setToken] = useState(() => getStoredStaffToken());
  const [staff, setStaff] = useState(null);
  const [booting, setBooting] = useState(Boolean(token));

  useEffect(() => {
    const onReplaced = (event) => {
      if (!event.detail?.staff) return;
      setToken(null);
      setStaff(null);
      setBooting(false);
    };
    window.addEventListener(SESSION_REPLACED_EVENT, onReplaced);
    return () => window.removeEventListener(SESSION_REPLACED_EVENT, onReplaced);
  }, []);

  useEffect(() => {
    if (!token) return undefined;
    let cancelled = false;
    staffAuthApi.me(token).then((account) => { if (!cancelled) setStaff(account); })
      .catch(() => {
        if (!cancelled) {
          setStoredStaffToken(null);
          setToken(null);
          setStaff(null);
        }
      }).finally(() => { if (!cancelled) setBooting(false); });
    return () => { cancelled = true; };
  }, [token]);

  async function login(email, password) {
    const response = await staffAuthApi.login(email, password);
    setStoredStaffToken(response.accessToken);
    setToken(response.accessToken);
    setStaff(response.staff);
    setBooting(false);
    return response.staff;
  }

  function logout() {
    setStoredStaffToken(null);
    setToken(null);
    setStaff(null);
    setBooting(false);
  }

  return <StaffAuthContext.Provider value={{
    token, staff, booting, isAuthenticated: Boolean(token && staff), login, logout,
  }}>{children}</StaffAuthContext.Provider>;
}
