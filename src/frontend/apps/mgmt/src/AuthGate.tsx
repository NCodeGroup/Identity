import { useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import {
  beginAuthorization,
  completeAuthorization,
  createTokenRenewer,
  getSubject,
  isAuthorizationCallback,
} from '@ncode/auth';
import { signedIn, signedOut, useAppDispatch, useAppSelector, useAppStore } from '@ncode/store';
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
  const store = useAppStore();
  const runtime = useAppSelector((state) => state.config.runtime);
  const accessToken = useAppSelector((state) => state.auth.accessToken);
  const deps = useMemo(createAuthDeps, []);
  const authConfig = useMemo(() => (runtime ? authConfigFromRuntime(runtime) : null), [runtime]);
  const productName = runtime?.branding?.displayName;

  const [error, setError] = useState<string | null>(null);
  const [isBusy, setBusy] = useState(() =>
    typeof window !== 'undefined' ? isAuthorizationCallback(window.location.search) : false,
  );

  // Completing the callback consumes a single-use authorization code and the one-shot PKCE
  // transaction, so it must run EXACTLY once. A run-once ref (not an effect-cancellation flag) guards
  // it: React StrictMode intentionally double-invokes effects in development, and a cancellation flag
  // lets the second invocation re-enter completeAuthorization after the first already consumed the
  // transaction — which then dead-ends on "No in-flight authorization transaction was found." The ref
  // is set only when completion actually starts, so a pre-discovery render (authConfig still null)
  // does not consume the single attempt.
  const completionStarted = useRef(false);

  useEffect(() => {
    if (
      completionStarted.current ||
      !authConfig ||
      accessToken ||
      !isAuthorizationCallback(window.location.search)
    ) {
      return;
    }
    completionStarted.current = true;
    setBusy(true);
    completeAuthorization(authConfig, window.location.search, deps)
      .then((tokens) => {
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
        setError(cause instanceof Error ? cause.message : 'Sign-in failed.');
      })
      .finally(() => {
        setBusy(false);
      });
  }, [authConfig, accessToken, deps, dispatch]);

  // Silent renewal: once signed in, keep the access token fresh by exchanging the refresh token
  // ahead of expiry, falling back to interactive sign-in when renewal is no longer possible.
  useEffect(() => {
    if (!authConfig || !accessToken) {
      return;
    }
    const renewer = createTokenRenewer({
      tokenEndpoint: authConfig.tokenEndpoint,
      clientId: authConfig.clientId,
      fetchFn: deps.fetchFn,
      getRefreshToken: () => store.getState().auth.refreshToken,
      onRenewed: (tokens) => {
        const subject =
          (tokens.idToken && getSubject(tokens.idToken)) ||
          getSubject(tokens.accessToken) ||
          store.getState().auth.subject ||
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
      },
      onError: () => dispatch(signedOut()),
    });
    renewer.schedule(store.getState().auth.expiresAt);
    return () => renewer.stop();
  }, [authConfig, accessToken, deps, dispatch, store]);

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
