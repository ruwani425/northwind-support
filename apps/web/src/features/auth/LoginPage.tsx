import { useEffect, useState, type FormEvent } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { toErrorMessage } from '../../api/client';
import { useAuth } from './useAuth';

export function LoginPage() {
  const { login, isAuthenticated } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();

  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [emailError, setEmailError] = useState<string | null>(null);
  const [passwordError, setPasswordError] = useState<string | null>(null);
  const [serverError, setServerError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  // Compute redirect destination
  const searchParams = new URLSearchParams(location.search);
  const redirectParam = searchParams.get('redirect');
  const fromState = (location.state as { from?: { pathname: string; search?: string } })?.from;
  const destination = redirectParam || (fromState ? `${fromState.pathname}${fromState.search || ''}` : '/tickets');

  // If already authenticated, redirect immediately
  useEffect(() => {
    if (isAuthenticated) {
      navigate(destination, { replace: true });
    }
  }, [isAuthenticated, destination, navigate]);

  function validate(): boolean {
    let isValid = true;

    if (!email.trim()) {
      setEmailError('Email is required.');
      isValid = false;
    } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.trim())) {
      setEmailError('Please enter a valid email address.');
      isValid = false;
    } else {
      setEmailError(null);
    }

    if (!password) {
      setPasswordError('Password is required.');
      isValid = false;
    } else {
      setPasswordError(null);
    }

    return isValid;
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setServerError(null);

    if (!validate()) {
      return;
    }

    setIsSubmitting(true);
    try {
      await login({ email: email.trim(), password });
      navigate(destination, { replace: true });
    } catch (err) {
      setServerError(toErrorMessage(err, 'Failed to sign in. Please verify your credentials.'));
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <div className="login-page" style={{ maxWidth: '420px', margin: '48px auto' }}>
      <div className="card" style={{ padding: '32px' }}>
        <h1 style={{ fontSize: '1.5rem', marginBottom: '8px' }}>Support Agent Sign In</h1>
        <p className="page-header__subtitle" style={{ marginBottom: '24px' }}>
          Sign in with your support credentials to manage tickets and customers.
        </p>

        {serverError && (
          <div
            className="error"
            role="alert"
            data-testid="login-error"
            style={{ marginBottom: '20px' }}
          >
            {serverError}
          </div>
        )}

        <form onSubmit={handleSubmit} noValidate>
          <div className="field">
            <label className="field__label" htmlFor="email">
              Email Address
            </label>
            <input
              id="email"
              name="email"
              type="email"
              autoComplete="email"
              value={email}
              aria-invalid={Boolean(emailError)}
              aria-describedby={emailError ? 'email-error' : undefined}
              onChange={(e) => {
                setEmail(e.target.value);
                if (emailError) setEmailError(null);
              }}
              required
            />
            {emailError && (
              <span id="email-error" className="field__error" role="alert">
                {emailError}
              </span>
            )}
          </div>

          <div className="field">
            <label className="field__label" htmlFor="password">
              Password
            </label>
            <input
              id="password"
              name="password"
              type="password"
              autoComplete="current-password"
              value={password}
              aria-invalid={Boolean(passwordError)}
              aria-describedby={passwordError ? 'password-error' : undefined}
              onChange={(e) => {
                setPassword(e.target.value);
                if (passwordError) setPasswordError(null);
              }}
              required
            />
            {passwordError && (
              <span id="password-error" className="field__error" role="alert">
                {passwordError}
              </span>
            )}
          </div>

          <div style={{ marginTop: '24px' }}>
            <button
              type="submit"
              className="button button--primary"
              style={{ width: '100%', justifyContent: 'center' }}
              disabled={isSubmitting}
            >
              {isSubmitting ? 'Signing in...' : 'Sign In'}
            </button>
          </div>
        </form>

        <div
          style={{
            marginTop: '24px',
            paddingTop: '16px',
            borderTop: '1px solid var(--colour-border)',
            fontSize: '0.85rem',
            color: 'var(--colour-muted)',
          }}
        >
          <p style={{ margin: '0 0 4px' }}>
            <strong>Demo Credentials:</strong>
          </p>
          <p style={{ margin: 0 }}>
            Email: <code>alex.turner@northwind-support.example</code>
            <br />
            Password: <code>Support123!</code>
          </p>
        </div>
      </div>
    </div>
  );
}
