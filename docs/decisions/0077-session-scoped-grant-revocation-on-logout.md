# 77. Logout revokes the terminating session's grants, not the subject's

- **Status:** Accepted (refines [ADR-0074](0074-session-management-and-logout.md), which revoked the subject's grants)
- **Date:** 2026-10-12
- **Deciders:** NCode Group

## Context

[ADR-0074](0074-session-management-and-logout.md) built RP-Initiated and Back-Channel Logout on the existing grant
store and, at logout, revoked **the subject's grants** — `DefaultRevokeGrantsOnEndSessionHandler` called
`IGrantStore.RevokeWhereAsync(subjectId, clientId: null, …)`. That reused the one revocation primitive the store
offered, but it over-revokes: a subject with two concurrent logins (say a phone and a laptop) who signs out of **one**
has the refresh tokens of the **other** invalidated too. RP-Initiated Logout is single-logout — it terminates the
session identified by the `id_token_hint` / login cookie — so revoking every grant the subject holds is broader than
the request.

The grant store predates sessions: it was a "one-stop shop" keyed by `(subject, client)` built before a login was a
first-class `sid`-keyed session. Now that [ADR-0074](0074-session-management-and-logout.md) makes a login a session
with an opaque `sid`, a grant issued **under** a session can carry that `sid`, and logout can scope its revocation to
exactly the terminating session.

## Decision

**A grant records the `sid` of the session it was issued under, and logout revokes by that `sid` — the terminating
session's grants only (including the session grant itself) — rather than the whole subject. `sid` becomes a first-class
grant-filter dimension alongside subject and client; a subject-wide ("sign out everywhere") revocation remains
available as the deliberate opt-in it already was (the management bulk-revoke of [ADR-0022](0022-grant-filters-and-bulk-revocation.md)).**

- **`SessionId` is a first-class grant column.** `PersistedGrant` (and the typed `PersistedGrant<TPayload>` envelope)
  gains a nullable `SessionId`; `GrantEntity` gains a `SessionId` column and a `(TenantId, SessionId)` lookup index. The
  `sid` is an opaque, case-significant Base64Url key matched verbatim, so — unlike `SubjectId`/`GrantId` — it is indexed
  directly with **no** normalized companion column (the normalized-vs-verbatim rule is recorded in
  [ADR-0052](0052-entity-framework-persistence-is-a-framework-plus-slices.md)).
- **Session-scoped grants carry the `sid` at issuance.** The authorization-code grant, the refresh-token grant, and the
  per-access-token UserInfo-claims grant read the `sid` from the authenticated subject (`SubjectAuthentication.SessionId`,
  a convenience over the subject's `sid` claim) and stamp it on the grant. The **session grant itself** carries its own
  `sid`, so a single `RevokeWhereAsync(sessionId)` revokes the session and every grant minted under it in one pass.
  Non-interactive issuance (client-credentials, device-code, dynamic-registration, initial-access tokens, the continue
  grant) has no session and stores `null`.
- **`IGrantStore` gains a `sessionId` filter.** `RevokeWhereAsync` and `GetPageAsync` take an optional `sessionId`
  that combines with the existing `subjectId`/`clientId` filters (AND). The management grant list and bulk-revoke
  endpoints expose it as a query parameter, and the bulk-revoke's "at least one filter required" guard now accepts
  `sessionId` as a sufficient filter; `GrantResource` and the bulk-revocation audit event surface it.
- **Logout prefers the session scope.** `DefaultRevokeGrantsOnEndSessionHandler` revokes by `sessionId` when the
  logout resolved a session (the normal RP-initiated and first-party-cookie case). When no session is resolved — a
  non-interactive `id_token_hint` carrying only a `sub` — it falls back to the subject-wide revocation so logout still
  invalidates server-side state. This makes the former subject-wide behavior the fallback, not the default.
- **Global sign-out stays an explicit, separate operation.** "Sign user X out everywhere" is the management
  bulk-revoke ([ADR-0022](0022-grant-filters-and-bulk-revocation.md)) keyed by `subjectId`; it is a deliberate
  administrative action, not a side effect of a single session's logout. Durable consent is unaffected — it is its own
  aggregate, not a grant ([ADR-0076](0076-consent-persistence-and-enforcement.md)).

The redundant `ISessionService.RevokeAsync` (which revoked only the session grant, leaving its child tokens live) is
removed: session revocation is now the session-scoped grant revocation, which is both complete and the single path.

## Options considered

- **Keep revoking the subject's grants (ADR-0074 as-was).** Simplest — one filter, no schema change — but it conflates
  single-logout with global sign-out and silently kills a subject's other sessions. Rejected: it is incorrect
  single-logout semantics, and the fix is a cohesive data-model addition, not a workaround.
- **Derive the session from each grant payload instead of a column.** The authorization/refresh payloads already carry
  a `SubjectAuthentication` from which the `sid` is reachable, so revocation could deserialize every candidate grant and
  filter in memory. Rejected: it is not sargable (no index), varies by payload type, and does not cover the session
  grant uniformly. A first-class indexed column is the queryable, type-uniform home.
- **A dedicated "session → grants" association table.** A join table tracking which grants belong to a session is the
  textbook normalization, but it duplicates what a single indexed `sid` column on the grant expresses and adds a write
  per issuance plus a join per revoke, for a relationship that is strictly one session → many grants. Rejected: the
  column is the cohesive home, consistent with riding the grant store ([ADR-0074](0074-session-management-and-logout.md)).

## Consequences

- **Easier:** single-logout no longer disturbs a subject's other sessions; `sid` is a queryable grant dimension, so an
  operator can list or revoke exactly one session's grants; the session grant and its child grants revoke together in
  one indexed pass.
- **Harder / follow-ups:** every session-scoped issuance path must stamp the `sid` (a small, uniform addition funneled
  through the grant envelope). A new EF column + index means a host-owned migration
  ([ADR-0041](0041-host-owned-ef-migrations-deliberate-outside-development.md)). A UI affordance for a user-initiated
  "sign out everywhere" (vs. the admin bulk-revoke) is a later presentation concern, not a model gap.
- **You-chose-wrong smells:** a deployment that genuinely wants every logout to be a global sign-out (then the
  session-scoped default is friction, though the subject-wide revoke is one call away); a grant type that should be
  session-bound but is left storing `null` (then its tokens outlive the session).

## References

- [ADR-0074](0074-session-management-and-logout.md) — sessions, the `sid` claim, and the logout pipeline this refines.
- [ADR-0022](0022-grant-filters-and-bulk-revocation.md) — the subject/client grant filters and bulk revocation that the
  `sessionId` filter joins, and the management "sign out everywhere" path.
- [ADR-0052](0052-entity-framework-persistence-is-a-framework-plus-slices.md) — the normalized-companion vs.
  verbatim-indexed-key convention that governs the `SessionId` column.
- [ADR-0076](0076-consent-persistence-and-enforcement.md) — durable consent is its own aggregate, immune to grant
  revocation.
- [ADR-0012](0012-interceptor-managed-concurrency-tokens.md) — the interceptor that bumps row tokens on the grants a
  session-scoped revoke changes.
- Code: `NCode.Identity.OpenId.Persistence.Stores.IGrantStore` (`RevokeWhereAsync`, `GetPageAsync`),
  `NCode.Identity.OpenId.Persistence.DataContracts.PersistedGrant` (`SessionId`),
  `NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Handlers.DefaultRevokeGrantsOnEndSessionHandler`.
