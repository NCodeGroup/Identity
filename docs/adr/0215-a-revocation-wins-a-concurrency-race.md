# 215. A revocation wins a concurrency race

- **Status:** Accepted
- **Date:** 2026-10-23
- **Deciders:** NCode Group

## Context

A grant row carries a concurrency token that the unit of work checks when it saves
([ADR-0012](0012-interceptor-managed-concurrency-tokens.md)), and a write that changes a grant after another request
read it fails as a `StoreConcurrencyException`
([ADR-0208](0208-a-lost-concurrency-race-surfaces-as-a-store-neutral-exception.md)). The revocation paths read grants
and stamp their revocation time, so a write that committed in between, such as a sliding-expiration update, a rotation,
or another revocation, made the save of the revocation fail:

- the revocation endpoint answered `500`, where RFC 7009 §2.2 answers `200`, and the token stayed active;
- a logout that terminated a session left the grants of that session active;
- the management API answered `412`
  ([ADR-0214](0214-a-management-write-that-loses-a-race-is-answered-412.md)) to a revocation that is correct whatever
  the caller read.

The revocation of a refresh-token lineage read again and retried
([ADR-0211](0211-a-rotated-refresh-token-is-spent-with-its-successor.md) decision 5), and the other revocations did not
(backlog `R78`).

## Decision

**Every revocation goes through `IPersistedGrantService`, which reads the grants again and retries when its save loses
a race.**

1. **A revocation retries.** When the save of a revocation throws `StoreConcurrencyException`, the service opens a new
   unit of work, reads again, and retries, up to `MaxSaveAttempts` (3) attempts in total. After the last lost attempt
   the exception propagates: `500` at a protocol endpoint, and `412` at the management API through the filter of
   [ADR-0214](0214-a-management-write-that-loses-a-race-is-answered-412.md). A revocation does not depend on what its
   caller read, because its outcome is the same whichever state the grants were in, so a retry is correct, as it is for
   the lineage revocation.
2. **One filtered revocation.** `IPersistedGrantService.RevokeWhereAsync` revokes every active grant that a
   `GrantFilter` selects, returns the count of grants it revoked, and throws `ArgumentException` for an empty filter, so
   an unscoped bulk revocation cannot happen by accident. It replaces `RevokeBySessionAsync`, which is removed. Its
   callers are:
   - the replay of an authorization code, by `SessionId`
     ([ADR-0112](0112-authorization-code-replay-revokes-session.md));
   - `DefaultRevokeGrantsOnSessionTerminatedHandler`, by `SessionId` or, for a subject-wide termination, by
     `SubjectId`;
   - the management bulk revocation, by its subject, client, and session filter;
   - the management revocation of a single grant.
3. **A revocation of one grant retries the same way.** `SetRevokedOnceAsync` keeps its name and its signature. The
   revocation endpoint calls it for a refresh token that names no lineage, and the device verification endpoint calls
   it for the user code.
4. **A filter selects one grant by its public identifier.** `GrantFilter.GrantId`, the opaque public identifier that
   the store matches on `NormalizedGrantId`, is part of `GrantFilter.IsEmpty`.
5. **The management revocation of a single grant audits what it revoked.** The endpoint reads the grant, for the `404`
   and for the details of the audit event, and revokes it through `RevokeWhereAsync` with a filter on `GrantId`. It
   records the audit event only when the returned count is above zero, so a grant that another request revoked in
   between is not audited twice.
6. **A spend answers a lost race as a second spend or a reuse.** `ConsumeOnceOrDefault`, `TryRevokeOnceAsync`, and
   `ReplaceOnceOrDefault` spend a grant that must be active, so their outcome depends on what they read. A spend that
   loses to a spend or to a revocation is answered as a second spend or a reuse
   ([ADR-0208](0208-a-lost-concurrency-race-surfaces-as-a-store-neutral-exception.md),
   [ADR-0211](0211-a-rotated-refresh-token-is-spent-with-its-successor.md)). A refresh-token spend reads again only
   after a write that left the grant active
   ([ADR-0217](0217-a-rotated-refresh-token-may-be-retried-within-a-reuse-interval.md)).

## Options considered

- **Answer without retrying.** Rejected: the endpoint would answer `200`, and the logout would report success, while the
  grants stay active, which is worse than a failure that the caller sees.
- **A retry in each caller.** Rejected: the revocation endpoint, the logout, the replay of a code, the device user code,
  and the two management revocations would repeat the loop, and a caller that forgets it answers `500` or `412`.
- **A set-based update.** Rejected for the reasons of
  [ADR-0208](0208-a-lost-concurrency-race-surfaces-as-a-store-neutral-exception.md): the Entity Framework InMemory
  provider does not support it, and it bypasses the interceptor-managed concurrency token.
- **Unbounded retries.** Rejected: a livelock never ends. Each attempt reads again and therefore sees every write that
  committed before it, so a revocation loses again only to a write that lands between its read and its save, and three
  attempts suffice.

## Consequences

- No revocation fails for a lost race in practice. One fails only when three attempts in a row each lose to another
  write, and it then fails: `500` at a protocol endpoint and `412` at the management API.
- A revocation that races a rotation, a sliding-expiration update, or another revocation revokes the grants that it
  finds when it reads again, including a successor that the rotation added.
- The Entity Framework InMemory provider checks no concurrency token, so the hermetic host cannot show the race; the
  unit tests cover it.
- **Breaking:** `IPersistedGrantService.RevokeBySessionAsync` is removed, `IPersistedGrantService.RevokeWhereAsync` is
  added, `GrantFilter.GrantId` is added, and `DefaultRevokeGrantsOnSessionTerminatedHandler` depends on
  `IPersistedGrantService`, so every implementation of the service implements `RevokeWhereAsync`.
- This implements backlog `R78`.

## References

- [ADR-0012](0012-interceptor-managed-concurrency-tokens.md) — the interceptor-managed concurrency tokens that detect
  the lost race.
- [ADR-0208](0208-a-lost-concurrency-race-surfaces-as-a-store-neutral-exception.md) — a lost race is a
  `StoreConcurrencyException`.
- [ADR-0211](0211-a-rotated-refresh-token-is-spent-with-its-successor.md) — the lineage revocation that reads again, and
  the spends that answer a lost race as a reuse.
- [ADR-0214](0214-a-management-write-that-loses-a-race-is-answered-412.md) — a management write that loses a race is
  answered `412`.
- [RFC 7009](https://www.rfc-editor.org/rfc/rfc7009) — OAuth 2.0 Token Revocation (§2.2).
- Code: `NCode.Identity.OpenId.Authentication.Persistence.IPersistedGrantService`,
  `NCode.Identity.OpenId.Authentication.Persistence.DefaultPersistedGrantService`,
  `NCode.Identity.OpenId.Persistence.Stores.GrantFilter`,
  `NCode.Identity.OpenId.Authentication.Sessions.DefaultRevokeGrantsOnSessionTerminatedHandler`,
  `NCode.Identity.OpenId.Management.Endpoints.Grants.GrantApiEndpointHandler`.
