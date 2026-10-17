import type { AuthCredentials, AuthenticatedUser, AuthResponse } from '../types/api';
import { request } from './client';

export const authApi = {
  login: (credentials: AuthCredentials) =>
    request<AuthResponse>('/auth/login', {
      method: 'POST',
      body: JSON.stringify(credentials),
    }),

  getMe: () => request<AuthenticatedUser>('/auth/me'),
};
