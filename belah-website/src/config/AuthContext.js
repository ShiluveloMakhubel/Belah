import React, { createContext, useContext, useState, useEffect, useCallback } from 'react';

const AuthContext = createContext(null);

export const AuthProvider = ({ children }) => {
  const [user, setUser] = useState(null);
  const [token, setToken] = useState(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    try {
      const savedToken = localStorage.getItem('belah_token');
      const savedUser = localStorage.getItem('belah_user');
      let savedExpiry = Number(localStorage.getItem('belah_token_expires_at'));
      if (!savedExpiry && savedToken) {
        try {
          const payload = JSON.parse(atob(savedToken.split('.')[1].replace(/-/g, '+').replace(/_/g, '/')));
          savedExpiry = Number(payload.exp) * 1000;
          if (savedExpiry) localStorage.setItem('belah_token_expires_at', String(savedExpiry));
        } catch { savedExpiry = 0; }
      }
      if (savedToken && savedUser && (!savedExpiry || savedExpiry > Date.now())) {
        setToken(savedToken);
        setUser(JSON.parse(savedUser));
      } else if (savedExpiry && savedExpiry <= Date.now()) {
        localStorage.removeItem('belah_token');
        localStorage.removeItem('belah_user');
        localStorage.removeItem('belah_token_expires_at');
      }
    } catch (e) {
      console.error('Failed to restore auth session:', e);
    } finally {
      setLoading(false);
    }
  }, []);

  const login = (authToken, userData, expiresAt) => {
    setToken(authToken);
    setUser(userData);
    try {
      localStorage.setItem('belah_token', authToken);
      localStorage.setItem('belah_user', JSON.stringify(userData));
      if (expiresAt) localStorage.setItem('belah_token_expires_at', String(new Date(expiresAt).getTime()));
    } catch (e) {
      console.error('Failed to save auth to localStorage:', e);
    }
  };

  const logout = useCallback(() => {
    setToken(null);
    setUser(null);
    try {
      localStorage.removeItem('belah_token');
      localStorage.removeItem('belah_user');
      localStorage.removeItem('belah_token_expires_at');
    } catch (e) {
      console.error('Failed to remove auth from localStorage:', e);
    }
  }, []);

  useEffect(() => {
    if (!token) return undefined;
    const expiry = Number(localStorage.getItem('belah_token_expires_at')) || Date.now() + 60 * 60 * 1000;
    const timeout = window.setTimeout(logout, Math.max(0, expiry - Date.now()));
    return () => window.clearTimeout(timeout);
  }, [token, logout]);

  const isAdmin = user?.role === 'Admin';

  return (
    <AuthContext.Provider value={{ user, token, isAdmin, loading, login, logout }}>
      {children}
    </AuthContext.Provider>
  );
};

export const useAuth = () => useContext(AuthContext);

export default AuthContext;
