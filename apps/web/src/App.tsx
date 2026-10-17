import { Link, NavLink, Outlet, useNavigate } from 'react-router-dom';
import { AuthProvider } from './features/auth/AuthContext';
import { useAuth } from './features/auth/useAuth';

function AppHeader() {
  const { user, isAuthenticated, logout } = useAuth();
  const navigate = useNavigate();

  function handleLogout() {
    logout();
    navigate('/login', { replace: true });
  }

  return (
    <header className="app__header">
      <Link className="app__brand" to="/tickets">
        Northwind Support
      </Link>

      {isAuthenticated && (
        <nav className="app__nav" aria-label="Main">
          <NavLink to="/tickets">Tickets</NavLink>
          <NavLink to="/customers">Customers</NavLink>
        </nav>
      )}

      <div className="app__user">
        {isAuthenticated && user ? (
          <>
            <div className="app__user-info">
              <span className="app__user-name" data-testid="user-name">
                {user.fullName}
              </span>
              <span className="app__user-role" data-testid="user-role">
                {user.role}
              </span>
            </div>
            <button
              type="button"
              className="button button--ghost"
              data-testid="logout-button"
              onClick={handleLogout}
            >
              Logout
            </button>
          </>
        ) : (
          <Link to="/login" className="button button--ghost">
            Sign In
          </Link>
        )}
      </div>
    </header>
  );
}

/** Application shell: the header, the navigation, and wherever the router puts the page. */
export function App() {
  return (
    <AuthProvider>
      <div className="app">
        <AppHeader />
        <main className="app__main">
          <Outlet />
        </main>
      </div>
    </AuthProvider>
  );
}
