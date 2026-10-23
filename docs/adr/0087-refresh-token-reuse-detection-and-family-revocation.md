# 87. Refresh-token reuse detection revokes the whole rotation family

- **Status:** Accepted (amended by [ADR-0211](0211-a-rotated-refresh-token-is-spent-with-its-successor.md): rotation
  spends the presented token with its successor in one unit of work, and a lost spend is a reuse)
- **Date:** 2026-10-12
- **Deciders:** NCode Group

## Context

With refresh-token rotation enabled, each use of a refresh token issues a replacement and revokes the one presented
([ADR-0028](0028-token-revocation-refresh-tokens-only.md) covers the revocation column these grants carry). If a refresh
token leaks, both the attacker and the legitimate client eventually present the same token value; whichever arrives
second presents an **already-revoked** token. The server cannot tell which party is legitimate, but that replay is an
unambiguous signal that the token was compromised.

The server already **detected and audited** this replay (`token.refresh.replay`), but it had no configurable response:
it rejected the single replayed token and left the rest of the family — including the currently-active token the
attacker (or the victim) still holds — usable. The OAuth 2.0 Security Best Current Practice
([RFC 9700](https://www.rfc-editor.org/rfc/rfc9700) §4.14.2, formerly draft-ietf-oauth-security-topics) is explicit: on
detecting refresh-token reuse, the authorization server **SHOULD revoke all refresh tokens for that authorization
grant** — the entire rotation family — which is what actually stops the attack. This is Auth0's "automatic reuse
detection" behavior.

Implementing "revoke the family" requires a way to identify a family. The grant model had no link between a rotated
token and its predecessor: each refresh-token grant was keyed only by its own token value, with `ClientId`,
`SubjectId`, and `SessionId` as the only groupings. Those are too coarse — a subject can hold several independent
refresh-token families at one client (multiple devices with no server-side session), so revoking by subject+client
would sign the victim out everywhere, not just kill the compromised chain.

## Decision

**Introduce a server-generated refresh-token _lineage id_ that every token in a rotation family shares, and on reuse
detection revoke the whole family by that id. The response is governed by a new per-client `refresh_token_reuse_policy`
setting that defaults to the secure `revoke_all`.**

Concretely:

- **Lineage id.** A fresh refresh-token issuance (authorization-code, password, device-code, CIBA, …) starts a new
  family with a new opaque `RefreshTokenLineageId` (a `CryptoService.GenerateResourceId()` value). A rotation inherits
  its predecessor's lineage id, so every descendant of one authorization grant carries the same id. The id lives on the
  **grant envelope** (`PersistedGrant` / `PersistedGrant<TPayload>`), not inside the refresh-token payload, because it is
  queryable metadata like `SubjectId`/`SessionId` rather than token-type-specific data; it is persisted as a nullable,
  indexed `RefreshTokenLineageId` column on the grants table (`(TenantId, RefreshTokenLineageId)` index) via an EF
  migration.
- **Reuse policy.** The `refresh_token_reuse_policy` setting takes `none` (reject + audit only, the prior behavior) or
  `revoke_all` (reject + audit + revoke the family). The default is **`revoke_all`**: reuse detection should revoke by
  default, and the operation is a no-op in the common case where no token is ever replayed. The setting is non-advertised
  (server-side security behavior, not client-negotiable) and uses `Replace` merge semantics like the sibling
  `refresh_token_expiration_policy`.
- **Revocation path.** `IGrantStore.RevokeByRefreshTokenLineageAsync` soft-revokes every **active** grant in the family
  (idempotent: already-revoked rows are untouched, so the concurrency interceptor bumps only the rows it changes, per
  [ADR-0012](0012-interceptor-managed-concurrency-tokens.md)). It loads and mutates the matched rows rather than issuing
  a set-based `ExecuteUpdate`, matching the established bulk-revocation pattern
  ([ADR-0022](0022-grant-filters-and-bulk-revocation.md)) so the interceptor still stamps each changed row;
  `IPersistedGrantService.RevokeByRefreshTokenLineageAsync` wraps it for the grant handler. The handler calls it, after
  the replay audit, for a presented token that is revoked and for one whose spend a concurrent request won
  ([ADR-0211](0211-a-rotated-refresh-token-is-spent-with-its-successor.md)).

## Options considered

- **Revoke by subject + client (no schema change).** Rejected: it over-revokes. A subject with several concurrent
  refresh-token families at one client (multiple devices, no server-side session) would lose all of them when any one is
  replayed. It is coarser than the "authorization grant" scope RFC 9700 names.
- **Revoke by session (`sid`).** Rejected as the primary mechanism: many refresh-token families exist outside any
  server-side session (`sid` is null), so it does not generalize; session logout already revokes by `sid`
  ([ADR-0077](0077-session-scoped-grant-revocation-on-logout.md)) for its own, different purpose.
- **Store the lineage id only in the payload JSON.** Rejected: the family revocation is a set-based `UPDATE … WHERE`,
  which needs an indexed, queryable column; payload JSON is neither portably queryable nor indexable.
- **Default the policy to `none`.** Rejected: it preserves the insecure status quo (a detected compromise leaves the
  family usable). Greenfield posture is secure-by-default, and `revoke_all` costs nothing until a replay actually
  happens. `none` remains available for deployments that deliberately want audit-only.
- **A lineage id plus `revoke_all` default (chosen).** Precisely implements RFC 9700 §4.14.2, matches Auth0 reuse
  detection, and revokes exactly the compromised chain.

## Consequences

- A replayed refresh token now invalidates its entire rotation family by default: the legitimate client's next refresh
  fails and it must re-authenticate — the intended outcome when a token in the chain is known to be compromised.
- The grants table gains a nullable `RefreshTokenLineageId` column and an index; the id is populated only for
  refresh-token grants (null for authorization codes, device codes, etc.). Rotating a refresh token threads the lineage
  id from the old grant into the new one through `CreateSecurityTokenRequest.RefreshTokenLineageId`.
- Deployments that want the prior audit-only behavior set `refresh_token_reuse_policy = none`.
- The lineage model is reusable: any future rotated grant type can populate the same column to get family revocation for
  free.

## References

- [RFC 9700](https://www.rfc-editor.org/rfc/rfc9700) — OAuth 2.0 Security Best Current Practice (§4.14.2, refresh-token
  rotation and reuse detection)
- [ADR-0028](0028-token-revocation-refresh-tokens-only.md) — token revocation revokes refresh tokens only
- [ADR-0022](0022-grant-filters-and-bulk-revocation.md) — the load-and-mutate bulk-revocation pattern this reuses
- [ADR-0077](0077-session-scoped-grant-revocation-on-logout.md) — session-scoped grant revocation on logout
- [ADR-0012](0012-interceptor-managed-concurrency-tokens.md) — the concurrency-token interceptor that stamps changed rows
- [ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md) — greenfield, secure-by-default, Auth0-parity-plus posture
