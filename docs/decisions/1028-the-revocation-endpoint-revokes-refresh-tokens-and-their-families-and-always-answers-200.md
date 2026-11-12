# 1028. The revocation endpoint revokes refresh tokens and their families, and always answers 200

- **Status:** Accepted
- **Date:** 2026-09-30
- **Deciders:** NCode Group

## Context

<see href="https://datatracker.ietf.org/doc/html/rfc7009">RFC 7009</see> defines a token revocation endpoint so a client
can proactively invalidate a token it does not need any more, for example at logout. The specification lets the server decide which token types it
supports and is deliberately lenient: the endpoint answers `200 OK` whether or not a token was revoked, so it never
becomes an oracle for token validity.

The server issues two token shapes with different natures. A refresh token is a persisted grant, a row with a
`RevokedWhen` column that the refresh-token grant path already honors. An access token is a self-contained, signed JWT
that carries no server-side state. That difference determines what "revoke" can mean for each.

## Decision

**The revocation endpoint (`/oauth2/revoke`) revokes refresh tokens and their rotation families. An access token is
accepted and nothing happens, because a stateless JWT cannot be invalidated without a denylist, which the server does
not keep.** The endpoint answers `200` for every authenticated request, whether the token was revoked, unknown or not
revocable, so it reveals nothing about token state. A missing `token` parameter is the one hard error
(`400 invalid_request`).

- **Shape.** The endpoint follows the mediator convention of
  [ADR-1001](1001-behavior-is-a-service-a-strategy-collection-or-a-mediator-seam-chosen-by-one-ladder.md). It is a thin
  `IOpenIdEndpointProvider` that authenticates the client, loads a typed `ITokenRevocationRequest` and sends a
  `RevokeTokenCommand` to `DefaultRevokeTokenHandler` (`High` priority), which owns the revocation logic, mirroring the
  token endpoint. It is advertised in discovery as `revocation_endpoint`.
- **Ownership.** Only the client a refresh token was issued to may revoke it (RFC 7009 section 2.1). The handler
  compares the grant's `ClientId` ordinally with the authenticated client and does nothing otherwise.
- **Family.** A refresh token that names a lineage is revoked with its whole rotation family through
  `IPersistedGrantService.RevokeByRefreshTokenLineageAsync`, which ends every branch. A refresh token with no lineage is
  revoked with `SetRevokedOnceAsync`. Both retry a lost concurrency race
  ([ADR-1222](1222-a-grants-payload-is-changed-by-a-read-modify-write.md)).
- **Audit.** A revocation records the `token.revoked` audit event through
  `IAuditEventRecorder.RecordTokenRevokedAsync` with the grant's subject.
- **Access tokens.** An access token already issued stays valid at external resource servers until it expires. The
  OP's own endpoints reject one whose session was revoked
  ([ADR-1167](1167-the-server-validates-the-tokens-it-issued-under-one-policy.md),
  [ADR-1112](1112-every-end-of-a-session-runs-one-termination-seam-once-revoking-the-grants-issued-under-it.md)), and short access-token lifetimes bound the rest.
- **Discovery.** Discovery mirrors the token endpoint's client-authentication methods and signing algorithms into the
  `revocation_endpoint_*` members
  ([ADR-1168](1168-introspection-reports-a-token-active-only-to-the-party-that-consumes-it.md)).

## Options considered

- **Revoke access tokens too, through a denylist.** Rejected: it adds per-access-token server state and a lookup on
  every resource-server call, which is the cost a stateless JWT exists to avoid. Short access-token lifetimes bound the
  exposure instead.
- **Answer `400` or `404` for an unknown or non-revocable token.** Rejected: it violates RFC 7009 and turns the endpoint
  into an oracle that tells real tokens from fake ones.
- **Skip the ownership check and revoke any presented token.** Rejected: a client that somehow obtained another
  client's token value could revoke it. The check is cheap and closes that gap.
- **Refresh-token revocation with an always-`200` answer and an ownership check (chosen).** It is standards-compliant,
  reveals nothing and matches the tokens that carry revocable state.

## Consequences

- A client can revoke a refresh token and, with it, the whole rotation family, ending the offline-access line for that
  grant. The next refresh attempt sees the grant as revoked and fails.
- Already-issued access tokens remain valid at external resource servers until they expire, so a caller that needs an
  immediate cutoff relies on short access-token lifetimes.
- Revoking an access token at the endpoint succeeds with `200` and does nothing.
- The endpoint needs client authentication, so only a registered client, public or confidential, reaches the revocation
  logic.

## References

- [RFC 7009](https://datatracker.ietf.org/doc/html/rfc7009), OAuth 2.0 token revocation.
- [ADR-1001](1001-behavior-is-a-service-a-strategy-collection-or-a-mediator-seam-chosen-by-one-ladder.md): the mediator
  command and handler shape the endpoint follows.
- [ADR-1020](1020-a-tenants-grants-are-listed-inspected-and-revoked-by-opaque-identifier-never-by-their-key.md): the
  management-side grant soft-revoke that this client-facing endpoint shares (`RevokedWhen`).
- [ADR-1026](1026-resource-servers-own-scopes-settings-and-keys-and-decide-a-clients-scopes-and-token-audiences.md): the
  audience-bound access tokens whose statelessness motivates revoking refresh tokens only.
- [ADR-1112](1112-every-end-of-a-session-runs-one-termination-seam-once-revoking-the-grants-issued-under-it.md) and
  [ADR-1167](1167-the-server-validates-the-tokens-it-issued-under-one-policy.md): session revocation and the OP's own
  token validation.
- [ADR-1222](1222-a-grants-payload-is-changed-by-a-read-modify-write.md): the retry of a lost concurrency race.
- [ADR-1168](1168-introspection-reports-a-token-active-only-to-the-party-that-consumes-it.md): the discovery members that
  mirror the token endpoint.
