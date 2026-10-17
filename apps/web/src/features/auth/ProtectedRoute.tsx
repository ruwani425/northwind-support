import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { Spinner } from '../../components/Spinner';
import { useAuth } from './useAuth';

export function ProtectedRoute({ children }: { children?: React.ReactNode }) {
  const { isAuthenticated, isLoading } = useAuth();
  const location = useLocation();

  if (isLoading) {
    return <Spinner />;
  }

  if (!isAuthenticated) {
    const intended = encodeURIComponent(location.pathname + location.search);
    return <Navigate to={`/login?redirect=${intended}`} state={{ from: location }} replace />;
  }

  return children ? <>{children}</> : <Outlet />;
}
