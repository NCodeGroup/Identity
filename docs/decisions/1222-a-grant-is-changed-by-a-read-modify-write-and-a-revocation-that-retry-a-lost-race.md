# 1222. A grant is changed by a read-modify-write and a revocation that retry a lost race

- **Status:** Accepted
- **Date:** 2026-10-23
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0110](project-template/0110-shared-state-is-changed-by-read-modify-write.md)

## Context

[project-template ADR-0110](project-template/0110-shared-state-is-changed-by-read-modify-write.md) changes stored state
that concurrent requests change only through one read-modify-write operation, which applies a pure function of the
caller to the value as stored and retries a lost race. This record applies it to grant payloads and to revocations.

A grant row carries a concurrency token that the unit of work checks when it saves, and a write that changes a grant
after another request read it fails as a `StoreConcurrencyException`
([ADR-1012](1012-concurrency-tokens-are-interceptor-managed-and-a-lost-race-surfaces-as-a-store-neutral-exception.md)).
Two kinds of writer would lose to such a race if each computed its change from an earlier read of the grant:

- A writer of a payload that computes the payload from its own read and then replaces the stored payload overwrites a
  write that landed in between. The touch of a session would remove a client that a concurrent authorize request added
  to the session, two concurrent additions of a client would lose one, and a client missing from the session would
  receive no back-channel logout when the session is terminated. A poll of a device-code or CIBA grant would replace the
  payload with a copy that records the poll and lose an approval that landed between its read and its write. Two
  approvals of a device authorization or of a CIBA request would both succeed, and both CIBA approvals would deliver a
  ping or push tokens.
- A revocation reads grants and stamps their revocation time, so a write that committed in between, such as a
  sliding-expiration update, a rotation or another revocation, would make the save of the revocation fail. The
  revocation endpoint would answer `500` where RFC 7009 §2.2 answers `200` and leave the token active, a logout that
  terminated a session would leave the grants of that session active, and the management API would fail a revocation
  that is correct whatever the caller read.

## Decision

**A grant's payload is changed only by `IPersistedGrantService.UpdatePayloadAsync`, which applies a function of the
caller to the grant as stored, and every revocation goes through `IPersistedGrantService`; both read again and retry
when their save loses a race.**

1. **One read-modify-write for a payload.** `UpdatePayloadAsync<TPayload>(openIdContext, grantId, update,
   cancellationToken)` is the only payload writer. It reads the grant in a unit of work and returns `false`, without
   calling `update`, when the grant is unknown, belongs to another tenant or is not `Active`. Otherwise it calls
   `update` with the grant as stored, whose payload is deserialized through `IPersistedPayloadSerializer`. When
   `update` returns `null` the call writes nothing and returns `false`. When it returns a value, the call serializes
   the payload, applies the expiration when the value carries one, saves under the concurrency token of that read and
   returns `true`.
2. **`PersistedGrantUpdate<TPayload>` carries the change.** It is a `public readonly record struct` with the required
   `Payload` to persist and `ExpiresWhen`, which is `null` to keep the expiration of the stored grant.
3. **A lost race reads again.** A `StoreConcurrencyException` opens a unit of work, reads the grant again and calls
   `update` again, up to `MaxSaveAttempts` (3) attempts in total; the last loss propagates. `update` may therefore run
   up to three times, so it decides from its argument only and has no side effect.
4. **Every writer checks its precondition on the stored grant.**
   - `ISessionService.AddClientAsync` adds a client only when the stored session does not list it, and returns whether
     this call added it.
   - The session touch ([ADR-1220](1220-an-end-user-session-is-a-grant-keyed-by-its-sid-bounded-by-idle-and-absolute-lifetimes.md))
     is applied to the session as stored: it writes nothing when the stored session was touched within the one-minute
     granularity or has ended, and otherwise sets `LastActivityWhen` to the time of the request and the expiry of the
     grant to the end of the session, so a client that a concurrent request added stays.
   - The device verification endpoint and the CIBA approval and denial change only a `Pending` request, and report
     whether this call decided it, so only the approval that won delivers a ping or pushes tokens.
   - The polls of a device-code grant and of a CIBA grant record `LastPolledWhen` only on a `Pending` request, so an
     approval that lands between the read of the poll and its write stands.
5. **Every revocation retries.** A revocation does not depend on what its caller read, because its outcome is the same
   whichever state the grants were in, so a retry is correct. When the save of a revocation loses a race, the service
   opens a new unit of work, reads again and retries, up to 3 attempts in total. The last loss propagates as
   `StoreConcurrencyException`, which a protocol endpoint answers `500` and the management API answers as
   [ADR-1483](1483-every-management-error-is-a-problem-document-that-its-route-declares.md) decides.
6. **One filtered revocation.** `RevokeWhereAsync(GrantFilter)` revokes every active grant the filter selects, returns
   the count it revoked and throws `ArgumentException` for an empty filter, so an unscoped bulk revocation cannot happen
   by accident. `GrantFilter.GrantId`, the opaque public identifier that the store matches on `NormalizedGrantId`,
   counts toward `GrantFilter.IsEmpty`. Its callers are the replay of an authorization code and the termination of a
   session ([ADR-1112](1112-every-end-of-a-session-runs-one-termination-seam-once-revoking-the-grants-issued-under-it.md)),
   by `SessionId`; a subject-wide termination, by `SubjectId`; and the management bulk revocation and the management
   revocation of a single grant. The latter reads the grant for the `404` and the audit details, revokes through a
   filter on `GrantId` and audits only when the count is above zero, so a grant that another request revoked in between
   is not audited twice.
7. **A revocation of one grant reports whether it revoked.** `SetRevokedOnceAsync` returns whether this call revoked
   the grant: `false` when it is unknown, belongs to another tenant or is already revoked, also by a concurrent
   request. The claim of an ended session revokes the grants issued under it in the same unit of work
   ([ADR-1223](1223-a-session-that-ends-unused-is-terminated-once-in-the-background.md)).
8. **A spend answers a lost race as a second spend or a reuse.** `ConsumeOnceOrDefault`, `TryRevokeOnceAsync` and
   `ReplaceOnceOrDefault` spend a grant that must be active, so their outcome depends on what they read
   ([ADR-1216](1216-a-refresh-token-rotates-unless-it-is-bound-and-rotation-spends-it-with-its-successor-atomically.md),
   [ADR-1217](1217-a-reused-refresh-token-revokes-its-family-unless-it-is-a-retry-within-an-opt-in-interval.md)).

## Options considered

- **Value-replacing payload writes.** Rejected: an overwrite loses a concurrent addition, touch or approval, and lets
  two approvals both deliver.
- **Answer a revocation without retrying.** Rejected: the endpoint would answer `200`, and the logout would report
  success, while the grants stay active, which is worse than a failure that the caller sees.
- **A retry loop in each caller.** Rejected: the revocation endpoint, the logout, the replay of a code, the device
  user code and the management revocations would repeat the loop, and a caller that forgets it fails on a lost race.
- **A set-based update.** Rejected for the reasons of
  [ADR-1012](1012-concurrency-tokens-are-interceptor-managed-and-a-lost-race-surfaces-as-a-store-neutral-exception.md): the
  Entity Framework InMemory provider does not support it, and it bypasses the interceptor-managed concurrency token.
- **Unbounded retries.** Rejected: a livelock never ends. Each attempt reads again and sees every write that committed
  before it, so a write loses again only to one that lands between its read and its save, and three attempts suffice.
- **A read-modify-write for payloads and a retrying revocation through the grant service.** Chosen: one place decides
  each change from the grant as stored.

## Consequences

- A function runs up to three times, so it decides from its argument only. A change that loses every attempt reaches
  its caller as `StoreConcurrencyException`, except the touch of a session, which is dropped because each write that
  won is itself activity and the session is still valid.
- No revocation fails for a lost race in practice. One fails only when three attempts in a row each lose to another
  write. A revocation that races a rotation, a sliding-expiration update or another revocation revokes the grants that
  it finds when it reads again, including a successor that the rotation added.
- `ISessionService.AddClientAsync` and the CIBA approval and denial report whether this call made the change.
- The Entity Framework InMemory provider checks no concurrency token, so the hermetic host cannot show the races; the
  unit tests cover them.

## References

- [ADR-1012](1012-concurrency-tokens-are-interceptor-managed-and-a-lost-race-surfaces-as-a-store-neutral-exception.md) —
  the interceptor-managed concurrency tokens that detect the lost race, and the `StoreConcurrencyException`.
- [ADR-1112](1112-every-end-of-a-session-runs-one-termination-seam-once-revoking-the-grants-issued-under-it.md) — the
  termination of a session, which revokes its grants.
- [ADR-1216](1216-a-refresh-token-rotates-unless-it-is-bound-and-rotation-spends-it-with-its-successor-atomically.md) —
  the lineage revocation that reads again, and the spends that answer a lost race as a reuse.
- [ADR-1217](1217-a-reused-refresh-token-revokes-its-family-unless-it-is-a-retry-within-an-opt-in-interval.md) — the
  refresh-token spend.
- [ADR-1220](1220-an-end-user-session-is-a-grant-keyed-by-its-sid-bounded-by-idle-and-absolute-lifetimes.md) — the
  session bounds that the touch advances.
- [ADR-1223](1223-a-session-that-ends-unused-is-terminated-once-in-the-background.md) — the claim of an ended session.
- [ADR-1483](1483-every-management-error-is-a-problem-document-that-its-route-declares.md) — the management API's answer
  to a lost race.
- [RFC 7009](https://www.rfc-editor.org/rfc/rfc7009) — OAuth 2.0 Token Revocation (§2.2).
- Issues [#269](https://github.com/NCodeGroup/identity/issues/269) and
  [#282](https://github.com/NCodeGroup/identity/issues/282).
- Code: `NCode.Identity.OpenId.Authentication.Persistence.IPersistedGrantService`,
  `NCode.Identity.OpenId.Authentication.Persistence.DefaultPersistedGrantService`,
  `NCode.Identity.OpenId.Persistence.Stores.GrantFilter`,
  `NCode.Identity.OpenId.Authentication.Sessions.DefaultSessionService`,
  `NCode.Identity.OpenId.Authentication.Sessions.DefaultRevokeGrantsOnSessionTerminatedHandler`,
  `NCode.Identity.OpenId.Authentication.Endpoints.DeviceAuthorization.DefaultDeviceVerificationEndpointHandler`,
  `NCode.Identity.OpenId.Authentication.Endpoints.Token.DeviceCode.DefaultDeviceCodeGrantHandler`,
  `NCode.Identity.OpenId.Authentication.Endpoints.Token.Ciba.DefaultCibaGrantHandler`,
  `NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication.DefaultCibaRequestService`,
  `NCode.Identity.OpenId.Management.Endpoints.Grants.GrantApiEndpointHandler`.
