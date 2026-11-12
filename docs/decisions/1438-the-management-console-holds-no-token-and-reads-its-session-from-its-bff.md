# 1438. The management console holds no token and reads its session from its BFF

- **Status:** Accepted
- **Date:** 2026-11-05
- **Deciders:** NCode Group

## Context

The management console is served by a BFF host of its own
([ADR-1437](1437-the-management-console-is-a-confidential-client-whose-own-bff-host-serves-only-the-console.md)), so
its bundle holds no token and calls the management API with the session cookie.

A bundle that a BFF serves needs three things at boot: the base of the API it calls, the prefix of its BFF's endpoints
and the anti-forgery header its BFF requires. It needs no issuer, no client id and no scopes, because the browser takes
no part in the OpenID flow; the BFF does.

Several tabs of one browser share one BFF session
([ADR-1434](1434-open-tabs-learn-that-a-bff-session-ended-from-its-next-answer-not-a-pushed-event.md)). A frontend
that learns that the session ended could navigate to `/bff/login`. When every tab does so, a logout in one tab races a
login in another, and a failing login loops: the BFF redirects back with an error, the page learns that there is no
session and navigates to `/bff/login` again.

## Decision

**The console's bundle holds no token and reads its session from `GET /bff/user`. Any end of the session returns the
console to its sign-in screen, and the user's click starts the login.**

1. **The bootstrap document.** The console host publishes `/config/bootstrap.json` as
   `{ "apiBaseUrl": "/management", "bff": { "basePath": "/bff", "antiforgeryHeader": { "name": "X-CSRF", "value": "1" } },
   "features": {…}, "branding": {…} }`.
2. **The runtime configuration.** `@ncode/config` models `RuntimeConfig` as the union of `OpenIdRuntimeConfig`
   (`kind: 'openid'`) and `BffRuntimeConfig` (`kind: 'bff'`). `loadBffRuntimeConfig` fetches the bootstrap document and
   fetches no OpenID discovery document.
3. **The session slice.** `@ncode/store` holds no token slice. Its `session` slice is `unknown`, `signed-in` or
   `signed-out`, and a signed-in session carries the `BffUser`. `fetchBffUser` reads `GET /bff/user` and feeds the slice.
4. **The management calls.** The management base query sends the anti-forgery header with same-origin credentials and
   never an `Authorization` header. A `401` dispatches `sessionEnded()`.
5. **`@ncode/auth`.** The package holds the Playground's public-client protocol helpers: PKCE, the authorization and
   end-session URLs, the authorization-code and refresh-token requests, callback parsing and JWT claim decoding. It
   holds no custody of a token for the console.
6. **`SessionGate`.** The gate probes `GET /bff/user`, polls it every 60 seconds, probes one second after
   `min(idleExpiresAt, expiresAt)`, probes again when a hidden tab becomes visible, and shares an end through the
   `BroadcastChannel` named `ncode.mgmt.session`.
7. **The sign-in screen is the end state.** Any end of the session, whether it is a logout, a `401`, an expiry or a
   failed refresh, returns the console to its sign-in screen: "Your session ended. Sign in to continue." Its button
   navigates to `/bff/login?returnUrl=<path>`. No tab starts a login unprompted, so a logout in one tab never races a
   login in another, and a failing login stops at the screen. The BFF's `?error=` query shows on the screen as "Sign-in failed (<code>)."
8. **Sign-out.** The console posts the BFF's logout form with `sid` and `returnUrl=/`.
9. **The page's security.** The console host serves `index.html` for every non-file `GET` or `HEAD` path with a fresh
   nonce in place of `__NCODE_CSP_NONCE__`, through `SetHtmlPageSecurity` with `ManagementConsolePageSecurity`'s policy:
   `script-src 'nonce' 'strict-dynamic'; style-src 'nonce'; img-src 'self'` plus the branding logo and favicon origins;
   `connect-src 'self'; form-action 'self' <authority origin>; base-uri 'none'; frame-ancestors 'none'`. `/assets/*` is
   immutable. A missing bundle answers `503` and never stops startup.
10. **The bundle's build.** The bundle builds with `defineBffServedSurfaceConfig`: base `/` and the nonce placeholder.
    Emotion takes the nonce from `<meta property="csp-nonce">` through `readCspNonce`, which `@ncode/ui` provides.

## Options considered

- **Navigating to `/bff/login` on every end.** Rejected: a logout in one tab races a login in another, and a failing
  login loops.
- **Keeping `@ncode/auth`'s custody with a token-mediating backend.** Rejected: tokens stay in the browser
  ([RFC 10017](https://datatracker.ietf.org/doc/html/rfc10017) §6.2).
- **One `RuntimeConfig` with optional OpenID and BFF members.** Rejected: every consumer would test for members its
  surface always has.
- **A union of `OpenIdRuntimeConfig` and `BffRuntimeConfig`.** Chosen: a consumer narrows on `kind` once and then holds
  only the members its surface has.

## Consequences

- No script on the console's origin reads a token, because the browser holds none.
- The console's sign-in screen needs one click after an end of the session, and no tab starts a login on its own.
- The console bundle has no dev server of its own: only its BFF host serves the bootstrap document and forwards
  `/management`.
- The OpenID-served surfaces, the Playground and the conformance dashboard, keep the `openid` runtime configuration and
  their public-client custody.

## References

- [ADR-1434](1434-open-tabs-learn-that-a-bff-session-ended-from-its-next-answer-not-a-pushed-event.md)
- [ADR-1437](1437-the-management-console-is-a-confidential-client-whose-own-bff-host-serves-only-the-console.md)
- [RFC 10017](https://datatracker.ietf.org/doc/html/rfc10017)
