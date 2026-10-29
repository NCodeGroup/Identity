# 293. The BFF ends sessions by guarded logout and by back-channel logout through indexes

- **Status:** Accepted
- **Date:** 2026-10-29
- **Deciders:** NCode Group

## Context

A backend-for-frontend session
([ADR-0290](0290-the-bff-keeps-each-session-as-an-encrypted-server-side-record-under-a-cookie-handle.md)) ends in four
ways: it expires, a refresh fails
([ADR-0291](0291-the-bff-refreshes-a-session-single-flight-under-a-lease-on-its-refresh-token.md)), the user logs out
at the BFF, or the provider ends the user's session and tells the BFF through OpenID Connect Back-Channel Logout. The
design (§5.8) asked for RP-initiated logout that cannot be forced cross-site, and for a back-channel receiver that ends
the session on every replica. Phase 2 (issue #394) builds both on the protocol client's logout-token validator
([ADR-0274](0274-the-protocol-client-validates-logout-tokens-and-rejects-replays.md)) and on the store's `sid` and
`sub` indexes. This ADR records how.

## Decision

**`GET /bff/logout` ends the session only with the session's logout guard, then sends the browser to the provider's
end-session endpoint; `POST /bff/backchannel-logout` finds the sessions a valid logout token names through the store's
indexes, ends them, and only then records the token.**

1. **The logout guard.** Each session holds a random logout guard in its encrypted payload, and `/bff/user` returns
   `logoutUrl` as `/bff/logout?sid=<guard>`; that URL is the only place the guard leaves the server. Logout is a top-level
   navigation, so it cannot carry the anti-forgery header
   ([ADR-0292](0292-the-bff-accepts-its-cookie-only-with-a-static-header-and-fetch-metadata.md)); the guard stands in
   for it, so a cross-site page cannot log the user out. The `sid` parameter must appear once and equal the guard,
   compared with `CryptographicOperations.FixedTimeEquals`; otherwise logout answers `400` and the session stays.
2. **The return URL.** `returnUrl` is optional, defaults to `/`, must appear at most once and must be allow-listed in
   `Bff:ReturnUrls`; otherwise logout answers `400`.
3. **Idempotent.** Without a session, logout redirects to the return URL, so a second click or a stale tab is harmless.
4. **Ending the session.** The session is removed with `CancellationToken.None` and the cookie is deleted first, so a
   client that disconnects cannot leave the session behind. The refresh token is then revoked (RFC 7009) with
   `CancellationToken.None`, bounded by the client's request timeout, and any failure, a cancellation included, is
   logged and never fails the logout. The end is audited as `bff.session.ended` with `logged_out`.
5. **The end-session redirect.** When the provider advertises `end_session_endpoint`, the browser is redirected there
   with `id_token_hint`, `client_id`, `post_logout_redirect_uri` (the absolute return URL) and a random `state`.
   Without the endpoint, or when the provider cannot be reached, the browser is redirected to the return URL; the local
   session has already ended.
6. **Sign-out through the scheme.** `HttpContext.SignOutAsync("Bff")` ends the current session the same way, so an
   application's own sign-out code works without the endpoint.
7. **The back-channel receiver.** `POST /bff/backchannel-logout` reads exactly one `logout_token` from a form body and
   validates it with `ILogoutTokenValidator` against the BFF's registration, which checks the signature, issuer,
   audience, `events` claim, the absence of `nonce`, and replay. It then looks the sessions up through the store's
   indexes: by `sid` when the token has one, and by `sub` otherwise. Each candidate's metadata must match the token's
   issuer, and its `sid` when the token has one, before it is ended (`backchannel_logout`) on every replica at once,
   because the record itself is removed.
8. **`sid` is required by default.** `Bff:BackchannelLogoutSessionRequired` defaults to `true`, matching the
   `backchannel_logout_session_required` metadata the client registers, so a logout token without `sid` is rejected
   and one provider session never ends every BFF session of the user. Setting it to `false` accepts `sub`-only tokens
   and ends all of the user's sessions at that issuer.
9. **Record after handling.** The token's `jti` is recorded as handled only after the sessions it names have ended
   ([ADR-0284](0284-a-logout-token-is-recorded-only-after-the-client-handles-it.md)), so a provider retry after a
   store failure is not refused as a replay.
10. **Responses.** The receiver answers `200` with `Cache-Control: no-store`, and `400` when the request or token is
    invalid, the provider's keys cannot be read (`provider_unavailable`) or a session cannot be ended
    (`session_end_failed`); a `400` records no `jti`, so the provider's retry is handled. Receipts and rejections are
    audited as `bff.backchannel_logout.received` and `bff.backchannel_logout.rejected`.

## Options considered

- **Logout by `POST` with the anti-forgery header.** Rejected: an RP-initiated logout ends in a top-level navigation to
  the provider, which a `fetch` cannot follow, and a frontend expects a plain link.
- **No guard on `GET /bff/logout`.** Rejected: any page could log the user out (logout CSRF).
- **Blocking logout on a failed revocation.** Rejected: the user asked to leave, and the session is gone locally either
  way; the refresh token expires at the provider.
- **Finding back-channel sessions by scanning the store.** Rejected: it does not scale and needs a store that can list
  keys; the `sid` and `sub` membership indexes answer the lookup directly.
- **Recording the `jti` before ending the sessions.** Rejected by ADR-0284: a failure would turn the provider's retry
  into a replay.

## Consequences

- A frontend logs out by navigating to the `logoutUrl` that `/bff/user` returns.
- Ending the provider's session ends the BFF session on every replica, which an end-to-end test proves against the
  NCode OP.
- A BFF client registers `backchannel_logout_uri` and `backchannel_logout_session_required=true`
  ([ADR-0294](0294-a-bff-client-is-registered-with-explicit-metadata-not-a-bff-security-profile.md)).
- Telling other open tabs that the session ended is not part of phase 2; their next API call answers `401`.

## References

- [Backend-for-frontend design](../backend-for-frontend.md) §5.8
- [ADR-0274](0274-the-protocol-client-validates-logout-tokens-and-rejects-replays.md) — logout-token validation
- [ADR-0284](0284-a-logout-token-is-recorded-only-after-the-client-handles-it.md) — record after handling
- [ADR-0290](0290-the-bff-keeps-each-session-as-an-encrypted-server-side-record-under-a-cookie-handle.md) — the session record and indexes
- OpenID Connect RP-Initiated Logout 1.0, OpenID Connect Back-Channel Logout 1.0, RFC 7009 (revocation)
- Issues #123 and #394
