# 112. Authorization-code replay revokes the end-user session; first-party resource endpoints enforce it

- **Status:** Accepted (introspection's enforcement realized by
  [ADR-0167](0167-the-server-validates-the-tokens-it-issued-under-one-policy.md): every reader of an issued access
  token checks its session through one seam; amended by
  [ADR-0228](0228-a-session-is-terminated-with-the-clients-it-lists-when-claimed-and-a-replayed-code-terminates-it.md):
  a replayed code terminates its session through the termination seam, which notifies its relying parties and audits
  it)
- **Date:** 2026-10-16
- **Deciders:** NCode Group

## Context

<see href="https://datatracker.ietf.org/doc/html/rfc6749#section-4.1.2">RFC 6749 §4.1.2</see> requires that, when an
authorization code is presented more than once, the authorization server MUST deny the request and SHOULD revoke (_"when
possible"_) all tokens previously issued based on that code. The OAuth Security BCP
(<see href="https://datatracker.ietf.org/doc/html/rfc9700">RFC 9700</see>) reaffirms this: a second redemption of a
one-time code is a replay signal and a strong indicator that the code was intercepted. The OpenID Foundation conformance
suite encodes the revocation step (`oidcc-codereuse-30seconds`): after a code replay it re-presents the
previously-issued access token at the resource endpoint and expects a `4xx`, warning when the token is still accepted.

Access tokens are self-contained, signed JWTs that carry no server-side state, so there is no per-token row to revoke.
The one server-side handle available on a replay is the **end-user session**: the authorization-code grant records the
`sid`, and a session is already persisted as a `session` grant whose status (active / revoked / expired) backs logout
and back-channel notification. That session is therefore the natural, already-indexed unit of revocation.

## Decision

**A second redemption of an already-consumed authorization code is treated as a compromise signal: the OP revokes the
end-user session's grants, and first-party resource endpoints reject any access token whose session has been revoked.**
Concretely:

- **Replay → session-scoped revocation.** When the token endpoint consumes an authorization code and finds it was
  already consumed, it revokes every grant for that session (the `session` grant itself plus the access- and
  refresh-token grants issued under it) via the same session-scoped grant revocation the logout seam uses
  (`IGrantStore.RevokeWhereAsync` by `sid`) — but **without** the back-channel-logout and logout-audit side effects,
  because this is a security revocation, not a user-initiated logout.
- **`sid` is always emitted.** The end-user session identifier (`sid`,
  <see href="https://datatracker.ietf.org/doc/html/rfc9068#section-2.2.1">RFC 9068 §2.2.1</see>) is always placed in a
  subject-bound access token, so the session reference is always available. The former opt-in
  `send_session_id_in_access_token` client setting is retired — the session binding is a non-bypassable security
  property, not a per-client toggle.
- **First-party endpoints enforce it.** Every endpoint that authenticates a self-issued access token (UserInfo,
  introspection, the management API) resolves the token's `sid` and rejects it (`invalid_token` / `401`) when the
  session grant is revoked. External resource servers continue to treat the JWT as self-contained; only the OP's own
  endpoints perform the session-status lookup.
- **Refresh tokens fall out for free.** The session-scoped revocation also revokes the refresh-token grants issued
  under the session, so the refresh-token grant handler's existing active-status check rejects them on the next use.

## Options considered

- **Keep stateless access tokens with no revocation check ([ADR-0028](0028-token-revocation-refresh-tokens-only.md)).**
  Rejected now: it leaves the RFC 6749 §4.1.2 SHOULD unmet and the conformance warning standing.
- **A global access-token denylist / reference tokens.** Rejected: it reintroduces per-access-token server state and a
  lookup on **every** resource-server call — the cost a stateless JWT exists to avoid.
- **Surgical per-exchange revocation** (record the exact access/refresh token issued from the code and revoke only
  those). Rejected: it requires persisting issued-token identifiers on the authorization-code grant for marginal benefit
  over session-scoped revocation, which already covers the tokens from that exchange.
- **Session-scoped revocation enforced at first-party endpoints (chosen).** Reuses the existing session grant and its
  revocation primitive, adds no per-token store, and precisely satisfies the SHOULD for the OP's own endpoints.

## Consequences

- `oidcc-codereuse-30seconds` passes; its `expected-failures.json` baseline is removed.
- The only new read is a single indexed session lookup when a first-party endpoint authenticates a subject-bound access
  token; the only new write is one session-scoped revocation on the rare, anomalous code-replay path. There is no global
  denylist and no per-access-token state.
- A code replay signs the end-user's session out **at the OP's own endpoints** and invalidates its refresh tokens.
  External resource servers still accept an unexpired access token until it expires; short access-token lifetimes bound
  that residual exposure.
- Revocation is session-scoped — marginally broader than the RFC's "tokens issued from that code" — justified by the
  compromise interpretation. Triggering it requires possession of the one-time authorization code (itself a compromise)
  or a client that violates the one-time-use contract; with PKCE `S256` mandatory, an interceptor cannot redeem the code
  in the first place, so the replay path is a fail-safe, not the primary defense.
- `sid` is disclosed to all resource servers. It is an opaque, random per-session identifier (not PII) that RFC 9068
  §2.2.1 defines precisely for session correlation and revocation.

## References

- [RFC 6749 §4.1.2](https://datatracker.ietf.org/doc/html/rfc6749#section-4.1.2),
  [RFC 9700](https://datatracker.ietf.org/doc/html/rfc9700),
  [RFC 9068 §2.2.1](https://datatracker.ietf.org/doc/html/rfc9068#section-2.2.1).
- [ADR-0028](0028-token-revocation-refresh-tokens-only.md) — revocation-endpoint scope, refined here for the
  code-replay path.
- [ADR-0077](0077-session-scoped-grant-revocation-on-logout.md) — session-scoped grant revocation.
- [ADR-0087](0087-refresh-token-reuse-detection-and-family-revocation.md) — the analogous reuse-detection response for
  refresh tokens.
