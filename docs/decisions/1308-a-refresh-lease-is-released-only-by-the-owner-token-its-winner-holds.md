# 1308. A refresh lease is released only by the owner token its winner holds

- **Status:** Accepted
- **Date:** 2026-11-01
- **Deciders:** NCode Group

## Context

[ADR-1291](1291-the-bff-refreshes-a-session-single-flight-under-a-lease-on-its-refresh-token.md) point 2 releases a won
lease only when the winner finished more than 5 seconds before the lease expires. The winner cannot release a lease it
may no longer hold: `IExpiringValueSet` ([ADR-1267](1267-replicas-share-versioned-expiring-values-and-membership-indexes.md))
has no compare-and-remove, and a key that expires and is added again restarts at version one (an ABA hazard), so a
version the winner read cannot tell its own lease from a newer one that another replica took after it expired.

A slow winner therefore leaves its lease held until it expires, and every replica that lost the race waits for that
expiry even though the refresh has ended. The 5-second margin is a guess about how far the replicas' clocks and the
store's expiry can differ.

## Decision

**A refresh lease stores a random owner token, and the winner always releases exactly its own lease when it finishes.**

1. **The value port compares and removes atomically.** `IExpiringValueSet.TryRemoveAsync(key, expectedValue,
   cancellationToken)` removes the live value only while its bytes equal `expectedValue`, ignoring the version. Of any
   number of concurrent calls that name the same value, across every replica that shares the set, at most one succeeds.
   The in-process adapter removes the exact entry it compared. The Valkey adapter runs one script that compares the
   bytes after the 8-byte version prefix with the expected bytes and, only when they match, runs `DEL`; it is recorded
   as the `value_set.try_remove` operation.
2. **The store creates the owner token.** `IBffSessionStore.TryAcquireRefreshLeaseAsync` stores a fresh 256-bit random
   token as the lease value and returns `BffRefreshLease(LeaseKey, Owner)`, or `null` when the lease is held.
   `ReleaseRefreshLeaseAsync(BffRefreshLease)` compare-and-removes the lease with that owner.
3. **The winner always releases.** `DefaultBffUserTokenService` releases a won lease when the refresh ends, in a
   `finally` with `CancellationToken.None`. The time margin is gone. A lease another replica took after this one expired
   has another owner, so the release leaves it in place. A release that fails because the store is unreachable
   (`BffStoreUnavailableException`) is logged (`RefreshLeaseReleaseFailed`, event 19004, Warning) and changes nothing
   else: the refresh's own result or exception stands, and the lease expires on its own after
   `Tokens:RefreshLeaseDuration`, so the replicas that lost the race wait at most that long, as they would for a dead
   winner.

The comparison uses `SequenceEqual`, not a constant-time compare: the owner token is not a secret that an attacker can
probe, because it never leaves the store.

## Options considered

- **Keep the 5-second time margin.** A slow winner still leaves its lease held until it expires, and the margin stays a
  guess about clocks. Rejected.
- **Remove only when the version matches.** It needs versions that never repeat, which changes the version semantics of
  every value and needs a tombstone that outlives the entry or random start versions. A lease needs identity, not
  order. Rejected.
- **Read the value, then `DEL` it without a script.** Another replica can take the lease between the two commands, so it
  is not atomic. Rejected.
- **A server-side conditional-delete command.** Not every server the adapter supports has one, and the stored string
  carries the version prefix. Rejected.

## Consequences

- A winner frees its lease as soon as it finishes, so the replicas that lost the race wait only for the refresh.
- A lease that another replica took after this one expired is never removed.
- A store outage during the release costs the losers at most one lease duration; it never turns a refresh whose tokens
  were written into a `503`, and never hides the refresh's own failure.
- A custom `IExpiringValueSet` adapter and a custom `IBffSessionStore` implement the new members. The family is
  pre-release ([ADR-1025](1025-the-family-is-greenfield-and-pre-release-tracks-its-public-api-and-models-auth0-parity-plus.md)), so this is a clean cutover with no shim.
- The Valkey script is covered by mock-based tests only, as the other scripts are.

## References

- [ADR-1267](1267-replicas-share-versioned-expiring-values-and-membership-indexes.md) - the value port, amended here
- [ADR-1291](1291-the-bff-refreshes-a-session-single-flight-under-a-lease-on-its-refresh-token.md) - point 2's release
  rule, amended here
- [Issue #422](https://github.com/NCodeGroup/identity/issues/422)
- [Issue #462](https://github.com/NCodeGroup/identity/issues/462)
- `IExpiringValueSet.cs`, `DefaultInProcessExpiringValueSet.cs`, `ValkeyExpiringValueSet.cs`
- `DefaultBffSessionStore.cs`, `DefaultBffUserTokenService.cs`
