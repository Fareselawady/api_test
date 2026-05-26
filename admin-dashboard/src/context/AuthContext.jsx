import { createContext, useContext, useMemo, useState } from 'react';
import { api, getErrorMessage, getRoleFromToken, routes, TOKEN_KEY } from '../api/client.js';

const AuthContext = createContext(null);

export function AuthProvider({ children }) {
  const [token, setToken] = useState(() => localStorage.getItem(TOKEN_KEY));
  const role = token ? getRoleFromToken(token) : '';
  const isAuthenticated = Boolean(token);
  const isAdmin = role === 'Admin';

  async function login(email, password) {
    const response = await api.post(routes.login, { email, password });
    const nextToken = response.data.token;
    if (!nextToken) {
      throw new Error('Login succeeded but no token was returned.');
    }

    const nextRole = getRoleFromToken(nextToken);
    if (nextRole !== 'Admin') {
      throw new Error('This dashboard requires an Admin account.');
    }

    localStorage.setItem(TOKEN_KEY, nextToken);
    setToken(nextToken);
    await api.get(routes.adminCheck);
    return response.data;
  }

  function logout() {
    localStorage.removeItem(TOKEN_KEY);
    setToken(null);
  }

  const value = useMemo(
    () => ({
      token,
      role,
      isAuthenticated,
      isAdmin,
      login,
      logout,
      getErrorMessage
    }),
    [token, role, isAuthenticated, isAdmin]
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth must be used inside AuthProvider.');
  }
  return context;
}
