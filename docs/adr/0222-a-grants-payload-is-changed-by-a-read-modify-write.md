# 222. A grant's payload is changed by a read-modify-write that loses no concurrent write

- **Status:** Accepted
- **Date:** 2026-10-23
- **Deciders:** NCode Group

## Context

A grant row carries a concurrency token that the unit of work checks when it saves
([ADR-0012](0012-interceptor-managed-concurrency-tokens.md)), and a write that changes a grant after another request
read it fails as a `StoreConcurrencyException`
([ADR-0208](0208-a-lost-concurrency-race-surfaces-as-a-store-neutral-exception.md)). The writers of a grant's payload
computed the payload from an earlier read of the grant, and `IPersistedGrantService` then read the grant again and
replaced its payload, so the concurrency token guarded only the time between that second read and the save. A write
that landed between the two reads was overwritten:

- the touch of a session, which the authorize request that resolves the session performs at most once a minute
  ([ADR-0220](0220-an-end-user-session-always-ends-and-its-grant-expires-when-it-does.md)), removed a client that a
  concurrent authorize request added to the session, and two concurrent additions of a client lost one, so a client
  that was missing from the session received no back-channel logout when the session was terminated;
- a poll of a device-code grant or of a CIBA grant replaced the payload that it read with a copy that records the poll,
  so an approval that landed between the poll's read and its write was lost;
- the approval and the denial of a device authorization or of a CIBA request replaced the payload without checking
  that it was still pending as stored, so two approvals both succeeded, and both CIBA approvals delivered a ping or
  pushed tokens (backlog `R91`).

## Decision

**A grant's payload is changed only by `IPersistedGrantService.UpdatePayloadAsync`, which applies a function of the
caller to the grant as it is stored and reads again when its write loses a race.**

1. **One read-modify-write.** `UpdatePayloadAsync<TPayload>(openIdContext, grantId, update, cancellationToken)` reads
   the grant in a unit of work and returns `false`, without calling `update`, when the grant is unknown, belongs to
   another tenant, or is not `Active` because it is revoked or has expired. Otherwise it calls `update` with the grant
   as stored, whose payload is deserialized through `IPersistedPayloadSerializer`. When `update` returns `null` the
   call writes nothing and returns `false`. When it returns a value, the call serializes the payload, sets the
   expiration when the value carries one, saves under the concurrency token of that read, and returns `true`. A
   `StoreConcurrencyException` opens a unit of work, reads the grant again and calls `update` again, up to
   `MaxSaveAttempts` (3) attempts in total, as the revocations do
   ([ADR-0215](0215-a-revocation-wins-a-concurrency-race.md)); after the last lost attempt the exception propagates.
   `true` means that this call changed the grant. `update` may run up to three times, so it decides from its argument
   only and has no side effect.
2. **`PersistedGrantUpdate<TPayload>` carries the change.** It is a `public readonly record struct` with the required
   `Payload` to persist and `ExpiresWhen`, which is `null` to keep the expiration of the stored grant.
3. **Every writer of a payload uses it and checks its precondition on the stored grant.** The overload of
   `UpdatePayloadAsync` that replaces the payload with a value and `UpdatePayloadAndExpirationAsync` are removed.
   - `ISessionService.AddClientAsync` adds the client only when the stored session does not list it, with no read of
     its own before the call. It returns `true` only when this call added the client.
   - The device verification endpoint approves or denies a device authorization only when the stored request is
     `Pending`.
   - The poll of a device-code grant and the poll of a CIBA grant record `LastPolledWhen` only on a `Pending` request,
     so an approval that lands between the read of the poll and its write stands.
   - `ICibaRequestService.ApproveAsync` and `DenyAsync` decide only a `Pending` request and report whether this call
     decided it, so only the approval that won delivers a ping or pushes tokens.
4. **The touch of a session is applied to the session as stored.** `ISessionService.ValidateAndTouchAsync` decides
   from its first read whether a touch is due, and then applies the touch to the stored session: the change is `null`
   when another request touched the session within the one-minute granularity or the session has ended. Otherwise it
   sets `LastActivityWhen` to the time of the request, stamps the absolute end, and sets the expiration of the grant to
   the end of the session. A client that a concurrent request added therefore stays. A touch that loses every attempt
   is dropped, because each write that won is itself activity, and the session is still valid.

## Options considered

- **Pass the concurrency token that the caller read.** Rejected: each caller would repeat the loop that reads again
  and retries, and a caller that read without the token would still overwrite a concurrent write.
- **A retry in each caller.** Rejected: every writer would repeat the loop and the check of its precondition, and one
  that forgets either overwrites a concurrent write.
- **Merge the members of the payload in the service.** Rejected: the service cannot know which member a caller
  changed, whether it appended to a list, decided a status, or recorded a time, so a merge would keep the wrong value.
- **Keep the replacement of the payload for the device and CIBA grants.** Rejected: its overwrites lose an approval and
  let two approvals both deliver.

## Consequences

- No concurrent write to a grant's payload is lost: a function runs against the grant as stored, and the write is
  guarded by the concurrency token of that read.
- Each change costs a read and, for each attempt, a write; a change that the stored grant does not need writes
  nothing.
- A function runs up to three times, so it decides from its argument only. A change that loses every attempt reaches
  its caller as `StoreConcurrencyException`, except the touch of a session, which is dropped.
- `ISessionService.AddClientAsync` reports whether this call added the client, and `ICibaRequestService.ApproveAsync`
  and `DenyAsync` report whether this call decided the request.
- The Entity Framework InMemory provider checks no concurrency token, so the hermetic host cannot show the race; the
  unit tests cover it.
- **Breaking:** `IPersistedGrantService.UpdatePayloadAndExpirationAsync` and the overload of `UpdatePayloadAsync` that
  takes a payload are removed, `UpdatePayloadAsync` takes the function that computes the change, and
  `PersistedGrantUpdate<TPayload>` is added, so every implementation of the service implements it. The result of
  `ISessionService.AddClientAsync` means that this call added the client.
- This implements backlog `R91`.

## References

- [ADR-0012](0012-interceptor-managed-concurrency-tokens.md) — the interceptor-managed concurrency tokens that detect
  the lost race.
- [ADR-0208](0208-a-lost-concurrency-race-surfaces-as-a-store-neutral-exception.md) — a lost race is a
  `StoreConcurrencyException`.
- [ADR-0215](0215-a-revocation-wins-a-concurrency-race.md) — the revocations that read again and retry, up to three
  attempts.
- [ADR-0220](0220-an-end-user-session-always-ends-and-its-grant-expires-when-it-does.md) — the session bounds that the
  touch advances.
- Code: `NCode.Identity.OpenId.Authentication.Persistence.DefaultPersistedGrantService`,
  `NCode.Identity.OpenId.Authentication.Sessions.DefaultSessionService`,
  `NCode.Identity.OpenId.Authentication.Endpoints.DeviceAuthorization.DefaultDeviceVerificationEndpointHandler`,
  `NCode.Identity.OpenId.Authentication.Endpoints.Token.DeviceCode.DefaultDeviceCodeGrantHandler`,
  `NCode.Identity.OpenId.Authentication.Endpoints.Token.Ciba.DefaultCibaGrantHandler`,
  `NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication.DefaultCibaRequestService`.
