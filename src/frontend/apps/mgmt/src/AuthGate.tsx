import { useEffect, useMemo, useState, type ReactNode } from 'react';
import {
  beginAuthorization,
  completeAuthorization,
  getSubject,
  isAuthorizationCallback,
} from '@ncode/auth';
import { signedIn, useAppDispatch, useAppSelector } from '@ncode/store';
import { SignInScreen } from '@ncode/ui';
import { authConfigFromRuntime, createAuthDeps } from './auth.js';

/**
 * Stateful auth gate. When a client id is discovered, it enforces an authorization-code + PKCE login:
 * it completes the redirect callback (exchanging the code for a management-audience token), or renders
 * the sign-in screen that starts the flow. When no client id is configured (e.g. local dev before a
 * SPA client is seeded), it runs open — children render unauthenticated.
 */
export function AuthGate({ children }: { children: ReactNode }) {
  const dispatch = useAppDispatch();
  const runtime = useAppSelector((state) => state.config.runtime);
  const accessToken = useAppSelector((state) => state.auth.accessToken);
  const deps = useMemo(createAuthDeps, []);
  const authConfig = useMemo(() => (runtime ? authConfigFromRuntime(runtime) : null), [runtime]);
  const productName = runtime?.branding?.displayName;

  const [error, setError] = useState<string | null>(null);
  const [isBusy, setBusy] = useState(() =>
    typeof window !== 'undefined' ? isAuthorizationCallback(window.location.search) : false,
  );

  useEffect(() => {
    if (!authConfig || accessToken || !isAuthorizationCallback(window.location.search)) {
      return;
    }
    let cancelled = false;
    setBusy(true);
    completeAuthorization(authConfig, window.location.search, deps)
      .then((tokens) => {
        if (cancelled) {
          return;
        }
        const subject =
          (tokens.idToken && getSubject(tokens.idToken)) ||
          getSubject(tokens.accessToken) ||
          'unknown';
        dispatch(
          signedIn({
            accessToken: tokens.accessToken,
            subject,
            ...(tokens.expiresInSeconds
              ? { expiresAt: Date.now() + tokens.expiresInSeconds * 1000 }
              : {}),
            ...(tokens.refreshToken ? { refreshToken: tokens.refreshToken } : {}),
          }),
        );
        window.history.replaceState({}, document.title, `${window.location.origin}/`);
      })
      .catch((cause: unknown) => {
        if (!cancelled) {
          setError(cause instanceof Error ? cause.message : 'Sign-in failed.');
        }
      })
      .finally(() => {
        if (!cancelled) {
          setBusy(false);
        }
      });
    return () => {
      cancelled = true;
    };
  }, [authConfig, accessToken, deps, dispatch]);

  if (accessToken || !authConfig) {
    return <>{children}</>;
  }

  const handleSignIn = () => {
    setError(null);
    setBusy(true);
    beginAuthorization(authConfig, deps)
      .then((url) => window.location.assign(url))
      .catch((cause: unknown) => {
        setError(cause instanceof Error ? cause.message : 'Unable to start sign-in.');
        setBusy(false);
      });
  };

  return (
    <SignInScreen
      onSignIn={handleSignIn}
      isBusy={isBusy}
      {...(productName ? { productName } : {})}
      {...(error ? { error } : {})}
    />
  );
}
