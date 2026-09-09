import { useEffect, useState } from 'react';
import { adminAuthApi } from '../api';
import { ApiError, getStoredAdminToken, setStoredAdminToken } from '../api/client';
import AdminAuthContext from './AdminAuthContext';

export default function AdminAuthProvider({ children }) {
  const [token, setToken] = useState(() => getStoredAdminToken());
  const [admin, setAdmin] = useState(null);
  const [booting, setBooting] = useState(Boolean(token));

  useEffect(() => {
    if (!token) return undefined;

    let cancelled = false;
    adminAuthApi.me(token)
      .then((account) => {
        if (!cancelled) setAdmin(account);
      })
      .catch(() => {
        if (!cancelled) {
          setStoredAdminToken(null);
          setToken(null);
          setAdmin(null);
        }
      })
      .finally(() => {
        if (!cancelled) setBooting(false);
      });

    return () => {
      cancelled = true;
    };
  }, [token]);

  async function login(email, password) {
    try {
      const response = await adminAuthApi.login(email, password);
      setStoredAdminToken(response.accessToken);
      setToken(response.accessToken);
      setAdmin(response.admin);
      setBooting(false);
      return response.admin;
    } catch (error) {
      if (error instanceof ApiError) throw error;
      throw new Error('Administrator sign-in failed.', { cause: error });
    }
  }

  function logout() {
    setStoredAdminToken(null);
    setToken(null);
    setAdmin(null);
    setBooting(false);
  }

  const value = {
    token,
    admin,
    booting,
    isAuthenticated: Boolean(token && admin),
    login,
    logout,
  };

  return <AdminAuthContext.Provider value={value}>{children}</AdminAuthContext.Provider>;
}
