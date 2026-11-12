# 1297. A refresh token family expires at a fixed horizon that the token response announces

- **Status:** Accepted
- **Date:** 2026-10-29
- **Deciders:** NCode Group

## Context

A refresh token belongs to a rotation family (its lineage,
[ADR-1217](1217-a-reused-refresh-token-revokes-its-family-unless-it-is-a-retry-within-an-opt-in-interval.md)): every
token that rotation or a retry issues from one grant shares the family's `RefreshTokenLineageId`. The client setting
`refresh_token_expiration_policy` is `absolute` or `sliding`, and `refresh_token_lifetime` is the validity window of
the family. A public client's unbound token always rotates
([ADR-1216](1216-a-refresh-token-rotates-unless-it-is-bound-and-rotation-spends-it-with-its-successor-atomically.md)),
so a window that each rotation renewed would let a family live for as long as it is used. That would contradict the
documented meaning of `absolute` ("expire at a fixed point in time regardless of use"), and `sliding` promises an
extension "up to an absolute maximum" that would not exist. RFC 9700 §4.14.2 expects refresh tokens to expire, and the
established platforms bound a family absolutely and bound idleness separately: Auth0's absolute and inactivity
lifetimes, and IdentityServer's absolute and sliding lifetimes.

A client also cannot see when its refresh token's family ends: OAuth 2.0 defines `expires_in` only for the access
token. The de-facto `refresh_token_expires_in` member (sent by Microsoft Entra ID, among others) carries it. The BFF
caps a session at it, and only shortens a session by it
([ADR-1291](1291-the-bff-refreshes-a-sessions-token-single-flight-under-a-lease-its-winner-releases-by-owner-token.md)), which is sound only
when the value is a fixed horizon that never moves later within a family.

## Decision

1. **A family has a fixed horizon.** A rotation family expires `refresh_token_lifetime` (clients, `Min`, default
   30 days) after the grant that began it, however its tokens are used, rotated or retried.
   `RefreshTokenGrant.LineageCreatedWhen` records when the family began, in the payload rather than on the grant
   envelope because nothing queries it: a fresh issuance sets it to its own issuance time, and a rotation or a retry
   carries the presented token's value forward through `CreateSecurityTokenRequest.RefreshTokenLineageCreatedWhen`. A
   refresh token issued by token exchange starts its family at the subject token's `auth_time`, not at the exchange,
   and the exchange is refused with `invalid_grant` when the subject token carries no `auth_time` or the horizon
   counted from it has passed, so exchange cannot renew a family.
2. **The policies say how a token expires within its family.** Under `absolute`, every token of the family expires at
   the family's horizon. Under `sliding`, a token expires `refresh_token_sliding_lifetime` (clients, `Min`, default
   15 days) after it was issued or last used, never later than the family's horizon. `RefreshTokenExpiration` computes
   both, for the token service and the refresh grant handler alike.
3. **The horizon follows the setting.** The horizon is computed from the family's start and the client's current
   `refresh_token_lifetime`, so changing the lifetime changes every family: the refresh grant handler refuses a token
   whose family has passed its horizon with `invalid_grant`, even when the store still holds it as active, and moves a
   kept token's stored expiry to the policy's value when that differs from what is stored. A stored grant without a
   family start reads as one that began at `DateTimeOffset.MinValue` and is refused, which fails closed.
4. **The token response announces the horizon.** Every token response that carries a refresh token, at every grant that
   issues one and at a refresh that keeps, rotates or retries one, CIBA push delivery included, carries
   `refresh_token_expires_in` (`OpenIdProtocol.Parameters.RefreshTokenExpiresIn`): the whole seconds left until the
   family's horizon. It is not the token's own expiry, which under `sliding` moves later with use; a client may bound a
   session by it. `ITokenService` returns an `IssuedRefreshToken` (the token and the family's `LineageExpiresWhen`) for
   every refresh token it issues.
5. **The protocol client reads it leniently.** `TokenResponse.RefreshTokenExpiresIn` reads the member from the
   extension data and ignores a value that is not a positive whole number, because the member is advisory and a
   provider's odd value must not fail a token response. The BFF bounds its session by it
   ([ADR-1291](1291-the-bff-refreshes-a-sessions-token-single-flight-under-a-lease-its-winner-releases-by-owner-token.md)).
6. **Introspection honors the horizon.** Introspection reports a refresh token whose family has passed its horizon as
   inactive, as the refresh grant refuses it with `invalid_grant`
   ([ADR-1168](1168-introspection-reports-a-token-active-only-to-the-party-that-consumes-it.md)).

## Options considered

- **Announce each token's own expiry.** Rejected: under `sliding` it moves later with every use, so a client that
  bounds a session by it would end active sessions early, and under rotation it would not be the family's horizon.
- **Renew the lifetime on rotation and add a separate maximum.** Rejected: `absolute` would still not mean absolute,
  and one more setting would describe what `refresh_token_lifetime` should mean.
- **Store the family's horizon rather than its start.** Rejected: a stored horizon would ignore a later change to
  `refresh_token_lifetime`, so shortening the lifetime would not end existing families until their horizon.
- **A grant-envelope column for the family's start.** Rejected: the lineage id is on the envelope because the store
  queries it
  ([ADR-1217](1217-a-reused-refresh-token-revokes-its-family-unless-it-is-a-retry-within-an-opt-in-interval.md)); the
  start is never queried.
- **Send `refresh_token_expires_in` only under `absolute`.** Rejected: once the family has a fixed horizon under both
  policies, the value is equally fixed under both.
- **A fixed horizon from the family's start, announced in every token response that carries a refresh token.** Chosen.

## Consequences

- A family ends `refresh_token_lifetime` (default 30 days) after its first token, so a client that rotates its tokens
  indefinitely must sign the user in again then. Under `sliding`, an idle family ends after
  `refresh_token_sliding_lifetime` (default 15 days).
- A stored grant without a family start is refused at its next use, so the user signs in again.
- `ITokenService.CreateRefreshTokenAsync`, `RotateRefreshTokenAsync` and `ReissueRefreshTokenAsync` return
  `IssuedRefreshToken`, and `RefreshTokenGrant` carries a positional `LineageCreatedWhen`.
- A BFF in front of the NCode OP caps its session at the family's horizon from the first token response.

## References

- [RFC 9700 §4.14.2](https://www.rfc-editor.org/rfc/rfc9700#section-4.14.2) — refresh token protection
- [ADR-1216](1216-a-refresh-token-rotates-unless-it-is-bound-and-rotation-spends-it-with-its-successor-atomically.md) — when a refresh token is rotated
- [ADR-1217](1217-a-reused-refresh-token-revokes-its-family-unless-it-is-a-retry-within-an-opt-in-interval.md) — refresh token lineage and retries within a reuse interval
- [ADR-1291](1291-the-bff-refreshes-a-sessions-token-single-flight-under-a-lease-its-winner-releases-by-owner-token.md) — the BFF caps a session at `refresh_token_expires_in`
- [ADR-1168](1168-introspection-reports-a-token-active-only-to-the-party-that-consumes-it.md) — introspection of an expired family
