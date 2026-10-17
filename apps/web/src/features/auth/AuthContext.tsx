import {
  useCallback,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from 'react';
import { authApi } from '../../api/auth';
import {
  clearStoredAuth,
  getStoredToken,
  getStoredUser,
  setStoredToken,
  setStoredUser,
} from '../../api/client';
import type { AuthCredentials, AuthenticatedUser } from '../../types/api';
import { AuthContext, type AuthContextValue } from './authContextDef';

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<AuthenticatedUser | null>(() => getStoredUser());
  const [token, setToken] = useState<string | null>(() => getStoredToken());
  const [isLoading, setIsLoading] = useState<boolean>(true);

  // Validate or refresh session on initial mount
  useEffect(() => {
    let mounted = true;

    async function initAuth() {
      const existingToken = getStoredToken();
      if (!existingToken) {
        if (mounted) {
          setUser(null);
          setToken(null);
          setIsLoading(false);
        }
        return;
      }

      try {
        const currentUser = await authApi.getMe();
        if (mounted) {
          setUser(currentUser);
          setStoredUser(currentUser);
          setToken(existingToken);
        }
      } catch {
        // If token is invalid or expired, clear session
        if (mounted) {
          clearStoredAuth();
          setUser(null);
          setToken(null);
        }
      } finally {
        if (mounted) {
          setIsLoading(false);
        }
      }
    }

    void initAuth();

    function handleUnauthorized() {
      if (mounted) {
        setUser(null);
        setToken(null);
      }
    }

    window.addEventListener('auth:unauthorized', handleUnauthorized);
    return () => {
      mounted = false;
      window.removeEventListener('auth:unauthorized', handleUnauthorized);
    };
  }, []);

  const login = useCallback(async (credentials: AuthCredentials): Promise<AuthenticatedUser> => {
    const response = await authApi.login(credentials);
    setStoredToken(response.token);
    setStoredUser(response.agent);
    setToken(response.token);
    setUser(response.agent);
    return response.agent;
  }, []);

  const logout = useCallback(() => {
    clearStoredAuth();
    setUser(null);
    setToken(null);
  }, []);

  const value = useMemo<AuthContextValue>(
    () => ({
      user,
      token,
      isAuthenticated: Boolean(token && user),
      isLoading,
      login,
      logout,
    }),
    [user, token, isLoading, login, logout],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
