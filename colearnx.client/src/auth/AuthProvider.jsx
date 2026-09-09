import { useEffect, useState } from 'react';
import { authApi } from '../api';
import { ApiError, getStoredToken, setStoredToken } from '../api/client';
import { AuthContext } from './AuthContext';

// Session + login / switchRole. Shared: AuthProvider
export function AuthProvider({ children }) {
  const [token, setToken] = useState(() => getStoredToken());
  const [user, setUser] = useState(null);
  const [booting, setBooting] = useState(true);
  const [error, setError] = useState(null);

  useEffect(() => {
    let cancelled = false;
    async function boot() {
      if (!token) {
        setBooting(false);
        return;
      }
      try {
        const me = await authApi.me();
        if (!cancelled) setUser(me);
      } catch {
        if (!cancelled) {
          setStoredToken(null);
          setToken(null);
          setUser(null);
        }
      } finally {
        if (!cancelled) setBooting(false);
      }
    }
    boot();
    return () => {
      cancelled = true;
    };
  }, [token]);

  async function login(email, password, activeRole) {
    setError(null);
    try {
      const res = await authApi.login(email, password, activeRole);
      setStoredToken(res.accessToken);
      setToken(res.accessToken);
      setUser(res.user);
      return res.user;
    } catch (e) {
      const msg = e instanceof ApiError ? e.message : 'Login failed';
      setError(msg);
      throw e;
    }
  }

  async function switchRole(activeRole) {
    const res = await authApi.switchRole(activeRole);
    setStoredToken(res.accessToken);
    setToken(res.accessToken);
    setUser(res.user);
    return res.user;
  }

  function logout() {
    setStoredToken(null);
    setToken(null);
    setUser(null);
  }

  function refreshUser(next) {
    setUser(next);
  }

  const value = {
    token,
    user,
    booting,
    error,
    isAuthenticated: Boolean(token && user),
    activeRole: user?.activeRole?.toLowerCase() ?? null,
    roles: user?.roles ?? [],
    login,
    switchRole,
    logout,
    refreshUser,
  };

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
