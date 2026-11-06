import type { BffRuntimeConfig } from '@ncode/config';
import type { BffUser } from '@ncode/store';

/**
 * The console's BFF session helpers (ADR-1434, ADR-1438): the sign-in and sign-out navigations, the sign-in error the
 * BFF reports, the schedule of the next session probe and the cross-tab signal that a session ended. The browser holds
 * no token; the session is the BFF's cookie, and these helpers only navigate to the BFF's endpoints.
 *
 * This module is UI-free so the session rules are unit-tested.
 */

/** The interval, in milliseconds, between two probes of the BFF session. */
export const SESSION_POLL_INTERVAL_MS = 60_000;

/** The name of the `BroadcastChannel` the console's tabs use to share the end of a session. */
export const SESSION_CHANNEL_NAME = 'ncode.mgmt.session';

/** The message a tab posts on the session channel when its session ended. */
export const SESSION_ENDED_MESSAGE = 'ended';

/** The notice the sign-in screen shows after a session ended. */
export const SESSION_ENDED_NOTICE = 'Your session ended. Sign in to continue.';

/** The BFF's login URL, which returns to `returnPath` once the sign-in round trip completes. */
export function loginUrl(runtime: BffRuntimeConfig, returnPath: string): string {
  return `${runtime.bff.basePath}/login?returnUrl=${encodeURIComponent(returnPath)}`;
}

/**
 * Signs the user out by posting the BFF's logout form. A form post is a top-level navigation, so the browser follows
 * the BFF's redirect to the OpenID Provider's end-session endpoint and back to the console's root.
 */
export function submitLogout(user: BffUser, doc: Document = document): void {
  const form = doc.createElement('form');
  form.method = 'post';
  form.action = user.logoutUrl;
  form.hidden = true;

  const fields: Readonly<Record<string, string>> = { sid: user.logoutSid, returnUrl: '/' };
  for (const [name, value] of Object.entries(fields)) {
    const input = doc.createElement('input');
    input.type = 'hidden';
    input.name = name;
    input.value = value;
    form.appendChild(input);
  }

  doc.body.appendChild(form);
  form.submit();
}

/** The `error` code the BFF put on the page's query string after a failed sign-in, if any. */
export function readSignInError(search: string): string | undefined {
  const code = new URLSearchParams(search).get('error');
  return code === null || code === '' ? undefined : code;
}

/** The sign-in screen's message for a BFF error code. */
export function signInErrorMessage(code: string): string {
  return `Sign-in failed (${code}).`;
}

/**
 * The delay, in milliseconds, until the session is probed again: one second after the earlier of the session's idle
 * and absolute expiry, or one second from `now` once that moment has passed.
 */
export function nextProbeDelayMs(user: BffUser, now: number = Date.now()): number {
  const expiryMs = Math.min(user.session.idleExpiresAt, user.session.expiresAt) * 1000;
  return Math.max(0, expiryMs - now) + 1000;
}

/**
 * Listens on the console's session channel and calls `onEnded` when another tab announces that its session ended.
 * Without `BroadcastChannel` there is nothing to listen to and the returned handle does nothing.
 */
export function openSessionChannel(onEnded: () => void): { close(): void } {
  if (typeof globalThis.BroadcastChannel === 'undefined') {
    return { close: () => {} };
  }
  const channel = new BroadcastChannel(SESSION_CHANNEL_NAME);
  channel.onmessage = (event: MessageEvent<unknown>) => {
    if (event.data === SESSION_ENDED_MESSAGE) {
      onEnded();
    }
  };
  return { close: () => channel.close() };
}

/** Tells the console's other tabs that the session ended. Does nothing without `BroadcastChannel`. */
export function announceSessionEnded(): void {
  if (typeof globalThis.BroadcastChannel === 'undefined') {
    return;
  }
  const channel = new BroadcastChannel(SESSION_CHANNEL_NAME);
  channel.postMessage(SESSION_ENDED_MESSAGE);
  channel.close();
}
