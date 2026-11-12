# 1112. Every end of a session runs one termination seam, once, revoking the grants issued under it

- **Status:** Accepted
- **Date:** 2026-10-23
- **Deciders:** NCode Group

## Context

A session ([ADR-1074](1074-session-management-and-logout.md)) ends for many causes: a logout, an idle timeout, an
absolute expiry, the concurrent-session limit, an administrator, a subject-wide sign-out, the deletion or disabling of
an account, a password change or reset, and the replay of an authorization code. Each cause has three obligations: the
grants issued under the session must stop working, the relying parties that took part in it must hear that it ended,
and the end must be audited. Four forces shape how a session ends.

- **Scope.** Revoking every grant of the subject at a logout over-revokes: a subject signed in on a phone and a laptop
  who signs out of one would lose the refresh tokens of the other. RP-Initiated Logout ends the session its
  `id_token_hint` or cookie names, so the revocation is scoped to that session.
- **One seam.** A termination that revokes grants directly, outside a shared seam, skips back-channel logout and the
  audit record, and consumers have no place to react to a termination they did not start. "Many contributors react to a
  state change" is the mediator shape of [ADR-1001](1001-behavior-is-a-service-a-strategy-collection-or-a-mediator-seam-chosen-by-one-ladder.md).
- **Once.** A session that two paths end at once notifies its relying parties twice and is audited twice, and a
  subject-wide termination that revokes grants only inside its dispatch leaves them active when the dispatch fails. A
  client added to the session between a read and the claim receives no back-channel logout although its tokens are
  revoked.
- **Replay.** [RFC 6749 §4.1.2](https://www.rfc-editor.org/rfc/rfc6749#section-4.1.2) requires the server to deny a code
  that is presented twice and to revoke, when possible, the tokens issued from it, and RFC 9700 reads a second
  redemption as a sign that the code was intercepted. Access tokens are self-contained signed JWTs with no row of their
  own, so the handle that a replay offers is the end-user session, already persisted as a `session` grant.

## Decision

**Every end of a session goes through one termination seam, `OnSessionTerminatedCommand`, which a termination reaches
only once it has claimed the session, and which revokes every grant issued under the session's `sid`.**

### The seam

- **`OnSessionTerminatedCommand`** is a fan-out of handlers registered with `TryAddEnumerable` and ordered by priority.
  It carries a `SessionTermination`: the reason, the subject id, the session id (`null` for a subject-wide
  termination) and the participating clients.
- **The default handlers** are `DefaultRevokeGrantsOnSessionTerminatedHandler` (high priority; revokes through
  `IPersistedGrantService.RevokeWhereAsync` by `SessionId`, or by `SubjectId` for a subject-wide termination),
  `DefaultBackChannelLogoutOnSessionTerminatedHandler` (low priority) and `DefaultAuditSessionTerminatedHandler`, which
  records `session.terminated`. A consumer extends the seam by registering another
  `ICommandHandler<OnSessionTerminatedCommand>`.
- **The reason is an open vocabulary** ([ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md)), compared
  ordinally. `SessionTerminationReasons` names `logout`, `idle_timeout`, `absolute_expiry`, `account_deleted`,
  `account_disabled`, `concurrent_session_limit`, `administrative_revocation`, `global_sign_out`, `user_initiated`,
  `password_reset`, `user_blocked`, `authorization_code_replay` and `password_changed`. An extension that ends a
  session for its own cause passes its own name, and the handlers and the audit event carry it unchanged.
- **Creation is inline.** One owner, interactive sign-in, establishes a session, so it is no fan-out concern.
  `DefaultSessionService.CreateAsync` records `session.created` (`IAuditEventRecorder.RecordSessionCreatedAsync`) and
  logs, with no seam. No termination touches consent, which is its own aggregate
  ([ADR-1081](1081-consent-is-a-durable-record-of-decided-and-granted-scopes-enforced-at-the-authorization-endpoint.md)).
- **Eviction is a policy.** At `max_concurrent_sessions`, `ISessionEvictionPolicy` (default
  `DefaultOldestFirstSessionEvictionPolicy`, which keeps the newest sessions) selects the sessions to evict. Each is
  claimed at the creation time of the session that causes the eviction, and `SessionEvicted` is logged only for a
  session the call claimed.

### Grants carry the session

- **`PersistedGrant.SessionId`** (and the typed `PersistedGrant<TPayload>` envelope) holds the `sid` of the session a
  grant was issued under. `GrantEntity` has a `SessionId` column with a `(TenantId, SessionId)` index. The `sid` is an
  opaque, case-significant Base64Url key matched verbatim, so it is indexed directly with no normalized companion column
  ([ADR-1052](1052-entity-framework-persistence-is-a-reusable-framework-that-domain-slices-plug-into.md)).
- **Issuance stamps it.** The authorization code, the refresh token, the `userinfo_claims` grant and the session grant
  itself read the `sid` from `SubjectAuthentication.SessionId`, so one revocation by `sid` revokes the session and every
  grant under it. Non-interactive issuance (client credentials, device code, dynamic registration, initial access
  tokens, the continue grant) has no session and stores `null`.
- **The filter.** `IGrantStore.RevokeWhereAsync` and `GetPageAsync` take an optional `sessionId` that combines with
  `subjectId` and `clientId` by AND. The management grant list and bulk revocation expose it, accept it as a sufficient
  filter, and `GrantResource` and the bulk-revocation audit event surface it
  ([ADR-1020](1020-a-tenants-grants-are-listed-inspected-and-revoked-by-opaque-identifier-never-by-their-key.md)).

### Claim, then dispatch

1. **Every termination of a named session claims it first.** `IPersistedGrantService.RevokeSessionOnceAsync<TPayload>`
   revokes the session grant and every grant under its `sid` in one unit of work and returns the session grant as it
   revoked it (`PersistedGrant<TPayload>?`, with `Status` `Revoked` and the payload as stored), or `null` when the grant
   is unknown, belongs to another tenant or is revoked already. The payload is read from the envelope that the
   committing attempt read, after the save, so a payload that cannot be read never keeps a session active.
   `DefaultSessionService` dispatches the termination only when its call claimed the session, through one private
   helper, `TerminateOnceAsync`, which claims, dispatches and reports whether it claimed. The paths are
   `ISessionService.RevokeAsync`, `RevokeAllExceptAsync` ([ADR-1230](1230-an-end-user-changes-their-password-and-keeps-only-the-current-session.md)
   states what it revokes), the eviction, the RP-initiated logout (through `RevokeAsync`) and the termination of an
   ended session ([ADR-1220](1220-an-end-user-session-always-ends-and-its-grant-expires-when-it-does.md),
   [ADR-1223](1223-a-session-that-ends-unused-is-terminated-once-in-the-background.md)).
2. **The termination names what the claim returned.** The `SessionTermination` of a named session takes its subject and
   the `ClientIds` of the claimed grant's payload. The list is complete: a concurrent `AddClientAsync` changes the
   session grant, which makes the claim retry ([ADR-1215](1215-a-revocation-wins-a-concurrency-race.md)), and the claim
   is the only read of the session that no such write can follow. `RevokeAsync` claims without reading the session
   first.
3. **The service reports what it ended.** `ISessionService.RevokeAsync` returns `ValueTask<bool>`: `true` when the call
   terminated the session, `false` when it does not exist or is revoked already, also by a concurrent caller.
   `RevokeAllAsync` and `RevokeAllExceptAsync` return `ValueTask<int>`, the number of sessions the call terminated. The
   sessions management API reports and audits those results.
4. **A subject-wide termination claims every unrevoked session, then revokes every grant.** `RevokeAllAsync` reads a
   page of the subject's session grants, skips the revoked ones without reading a payload, and claims each other, an
   ended session awaiting the background pass included, which is terminated with the subject-wide reason and its
   relying parties notified. It then revokes every grant of the subject with `RevokeWhereAsync` and a `GrantFilter`
   naming the subject, so a failed dispatch leaves no grant active. It always dispatches one subject-wide termination,
   with a `null` session id and the union of the claimed sessions' clients, also when it claimed none, so every handler
   observes it and a concurrent second call notifies no relying party.
5. **Logout.** `DefaultEndSessionHandler` terminates the session a logout resolves with `ISessionService.RevokeAsync`
   and `SessionTerminationReasons.Logout`, which revokes that session's grants only. A logout that names no session, a
   non-interactive `id_token_hint` carrying only `sub`, dispatches a subject-wide termination, which has no relying
   parties to notify. Signing a subject out everywhere is otherwise an explicit operation: `RevokeAllAsync`, the
   sessions management API ([ADR-1036](1036-one-factory-materializes-every-openid-context-for-a-request-an-addressed-tenant-or-a-job.md)),
   or the management bulk revocation by `subjectId`.

### A replayed authorization code ends its session

A replay is a code whose grant was redeemed (`ConsumedWhen` is set). `DefaultAuthorizationCodeGrantHandler` terminates
the code's session (`PersistedGrant.SessionId`) through `ISessionService.RevokeAsync` with `authorization_code_replay`
(`SessionTerminationReasons.AuthorizationCodeReplay`). The claim revokes the grants issued under the session, the
relying parties receive back-channel logout and the termination is audited. A code that expired unused, was revoked or
names no session terminates nothing: it is no compromise signal, and a termination would sign the end user out of
every relying party. The OP's own endpoints (UserInfo, introspection, the management API) reject an access token whose
`sid` names a revoked session ([ADR-1167](1167-the-server-validates-the-tokens-it-issued-under-one-policy.md)).
External resource servers accept an unexpired access token until it expires, so short access-token lifetimes bound that
exposure. Refresh tokens issued under the session are revoked with it, so the refresh-token grant handler's active
check rejects them.

## Options considered

- **Revoke the subject's grants on every logout.** One filter and no schema change, but it conflates single logout with
  global sign-out and silently ends the subject's other sessions. Rejected.
- **Derive the session from each grant's payload.** Revocation would deserialize every candidate grant and filter in
  memory. Rejected: it is not sargable, varies by payload type and does not cover the session grant uniformly.
- **A session-to-grants join table.** It duplicates what one indexed `sid` column expresses and adds a write per
  issuance and a join per revocation for a strictly one-to-many relationship. Rejected.
- **A per-token denylist or reference tokens.** Rejected: it reintroduces per-access-token state and a lookup on every
  resource-server call, the cost a stateless JWT exists to avoid.
- **Dispatch one termination per session in `RevokeAllAsync`.** Rejected: a subject-wide termination sends each relying
  party one logout token with `sub`, which covers every session it took part in, and a termination per session sends
  one per session.
- **Let the revocation handler claim the session.** Rejected: it runs inside the dispatch, after the decision to notify
  the relying parties.
- **Skip the subject-wide dispatch when nothing was claimed.** Rejected: the handlers that extend the seam would miss
  the termination and its audit record.
- **Treat every refused code as a replay.** Rejected: a code that expired unused is no compromise signal.
- **The chosen model:** one seam that every path reaches by claiming the session, with the claim returning the grant.

## Consequences

- Every cause of an end revokes the session's grants, notifies its relying parties and audits it, and each relying
  party is notified once and each session audited once, however many paths end it at the same time. A client added by
  an ID token issued under the session after an earlier read is notified.
- A subject-wide termination revokes the subject's grants before its dispatch and is audited also when it ended no
  session. Single logout leaves the subject's other sessions alone.
- A consumer reacts to every termination by registering a handler, and swaps the eviction strategy by registering an
  `ISessionEvictionPolicy`.
- Every session-scoped issuance path must stamp the `sid` through the grant envelope. The column and index are part of
  the host's schema migration ([ADR-1073](1073-one-postgresql-database-holds-the-hosts-schema-and-key-ring-migrated-by-a-worker-and-drift-checked.md)).
- Smells: a grant type that should be bound to a session but stores `null` outlives it, and a deployment that wants
  every logout to be a global sign-out finds the session scope friction, although `RevokeAllAsync` is one call away.
- The replay path is audited and notifies relying parties, and the conformance test `oidcc-codereuse-30seconds`, which
  re-presents the access token after a replay, passes.

## References

- [RFC 6749 §4.1.2](https://www.rfc-editor.org/rfc/rfc6749#section-4.1.2) and
  [RFC 9700](https://www.rfc-editor.org/rfc/rfc9700) — a replayed code is denied and its tokens revoked.
- [RFC 9068 §2.2.1](https://www.rfc-editor.org/rfc/rfc9068#section-2.2.1) — the `sid` of an access token, which
  [ADR-1220](1220-an-end-user-session-always-ends-and-its-grant-expires-when-it-does.md) states.
- [OpenID Connect Back-Channel Logout 1.0](https://openid.net/specs/openid-connect-backchannel-1_0.html) — the logout
  notification a termination sends.
- [ADR-1001](1001-behavior-is-a-service-a-strategy-collection-or-a-mediator-seam-chosen-by-one-ladder.md),
  [ADR-1074](1074-session-management-and-logout.md),
  [ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md),
  [ADR-1215](1215-a-revocation-wins-a-concurrency-race.md),
  [ADR-1223](1223-a-session-that-ends-unused-is-terminated-once-in-the-background.md) and
  [ADR-1230](1230-an-end-user-changes-their-password-and-keeps-only-the-current-session.md) — the decisions this one
  builds on.
- Issues [#63](https://github.com/NCodeGroup/identity/issues/63),
  [#286](https://github.com/NCodeGroup/identity/issues/286),
  [#287](https://github.com/NCodeGroup/identity/issues/287) and
  [#288](https://github.com/NCodeGroup/identity/issues/288).
- Code: `NCode.Identity.OpenId.Authentication.Sessions.DefaultSessionService`,
  `NCode.Identity.OpenId.Authentication.Persistence.DefaultPersistedGrantService`,
  `NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Handlers.DefaultEndSessionHandler`,
  `NCode.Identity.OpenId.Authentication.Endpoints.Token.AuthorizationCode.DefaultAuthorizationCodeGrantHandler`,
  `NCode.Identity.OpenId.Management.Endpoints.Sessions.SessionApiEndpointHandler`.
