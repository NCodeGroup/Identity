# 1218. Ended grants are removed after a retention period in bounded batches

- **Status:** Accepted
- **Date:** 2026-10-23
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0142](project-template/0142-data-that-grows-with-use-is-pruned-in-bounded-batches.md)

## Context

[project-template ADR-0142](project-template/0142-data-that-grows-with-use-is-pruned-in-bounded-batches.md) gives data
that grows with use a configured retention after it ends, and prunes it in bounded batches from a hosted service, each
batch its own unit of work. This record applies it to grants.

Every authorization code, token, rotation of a refresh token and session leaves a grant row, so without a removal the
grants table grows without bound. Deleting a client or a tenant loads all of its grants into one unit of work
([ADR-1212](1212-deleting-a-client-resource-server-principal-or-tenant-removes-what-it-owns.md)), so the growth also
makes the delete a larger unit of work (issue [#267](https://github.com/NCodeGroup/identity/issues/267)).

A grant that is revoked or expired is still read: a rotated refresh token that is presented again is a reuse
([ADR-1217](1217-a-reused-refresh-token-revokes-its-family-unless-it-is-a-retry-within-an-opt-in-interval.md)), and a
replayed authorization code revokes its session
([ADR-1112](1112-every-end-of-a-session-runs-one-termination-seam-once-revoking-the-grants-issued-under-it.md)). A
removal therefore has to wait for a period after the grant ended.

## Decision

**A hosted service removes the grants that ended before a retention period, in bounded batches that every provider
supports.**

1. **A grant ends when it expires or, if earlier, when it is revoked.** A grant that neither expires nor is revoked
   never ends and is kept. A grant of a type that ends only when it is revoked, the end-user session, ends when it is
   revoked, whether or not it expired, so a session that was not terminated is kept
   ([ADR-1223](1223-a-session-that-ends-unused-is-terminated-once-in-the-background.md)).
2. **The store stages a bounded batch for removal.**
   `IGrantStore.RemoveEndedBeforeAsync(endedBefore, grantTypesEndedByRevocation, limit, cancellationToken)` stages up
   to `limit` grants for removal when the unit of work is saved, ordered by `Id`, and returns the count. A grant whose
   type is in `grantTypesEndedByRevocation` is staged only when `RevokedWhen < endedBefore`; any other grant is staged
   when `ExpiresWhen < endedBefore` or `RevokedWhen < endedBefore`. A `limit` below one throws
   `ArgumentOutOfRangeException`. The Entity Framework store reads the rows and removes them through the change
   tracker, as `ITenantUsageStore.RemoveBeforeAsync` does
   ([ADR-1199](1199-tenant-usage-is-a-daily-ledger-that-replicas-add-to-once-and-prune-in-batches.md)).
3. **`GrantPruneService` prunes on a timer.** It is a hosted service that ticks every
   `GrantRetentionOptions.PruneInterval` on a `PeriodicTimer` and removes the grants that ended before the current time
   minus `GrantRetentionOptions.Retention`, in units of work of 256 grants, repeating while a batch is full. It passes
   the end-user session grant type as the grant type that ends by revocation. A batch whose save loses a race, which
   is another replica that removed rows of it, counts as a full batch, because it is progress, and the next batch is
   read. Any other failure is logged (`GrantPruneFailed`, event 2052, Warning), and the prune runs again at the next
   tick; only the host's stopping token ends the service. Every replica runs the service, and concurrent prunes waste
   at most one batch.
4. **`GrantRetentionOptions` configures it.** The options type is `public sealed` with `Retention`, which defaults to 14
   days, and `PruneInterval`, which defaults to one hour. They are bound from `OpenId:GrantRetention` and validated
   when the host starts: `Retention` is not negative, and `PruneInterval` is a period that a timer accepts
   ([ADR-1137](1137-configuration-binds-strictly-and-every-option-timer-period-and-retention-is-validated-at-startup.md)).
5. **The retention is 14 days.** A rotated refresh token that is presented again within 14 days after it ended is
   recognized as a reuse
   ([ADR-1217](1217-a-reused-refresh-token-revokes-its-family-unless-it-is-a-retry-within-an-opt-in-interval.md)), and a
   replayed authorization code still revokes its session
   ([ADR-1112](1112-every-end-of-a-session-runs-one-termination-seam-once-revoking-the-grants-issued-under-it.md)).
6. **The revocation time is indexed.** `GrantEntity` has an index on `RevokedWhen`, as it has one on `ExpiresWhen`, so
   the removal reads the ended grants through an index.

## Options considered

- **Keep a revoked grant until its own expiry.** Rejected: every rotation of a refresh token would leave a row for the
  whole lifetime of the refresh token.
- **Set-based deletes.** Rejected: the Entity Framework InMemory provider does not support them, and the change tracker
  gives every provider one code path
  ([ADR-1199](1199-tenant-usage-is-a-daily-ledger-that-replicas-add-to-once-and-prune-in-batches.md)).
- **One replica elected as the leader that prunes.** Rejected: the election needs shared state, and concurrent prunes
  waste at most one batch.
- **A hosted service on every replica that removes ended grants after the retention, in batches through the change
  tracker.** Chosen: it needs no shared state and works on every provider.

## Consequences

- The grants table holds the live grants and those that ended within the retention.
- Deleting a client or a tenant loads the grants that it removes into one unit of work
  ([ADR-1212](1212-deleting-a-client-resource-server-principal-or-tenant-removes-what-it-owns.md)), and the prune
  bounds that work the same way.
- A refresh token or an authorization code that is presented after its grant was removed is unknown, so it is answered
  as an unknown token, not as a reuse.
- An abandoned session is removed after the retention once it is terminated
  ([ADR-1223](1223-a-session-that-ends-unused-is-terminated-once-in-the-background.md)).
- An operator sets the retention and the interval through configuration.
- Every implementation of `IGrantStore` implements `RemoveEndedBeforeAsync`.
- This implements issue [#267](https://github.com/NCodeGroup/identity/issues/267).

## References

- [ADR-1217](1217-a-reused-refresh-token-revokes-its-family-unless-it-is-a-retry-within-an-opt-in-interval.md) — a
  rotated token that is presented again is a reuse.
- [ADR-1112](1112-every-end-of-a-session-runs-one-termination-seam-once-revoking-the-grants-issued-under-it.md) — a
  replayed authorization code revokes the session.
- [ADR-1199](1199-tenant-usage-is-a-daily-ledger-that-replicas-add-to-once-and-prune-in-batches.md) — the usage ledger
  and the bounded batches that remove rows through the change tracker.
- [ADR-1212](1212-deleting-a-client-resource-server-principal-or-tenant-removes-what-it-owns.md) — deleting an owner
  removes its grants in one unit of work.
- [ADR-1223](1223-a-session-that-ends-unused-is-terminated-once-in-the-background.md) — a session ends when it is
  terminated.
- [ADR-1137](1137-configuration-binds-strictly-and-every-option-timer-period-and-retention-is-validated-at-startup.md)
  — options are validated at start.
- Code: `NCode.Identity.OpenId.Authentication.Persistence.GrantPruneService`,
  `NCode.Identity.OpenId.Authentication.Persistence.GrantRetentionOptions`,
  `NCode.Identity.OpenId.Persistence.Stores.IGrantStore`,
  `NCode.Identity.OpenId.Persistence.EntityFramework.Core.Stores.GrantStore`.
