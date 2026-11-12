# 1293. The BFF ends sessions by a guarded logout form post and by back-channel logout through indexes

- **Status:** Accepted
- **Date:** 2026-11-01
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0129](project-template/0129-browser-facing-requests-are-safe-by-construction.md)

## Context

A backend-for-frontend session
([ADR-1290](1290-the-bff-keeps-each-session-as-an-encrypted-server-side-record-under-a-cookie-handle.md)) ends in four
ways: it expires, a refresh fails
([ADR-1291](1291-the-bff-refreshes-a-sessions-token-single-flight-under-a-lease-its-winner-releases-by-owner-token.md)),
the user logs out at the BFF, or the provider ends the user's session and tells the BFF through OpenID Connect
Back-Channel Logout. The design (§5.8) asks for RP-initiated logout that cannot be forced cross-site, and for a
back-channel receiver that ends the session on every replica. Both build on the protocol client's logout-token validator
([ADR-1274](1274-the-protocol-client-validates-logout-tokens-and-records-each-only-after-the-client-handles-it.md)) and
on the store's `sid` and `sub` indexes.

[project-template ADR-0129](project-template/0129-browser-facing-requests-are-safe-by-construction.md) allows no state
change on a `GET`. A `GET` link that ends the session runs on a browser's prefetch, and a link that carries the guard
puts it into history, server logs and the `Referer` header. A `fetch` cannot follow the redirect to the provider's
end-session endpoint, so logout cannot be a script-driven request either. Logout is a top-level navigation and cannot
carry the anti-forgery header
([ADR-1292](1292-the-bffs-cookie-authenticates-only-guarded-endpoints-which-refuse-cross-site-requests-and-reload-cookieless-navigations.md)),
so a per-session guard stands in for it.

## Decision

**`POST /bff/logout` ends the session only when its form body carries the session's logout guard, then sends the
browser to the provider's end-session endpoint; `POST /bff/backchannel-logout` finds the sessions a valid logout token
names through the store's indexes, ends them, and only then records the token.**

1. **The logout guard.** Each session holds a random logout guard in its encrypted payload. `POST /bff/logout` is a form
   post whose `sid` field appears exactly once and equals the guard, compared with
   `CryptographicOperations.FixedTimeEquals`. A request that is not a form post, or whose guard is missing or
   mismatched, answers `400` and the session stays. A `sid` in the query string is ignored, and `GET /bff/logout`
   answers `405`.
2. **The form.** `/bff/user` returns `logoutUrl` (a path only) and `logoutSid`, and the principal carries
   `bff_logout_url` and `bff_logout_sid`. A frontend renders `<form method="post" action="{logoutUrl}">` with a hidden
   `sid` input that holds `logoutSid`. A form post is a top-level navigation, so the browser follows the end-session
   redirect, and the guard never appears in a URL, history, server logs or `Referer`.
3. **Why the guard holds.** The guard is the form's synchronizer token
   ([project-template ADR-0129](project-template/0129-browser-facing-requests-are-safe-by-construction.md) decision 3).
   A cross-site post carries no session cookie (`SameSite=Strict`), and a same-site one does not know the guard.
4. **The return URL.** `returnUrl` is optional, defaults to `/`, must appear at most once and must be in the frontend's
   `ReturnUrls`; otherwise logout answers `400`.
5. **Idempotent.** Without a session, logout redirects to the return URL, so a second click or a stale tab is harmless.
6. **Ending the session.** The session is removed with `CancellationToken.None` and the cookie is deleted first, so a
   client that disconnects cannot leave the session behind. The refresh token is then revoked (RFC 7009) with
   `CancellationToken.None`, bounded by the client's request timeout, and any failure, a cancellation included, is
   logged and never fails the logout. The end is audited as `bff.session.ended` with `logged_out`.
7. **The end-session redirect.** When the provider advertises `end_session_endpoint`, the browser is redirected there
   with `id_token_hint`, `client_id`, `post_logout_redirect_uri` (the absolute return URL) and a random `state`. Without
   the endpoint, or when the provider cannot be reached, the browser is redirected to the return URL; the local session
   has already ended.
8. **Sign-out through the scheme.** `HttpContext.SignOutAsync("Bff")` ends the current session the same way, so an
   application's own sign-out code works without the endpoint.
9. **The back-channel receiver.** `POST /bff/backchannel-logout` reads exactly one `logout_token` from a form body and
   validates it with `ILogoutTokenValidator` against the receiving frontend's registration, which checks the signature,
   issuer, audience, `events` claim, the absence of `nonce`, and replay. It then looks the sessions up through the
   store's indexes: by `sid` when the token has one, and by `sub` otherwise. Each candidate must match the token's
   issuer, and its `sid` when the token has one, and must belong to the receiving frontend
   ([ADR-1432](1432-one-bff-serves-several-frontends-each-selected-per-request-by-an-open-rule.md)). It is then ended
   (`backchannel_logout`) on every replica at once, because the record itself is removed.
10. **`sid` is required by default.** `BackchannelLogoutSessionRequired` defaults to `true`, matching the
    `backchannel_logout_session_required` metadata the client registers, so a logout token without `sid` is rejected and
    one provider session never ends every BFF session of the user. Setting it to `false` accepts `sub`-only tokens and
    ends all of the user's sessions at that issuer.
11. **Record after handling.** The token's `jti` is recorded as handled only after the sessions it names have ended
    ([ADR-1274](1274-the-protocol-client-validates-logout-tokens-and-records-each-only-after-the-client-handles-it.md)),
    so a provider retry after a store failure is not refused as a replay.
12. **Responses.** The receiver answers `200` with `Cache-Control: no-store`, and `400` when the request or token is
    invalid, the provider's keys cannot be read (`provider_unavailable`) or a session cannot be ended
    (`session_end_failed`); a `400` records no `jti`, so the provider's retry is handled. Receipts and rejections are
    audited as `bff.backchannel_logout.received` and `bff.backchannel_logout.rejected`.
13. **Other tabs.** Other open tabs learn of the end from their next guarded call
    ([ADR-1434](1434-open-tabs-learn-that-a-bff-session-ended-from-its-next-answer-not-a-pushed-event.md)).

## Options considered

- **`GET` logout with the guard in the query string.** Rejected: it changes state on a `GET`
  ([project-template ADR-0129](project-template/0129-browser-facing-requests-are-safe-by-construction.md) decision 2),
  and the guard lands in history, logs and `Referer`.
- **A `fetch` `POST` with the static anti-forgery header.** Rejected: a `fetch` cannot follow the redirect to the
  provider's end-session endpoint.
- **No guard on logout.** Rejected: any page could log the user out (logout CSRF).
- **Blocking logout on a failed revocation.** Rejected: the user asked to leave, and the session is gone locally either
  way; the refresh token expires at the provider.
- **Finding back-channel sessions by scanning the store.** Rejected: it does not scale and needs a store that can list
  keys; the `sid` and `sub` membership indexes answer the lookup directly.
- **Recording the `jti` before ending the sessions.** Rejected: a failure would turn the provider's retry into a replay.
- **A form post that carries the guard, and back-channel lookup through the indexes (chosen).**

## Consequences

- A frontend renders a form instead of a link.
- Ending the provider's session ends the BFF session on every replica, which an end-to-end test proves against the NCode
  OP.
- A BFF client registers `backchannel_logout_uri` and `backchannel_logout_session_required=true`
  ([ADR-1294](1294-a-bff-client-is-registered-with-explicit-metadata-not-a-bff-security-profile.md)).
- A logout token ends only the receiving frontend's sessions.

## References

- [`BffLogoutHandler.cs`](../../src/NCode.Identity.Bff/Logout/BffLogoutHandler.cs),
  [`BffUserHandler.cs`](../../src/NCode.Identity.Bff/Endpoints/BffUserHandler.cs) and
  [`BffEndpointRouteBuilderExtensions.cs`](../../src/NCode.Identity.Bff/Endpoints/BffEndpointRouteBuilderExtensions.cs)
- [Backend-for-frontend design](../backend-for-frontend.md) §5.8
- [ADR-1274](1274-the-protocol-client-validates-logout-tokens-and-records-each-only-after-the-client-handles-it.md) —
  logout-token validation and record after handling
- [ADR-1290](1290-the-bff-keeps-each-session-as-an-encrypted-server-side-record-under-a-cookie-handle.md) — the session
  record and indexes
- OpenID Connect RP-Initiated Logout 1.0, OpenID Connect Back-Channel Logout 1.0, RFC 7009 (revocation)
- Issues #123, #394 and [NCodeGroup/identity#442](https://github.com/NCodeGroup/identity/issues/442)
