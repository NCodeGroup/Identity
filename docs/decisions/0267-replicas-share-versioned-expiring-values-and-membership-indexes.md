# 267. Replicas share versioned expiring values and membership indexes

- **Status:** Accepted
- **Date:** 2026-10-26
- **Deciders:** NCode Group

## Context

The backend-for-frontend ([design](../backend-for-frontend.md), §5.2–§5.5 and §5.8, issue #123) keeps its state on the
server and any replica may serve the next request ([ADR-0134](0134-cloud-native-operating-principles.md)). Three things
follow:

- A login transaction is taken exactly once: the callback that redeems it must remove it atomically, so a replayed
  callback finds nothing.
- A session is updated by compare-and-swap, so two concurrent requests that refresh the same tokens cannot overwrite each
  other, and its expiry slides with activity.
- A session is found by the OP's `sid` or `sub` claim, so a back-channel logout can end every session it names.

The family's shared-state ports do not fit. `IExpiringKeySet`
([ADR-0164](0164-replicas-share-single-use-state-through-an-atomic-expiring-key-set.md)) records only that a key was
seen, and `IExpiringCounterSet`
([ADR-0196](0196-tenants-are-rate-limited-through-a-shared-expiring-counter-set.md)) only counts. Neither holds a value,
and neither can look anything up by a secondary key.

## Decision

**State that replicas share as a value is held in an expiring value set whose entries carry a version for
compare-and-swap, and a lookup by a secondary key is held in an expiring index set whose members expire individually;
both have an in-process default and a Valkey adapter.**

1. **The value port.** `NCode.Identity.SharedState.Abstractions` defines `IExpiringValueSet` and
   `ExpiringValue(ReadOnlyMemory<byte> Value, long Version)`:
   - `TryAddAsync(key, value, expiresWhen)` stores a value only when the key is absent or expired, at version 1, and
     reports whether it did;
   - `GetAsync(key)` returns the live `ExpiringValue`, or `null` when the key is absent or expired;
   - `TryReplaceAsync(key, expectedVersion, value, expiresWhen)` replaces the value only when the live version equals
     `expectedVersion`, sets the version to `expectedVersion + 1`, and reports whether it did. It takes a new absolute
     expiry, so a session's idle window can slide. An expiry that is not in the future returns `false` and leaves the
     value unchanged;
   - `TryTakeAsync(key)` atomically removes the live entry and returns it, or returns `null` when it is absent or
     expired, so a login transaction is redeemed once;
   - `RemoveAsync(key)` removes an entry.

   Values are opaque bytes: the caller serializes them, and encrypts what must not be readable in the store.
2. **Versions.** A version is a `long`. It starts at 1 on add, and a successful replace sets `expectedVersion + 1`. A key
   that is removed and added again restarts at 1, so a stale writer could in principle match a recycled version (ABA).
   The BFF never adds a removed key again: every key derives from a fresh 256-bit random, so ABA does not arise, and the
   port does not pay for a guard against it.
3. **The index port.** `IExpiringIndexSet` keeps a set of members per index key, each member with its own expiry:
   `AddMemberAsync(key, member, expiresWhen)` adds a member, and on an existing member sets its expiry;
   `GetMembersAsync(key)` returns the members that have not expired; `RemoveMemberAsync(key, member)` removes one; and
   `RemoveAsync(key)` removes the whole index. An index holds the keys of the values it finds, for example every session
   of one `sid` or `sub`.
4. **The in-process adapters.** `AddInProcessExpiringValueSet` and `AddInProcessExpiringIndexSet` register the
   in-process defaults, correct for one replica. They copy a value on write, so a caller that reuses its buffer cannot
   change a stored entry, and they sweep expired entries at most once per minute, as the key and counter sets do.
   `AddIdentityServer` does not register them: the OP uses neither port, and the BFF composition registers them.
5. **The Valkey adapters.** `AddValkey` registers both Valkey adapters in place of any registered before them.
   - `ValkeyExpiringValueSet` stores a value as one string: an 8-byte big-endian version followed by the value bytes.
     Add is `SET NX PX`, get is `GET`, take is `GETDEL`, and remove is `DEL`. Replace is one Lua script that compares
     the first 8 bytes with the expected version and, on a match, writes the new version and value with the new
     time-to-live, so the comparison and the write are atomic on the server.
   - `ValkeyExpiringIndexSet` keeps a sorted set per index, with the score of a member equal to its expiry in Unix
     milliseconds from the replica's clock. Reads filter to scores after the replica's now. An add prunes expired
     members and extends the key's `PTTL` to at least the new member's time-to-live in one script, so an index lives at
     most as long as the longest time-to-live applied to it and never grows without bound.
6. **Failure.** A store failure throws, so a caller fails closed; the BFF maps it to `503`. A session that cannot be read
   is not treated as absent.
7. **Metrics.** The new operations `value_set.*` and `index_set.*` are recorded under the shared-state meter
   ([ADR-0197](0197-protocol-metrics-have-one-owner-per-signal-and-only-bounded-dimensions.md)).

## Options considered

- **A Valkey hash for a value (the version and the value as fields).** Rejected: `GETDEL` and `SET NX PX` work only on
  strings, and a hash needs a script for every operation, not just replace.
- **`IDistributedCache`.** Rejected: it has no atomic add, take or compare-and-swap, so a login transaction could be
  redeemed twice and concurrent refreshes could overwrite each other.
- **A version per key that never restarts.** Rejected: callers never add a removed key again, and it would need a
  tombstone that outlives the entry.
- **A plain set plus one key per member for the index.** Rejected: two keys per member, and no single-key pruning, so
  expired members would need a separate sweep.

## Consequences

- A deployment that runs several replicas must configure Valkey for the BFF; with the in-process adapters each replica
  sees only its own sessions and login transactions.
- Index members expire by the replica clocks that wrote and read them, not the server's, as the key and counter sets'
  expiries do; clock skew between replicas moves a member's expiry by the skew.
- The OP registers neither adapter, so it gains no dependency on them.
- The new metric operations `value_set.*` and `index_set.*` appear under the shared-state meter with the existing
  bounded dimensions.
- The Lua scripts are covered by mock-based unit tests only, as the existing scripts are; no test runs them against a
  server.

## References

- [ADR-0164](0164-replicas-share-single-use-state-through-an-atomic-expiring-key-set.md) — the shared key set
- [ADR-0196](0196-tenants-are-rate-limited-through-a-shared-expiring-counter-set.md) — the shared counter set
- [ADR-0197](0197-protocol-metrics-have-one-owner-per-signal-and-only-bounded-dimensions.md) — metrics
- [Backend for frontend](../backend-for-frontend.md)
- `src/NCode.Identity.SharedState.Abstractions/IExpiringValueSet.cs`, `ExpiringValue.cs`, `IExpiringIndexSet.cs`,
  `src/NCode.Identity.SharedState/InProcess/DefaultInProcessExpiringValueSet.cs`,
  `DefaultInProcessExpiringIndexSet.cs`,
  `src/NCode.Identity.SharedState.Valkey/ValkeyExpiringValueSet.cs`, `ValkeyExpiringIndexSet.cs`
