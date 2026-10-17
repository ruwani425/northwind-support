import { render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { authApi } from '../../api/auth';
import { clearStoredAuth, setStoredToken, setStoredUser } from '../../api/client';
import { AuthProvider } from './AuthContext';
import { ProtectedRoute } from './ProtectedRoute';

vi.mock('../../api/auth', () => ({
  authApi: {
    getMe: vi.fn(),
    login: vi.fn(),
  },
}));

describe('ProtectedRoute', () => {
  beforeEach(() => {
    localStorage.clear();
    clearStoredAuth();
    vi.clearAllMocks();
  });

  it('redirects unauthenticated users to /login preserving intended destination', async () => {
    render(
      <MemoryRouter initialEntries={['/tickets/42']}>
        <AuthProvider>
          <Routes>
            <Route path="/login" element={<div>Login Page Target</div>} />
            <Route element={<ProtectedRoute />}>
              <Route path="/tickets/:id" element={<div>Protected Ticket Content</div>} />
            </Route>
          </Routes>
        </AuthProvider>
      </MemoryRouter>,
    );

    await waitFor(() => {
      expect(screen.getByText('Login Page Target')).toBeInTheDocument();
    });
    expect(screen.queryByText('Protected Ticket Content')).not.toBeInTheDocument();
  });

  it('allows access to protected routes when authenticated', async () => {
    setStoredToken('valid-token');
    setStoredUser({
      id: 1,
      fullName: 'Alex Turner',
      email: 'alex.turner@northwind-support.example',
      role: 'Support Lead',
    });
    vi.mocked(authApi.getMe).mockResolvedValue({
      id: 1,
      fullName: 'Alex Turner',
      email: 'alex.turner@northwind-support.example',
      role: 'Support Lead',
    });

    render(
      <MemoryRouter initialEntries={['/tickets/42']}>
        <AuthProvider>
          <Routes>
            <Route path="/login" element={<div>Login Page Target</div>} />
            <Route element={<ProtectedRoute />}>
              <Route path="/tickets/:id" element={<div>Protected Ticket Content</div>} />
            </Route>
          </Routes>
        </AuthProvider>
      </MemoryRouter>,
    );

    await waitFor(() => {
      expect(screen.getByText('Protected Ticket Content')).toBeInTheDocument();
    });
    expect(screen.queryByText('Login Page Target')).not.toBeInTheDocument();
  });
});
