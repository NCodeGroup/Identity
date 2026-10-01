# 28. Token revocation revokes refresh tokens only; stateless access tokens expire

- **Status:** Accepted
- **Date:** 2026-09-30
- **Deciders:** NCode Group

## Context

<see href="https://datatracker.ietf.org/doc/html/rfc7009">RFC 7009</see> defines a token revocation endpoint so a client
can proactively invalidate a token it no longer needs (for example, at logout). The spec lets the server decide which
token types it supports and is deliberately lenient: the endpoint responds `200 OK` whether or not a token was actually
revoked, so it never becomes an oracle for token validity.

This server issues two token shapes with different natures: a **refresh token** is a persisted grant (a row with a
`RevokedWhen` column, already honored by the refresh-token grant path), while an **access token** is a self-contained,
signed JWT that carries no server-side state. That difference determines what "revoke" can even mean for each.

## Decision

**The revocation endpoint (`/oauth2/revoke`) revokes refresh tokens; an access token is accepted but is a no-op,
because a stateless JWT cannot be invalidated without introducing a denylist.** The endpoint always responds
`200 OK` for an authenticated request (RFC 7009), whether the token was revoked, unknown, or non-revocable, so it
reveals nothing about token state. A missing `token` parameter is the one hard error (`400 invalid_request`).

Two further rules:

- **Ownership.** Only the client the refresh token was issued to may revoke it (RFC 7009 section 2.1): the handler loads
  the grant and compares its `ClientId` to the authenticated client before setting `RevokedWhen`.
- **Shape.** The endpoint follows the established OAuth-endpoint and mediator conventions
  ([ADR-0001](0001-mediator-vs-dependency-injection-logic-classes.md)): a thin `IOpenIdEndpointProvider` authenticates
  the client and parses the form into a typed `ITokenRevocationRequest`, then delegates to a `RevokeTokenCommand`
  mediator handler that owns the revocation logic — mirroring the token endpoint. The endpoint auto-advertises as
  `revocation_endpoint` in discovery.

## Options considered

- **Revoke access tokens too, via a denylist.** Rejected for now: it reintroduces per-access-token server state and a
  lookup on every resource-server call, which is the cost a stateless JWT exists to avoid. Short access-token lifetimes
  bound the exposure instead; a denylist can be added later if a hard "kill this access token now" requirement appears.
- **Return `400`/`404` for an unknown or non-revocable token.** Rejected: it violates RFC 7009 and turns the endpoint
  into an oracle that distinguishes real from fake tokens.
- **Skip the ownership check and revoke any presented token.** Rejected: a client could revoke another client's token if
  it somehow obtained the value; the ownership check is cheap and closes that gap.
- **Refresh-token revocation with an always-`200` response and an ownership check (chosen).** Standards-compliant,
  reveals nothing, and matches the tokens that actually carry revocable state.

## Consequences

- A client can revoke a refresh token (ending the offline-access line for that grant); the next refresh attempt sees the
  grant as revoked and fails. Already-issued access tokens remain valid until they expire — callers that need immediate
  cutoff must rely on short access-token lifetimes.
- Revoking an access token at the endpoint succeeds (`200`) but does nothing; this is intentional and documented.
- The endpoint needs client authentication, so only a registered client (public or confidential) can reach the
  revocation logic.
- A future "revoke access tokens immediately" requirement would be an additive denylist/introspection-state feature, not
  a change to this contract.

## References

- [ADR-0001](0001-mediator-vs-dependency-injection-logic-classes.md) — the mediator command/handler shape the endpoint
  follows.
- [ADR-0020](0020-grant-management-read-surface.md) / [ADR-0022](0022-grant-filters-and-bulk-revocation.md) — the
  management-side grant soft-revoke this client-facing endpoint reuses (`RevokedWhen`).
- [ADR-0027](0027-access-token-audience-from-resource-servers.md) — the audience-bound access tokens whose statelessness
  motivates "refresh tokens only."
