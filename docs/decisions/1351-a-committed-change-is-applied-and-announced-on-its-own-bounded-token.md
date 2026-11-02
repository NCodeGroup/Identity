# 1351. A committed change is applied and announced on its own bounded token

- **Status:** Accepted
- **Date:** 2026-11-01
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0141](project-template/0141-side-effects-after-a-commit-go-through-an-outbox.md)

## Context

A management write sends `TenantChangedCommand` or `ServerSettingsChangedCommand` after it commits. The handlers ran
with the request's token and caught everything but `OperationCanceledException`, so a client that disconnected skipped
the eviction, the refresh and the announcement, and a store or Valkey timeout that surfaced as a
`TaskCanceledException` with the request's token untouched failed a write that had already committed. The in-process
broadcaster let a handler's timeout reach the publisher, against
[ADR-1205](1205-replicas-announce-changes-through-a-broadcast-port.md) decision 2.
[project-template ADR-0141](project-template/0141-side-effects-after-a-commit-go-through-an-outbox.md) decision 1 runs
such inline effects on `CancellationToken.None` with their own timeout
([#485](https://github.com/NCodeGroup/Identity/issues/485)).

## Decision

1. **Each step after the commit runs on its own bounded token.** `TenantChangedHandler`'s eviction (the tenant cache
   and the master keys) and announcement, and `ServerSettingsChangedHandler`'s refresh and announcement, each run on
   `CancellationToken.None` bounded by `PostCommitTimeout`, 30 seconds, through the injected `TimeProvider`. The
   request's token reaches none of them.

2. **A failed refresh or announcement is logged, and the write succeeds.** Whatever it threw, it is logged
   (`ServerSettingsChangeRefreshFailed`, `ServerSettingsChangeBroadcastFailed`, `TenantChangeBroadcastFailed`), because
   no caller token flows into these steps.

3. **A failed eviction or seed is still announced.** The tenant handler evicts and seeds inside a `try` whose `finally`
   announces, so the failure still reaches the caller and the other replicas still learn of the change.

4. **The seed keeps the request's token.** Its failure fails the write
   ([ADR-1169](1169-every-tenant-is-provisioned-with-persisted-keys.md)), and a tenant that lacks a key is seeded when it
   is next materialized.

5. **The in-process broadcaster lets a cancellation through only when the publisher cancelled.**
   `DefaultInProcessReplicaBroadcaster` rethrows an `OperationCanceledException` only when the publisher's token is
   cancelled; a handler's own timeout is logged (`ReplicaBroadcastHandlerFailed`) and the other handlers still run.

6. **The bound is fixed, not an option.** It is a backstop behind the store's and the Valkey client's own timeouts, and
   it equals the guarded client's default `TotalTimeout`
   ([ADR-1315](1315-outbound-calls-to-caller-supplied-urls-share-one-guarded-bounded-client.md)).

## Options considered

- **The request's token.** Rejected: a client that disconnects skips the effect of a change that committed.
- **`CancellationToken.None` without a bound.** Rejected: a hung store would hold the response forever.
- **A configured option.** Rejected: a backstop that nobody tunes.
- **An outbox.** Rejected: these steps are a local eviction, a read-back and a best-effort hint, which ADR-0141
  decision 1 keeps inline.

## Consequences

- A committed write succeeds even when its announcement fails, and the replicas converge as
  [ADR-1205](1205-replicas-announce-changes-through-a-broadcast-port.md) decision 6 says.
- A hung store or Valkey adds at most 30 seconds per step to the response.

## References

- [ADR-1169](1169-every-tenant-is-provisioned-with-persisted-keys.md),
  [ADR-1205](1205-replicas-announce-changes-through-a-broadcast-port.md),
  [ADR-1219](1219-server-settings-converge-on-every-replica-through-one-reconciliation.md),
  [ADR-1315](1315-outbound-calls-to-caller-supplied-urls-share-one-guarded-bounded-client.md)
- Issue [#485](https://github.com/NCodeGroup/Identity/issues/485)
- `TenantChangedHandler.cs` and `ServerSettingsChangedHandler.cs` in `NCode.Identity.OpenId.Core`, and
  `DefaultInProcessReplicaBroadcaster.cs` in `NCode.Identity.SharedState`
