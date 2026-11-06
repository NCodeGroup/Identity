import { useCallback, useEffect, useRef, useState, type ReactNode } from 'react';
import type { BffRuntimeConfig } from '@ncode/config';
import {
  fetchBffUser,
  sessionEnded,
  sessionStarted,
  useAppDispatch,
  useAppSelector,
} from '@ncode/store';
import { BootstrapScreen, SignInScreen } from '@ncode/ui';
import {
  SESSION_ENDED_NOTICE,
  SESSION_POLL_INTERVAL_MS,
  announceSessionEnded,
  loginUrl,
  nextProbeDelayMs,
  openSessionChannel,
  readSignInError,
  signInErrorMessage,
} from './session.js';

const PROBE_FAILED = 'Unable to check your session.';

/**
 * Stateful session gate. The browser holds no token: the console's session is the BFF's cookie, which the gate probes
 * through the BFF's user endpoint. It renders the children while a session exists and the sign-in screen otherwise,
 * whose button navigates to the BFF's login endpoint.
 *
 * While signed in, the gate probes the session on an interval, shortly after the session's expiry and when a hidden
 * tab becomes visible, and it shares an end of the session with the console's other tabs (ADR-1434, ADR-1438).
 */
export function SessionGate({ children }: { children: ReactNode }) {
  const dispatch = useAppDispatch();
  const runtime = useAppSelector((state) => state.config.runtime);
  const status = useAppSelector((state) => state.session.status);
  const user = useAppSelector((state) => state.session.user);
  const bff = runtime?.kind === 'bff' ? runtime : null;

  const [signInError] = useState(() => readSignInError(window.location.search));
  const [probeError, setProbeError] = useState<string | null>(null);
  const [isBusy, setIsBusy] = useState(false);
  const [hasEnded, setHasEnded] = useState(false);

  const mountedRef = useRef(false);
  const endedByBroadcastRef = useRef(false);
  const previousStatusRef = useRef(status);

  useEffect(() => {
    mountedRef.current = true;
    return () => {
      mountedRef.current = false;
    };
  }, []);

  // A probe that answers after the gate unmounted must not touch the store or the gate's state.
  const probe = useCallback(
    async (target: BffRuntimeConfig) => {
      try {
        const probed = await fetchBffUser(target);
        if (!mountedRef.current) {
          return;
        }
        dispatch(probed ? sessionStarted(probed) : sessionEnded());
      } catch {
        if (mountedRef.current) {
          setProbeError(PROBE_FAILED);
        }
      }
    },
    [dispatch],
  );

  useEffect(() => {
    if (bff) {
      void probe(bff);
    }
  }, [bff, probe]);

  // The BFF's error code is shown once on the sign-in screen and removed from the address bar.
  useEffect(() => {
    if (signInError) {
      window.history.replaceState(null, '', window.location.pathname);
    }
  }, [signInError]);

  useEffect(() => {
    if (!bff || status !== 'signed-in' || !user) {
      return;
    }

    const intervalId = window.setInterval(() => void probe(bff), SESSION_POLL_INTERVAL_MS);
    const timeoutId = window.setTimeout(() => void probe(bff), nextProbeDelayMs(user));
    const handleVisibility = () => {
      if (document.visibilityState === 'visible') {
        void probe(bff);
      }
    };
    document.addEventListener('visibilitychange', handleVisibility);
    const channel = openSessionChannel(() => {
      endedByBroadcastRef.current = true;
      dispatch(sessionEnded());
    });

    return () => {
      window.clearInterval(intervalId);
      window.clearTimeout(timeoutId);
      document.removeEventListener('visibilitychange', handleVisibility);
      channel.close();
    };
  }, [bff, status, user, probe, dispatch]);

  // A session that ended in this tab is announced to the other tabs; one that ended because another tab announced it
  // is not announced again.
  useEffect(() => {
    const previousStatus = previousStatusRef.current;
    previousStatusRef.current = status;
    if (previousStatus === 'signed-in' && status === 'signed-out') {
      if (!endedByBroadcastRef.current) {
        announceSessionEnded();
      }
      endedByBroadcastRef.current = false;
      setHasEnded(true);
    }
  }, [status]);

  if (!bff) {
    return <BootstrapScreen error="The management console must be served by its BFF host." />;
  }

  if (status === 'signed-in') {
    return <>{children}</>;
  }

  const handleSignIn = () => {
    setIsBusy(true);
    window.location.assign(loginUrl(bff, window.location.pathname));
  };

  const productName = bff.branding?.displayName;
  const error = signInError
    ? signInErrorMessage(signInError)
    : hasEnded
      ? SESSION_ENDED_NOTICE
      : probeError;

  return (
    <SignInScreen
      onSignIn={handleSignIn}
      isBusy={isBusy || (status === 'unknown' && !probeError)}
      {...(productName ? { productName } : {})}
      {...(error ? { error } : {})}
    />
  );
}
