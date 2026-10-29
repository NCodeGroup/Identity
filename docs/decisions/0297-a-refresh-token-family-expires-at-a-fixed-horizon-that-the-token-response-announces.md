# 297. A refresh token family expires at a fixed horizon that the token response announces

- **Status:** Accepted
- **Date:** 2026-10-29
- **Deciders:** NCode Group

## Context

A refresh token belongs to a rotation family (its lineage,
[ADR-0087](0087-refresh-token-reuse-detection-and-family-revocation.md)): every token that rotation or a retry issues
from one grant shares the family's `RefreshTokenLineageId`. The client setting `refresh_token_expiration_policy` is
`absolute` or `sliding`, and `refresh_token_lifetime` was "the validity window for issued refresh tokens".

Each issued token got the full `refresh_token_lifetime` from the moment it was issued. A token that is never rotated
did expire at a fixed point, but a rotation gave the successor a new full lifetime, so under rotation, which a public
client's unbound token always gets ([ADR-0216](0216-a-refresh-token-is-rotated-unless-it-is-sender-constrained.md)),
`absolute` behaved like `sliding`, and the family lived for as long as it was used. That contradicted the documented
meaning of `absolute` ("expire at a fixed point in time regardless of use"). `sliding` promised an extension "up to an
absolute maximum", but no maximum existed. RFC 9700 §4.14.2 expects refresh tokens to expire, and the established
platforms bound a family absolutely and bound idleness separately: Auth0's absolute and inactivity lifetimes, and
IdentityServer's absolute and sliding lifetimes.

A client also cannot see when its refresh token's family ends: OAuth 2.0 defines `expires_in` only for the access
token. The de-facto `refresh_token_expires_in` member (sent by Microsoft Entra ID, among others) carries it. The BFF
already caps a session at it, and only shortens a session by it
([ADR-0291](0291-the-bff-refreshes-a-session-single-flight-under-a-lease-on-its-refresh-token.md) point 10), which is
sound only when the value is a fixed horizon that never moves later within a family.

## Decision

1. **A family has a fixed horizon.** A rotation family expires `refresh_token_lifetime` after the grant that began it,
   however often its tokens are used, rotated or retried. `RefreshTokenGrant.LineageCreatedWhen` records when the
   family began: a fresh issuance sets it to its own issuance time, and a rotation or a retry carries the presented
   token's value forward through `CreateSecurityTokenRequest.RefreshTokenLineageCreatedWhen`. It lives in the payload
   rather than on the grant envelope because nothing queries it; it is read only when the token is presented.
2. **The policies say how a token expires within its family.** Under `absolute`, every token of the family expires at
   the family's horizon. Under `sliding`, a token expires `refresh_token_sliding_lifetime` (new, default 15 days,
   a ceiling like the other lifetimes) after it was issued or last used, never later than the family's horizon; a
   rotation slides the successor, and a kept token's stored expiry slides on each use. `RefreshTokenExpiration`
   computes both, for the token service and the refresh grant handler alike.
3. **The horizon follows the setting.** The horizon is computed from the family's start and the client's current
   `refresh_token_lifetime`, so changing the lifetime changes every family: the refresh grant handler refuses a token
   whose family has passed its horizon with `invalid_grant`, even if the store still holds it as active, and moves a
   kept token's stored expiry to the policy's value when that differs from what is stored. A stored grant without a
   family start reads as one that began at `DateTimeOffset.MinValue` and is refused, which fails closed.
4. **The token response announces the horizon.** Whenever a token response carries a refresh token, at every grant
   that issues one and at a refresh that keeps, rotates or retries one, it carries `refresh_token_expires_in`: the
   whole seconds left until the family's horizon. It is not the token's own expiry, which under `sliding` moves
   later with use; a client may bound a session by it. `ITokenService` returns an `IssuedRefreshToken` (the token
   and the family's `LineageExpiresWhen`) for every refresh token it issues, and CIBA push delivery carries the member
   too. The name is `OpenIdProtocol.Parameters.RefreshTokenExpiresIn`.
5. **The protocol client reads it leniently.** `TokenResponse.RefreshTokenExpiresIn` reads the member from the
   extension data and ignores a value that is not a positive whole number, because the member is advisory and a
   provider's odd value must not fail a token response; the BFF reads it there.

## Options considered

- **Announce each token's own expiry.** Rejected: under `sliding` it moves later with every use, so a client that
  bounds a session by it would end active sessions early, and under rotation it would not be the family's horizon.
- **Keep renewing the lifetime on rotation and add a separate maximum.** Rejected: `absolute` would still not mean
  absolute, and one more setting would describe what `refresh_token_lifetime` should have meant.
- **Store the family's horizon rather than its start.** Rejected: a stored horizon would ignore a later change to
  `refresh_token_lifetime`, so shortening the lifetime would not end existing families until their horizon.
- **A grant-envelope column for the family's start.** Rejected: the lineage id is on the envelope because the store
  queries it ([ADR-0087](0087-refresh-token-reuse-detection-and-family-revocation.md)); the start is never queried.
- **Send `refresh_token_expires_in` only under `absolute`.** Rejected: once the family has a fixed horizon under both
  policies, the value is equally fixed under both.

## Consequences

- A family now ends `refresh_token_lifetime` (default 30 days) after its first token, so a client that rotated its
  tokens indefinitely must sign the user in again then. Under `sliding`, an idle family ends after
  `refresh_token_sliding_lifetime` (default 15 days).
- Refresh tokens stored before this change carry no family start and are refused at their next use; the users sign
  in again. Nothing is shipped, so no migration is offered
  ([ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md)).
- `ITokenService.CreateRefreshTokenAsync`, `RotateRefreshTokenAsync` and `ReissueRefreshTokenAsync` return
  `IssuedRefreshToken`, and `RefreshTokenGrant` gains a positional `LineageCreatedWhen`: breaking changes to the
  public API, acceptable before release.
- A BFF in front of the NCode OP caps its session at the family's horizon from the first token response
  ([ADR-0291](0291-the-bff-refreshes-a-session-single-flight-under-a-lease-on-its-refresh-token.md) point 10).

## References

- [RFC 9700 §4.14.2](https://www.rfc-editor.org/rfc/rfc9700#section-4.14.2) — refresh token protection
- [ADR-0087](0087-refresh-token-reuse-detection-and-family-revocation.md) — refresh token lineage
- [ADR-0216](0216-a-refresh-token-is-rotated-unless-it-is-sender-constrained.md) — when a refresh token is rotated
- [ADR-0217](0217-a-rotated-refresh-token-may-be-retried-within-a-reuse-interval.md) — retries within a reuse interval
- [ADR-0291](0291-the-bff-refreshes-a-session-single-flight-under-a-lease-on-its-refresh-token.md) — the BFF caps a
  session at `refresh_token_expires_in`
