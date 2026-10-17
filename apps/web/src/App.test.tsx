import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { beforeEach, describe, expect, it } from 'vitest';
import { App } from './App';
import { clearStoredAuth, getStoredToken, setStoredToken, setStoredUser } from './api/client';

describe('App Header Authentication', () => {
  beforeEach(() => {
    localStorage.clear();
    clearStoredAuth();
  });

  it('renders brand and Sign In link when unauthenticated', () => {
    render(
      <MemoryRouter>
        <App />
      </MemoryRouter>,
    );

    expect(screen.getByText('Northwind Support')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /sign in/i })).toBeInTheDocument();
  });

  it('displays user name, role badge, and handles logout when authenticated', async () => {
    const user = userEvent.setup();
    setStoredToken('active-jwt-token');
    setStoredUser({
      id: 1,
      fullName: 'Alex Turner',
      email: 'alex.turner@northwind-support.example',
      role: 'Support Lead',
    });

    render(
      <MemoryRouter>
        <App />
      </MemoryRouter>,
    );

    expect(screen.getByTestId('user-name')).toHaveTextContent('Alex Turner');
    expect(screen.getByTestId('user-role')).toHaveTextContent('Support Lead');
    const logoutBtn = screen.getByTestId('logout-button');
    expect(logoutBtn).toBeInTheDocument();

    await user.click(logoutBtn);

    await waitFor(() => {
      expect(getStoredToken()).toBeNull();
      expect(screen.queryByTestId('user-name')).not.toBeInTheDocument();
      expect(screen.getByRole('link', { name: /sign in/i })).toBeInTheDocument();
    });
  });
});
