# 1012. Concurrency tokens are interceptor-managed, and a lost race surfaces as a store-neutral exception

- **Status:** Accepted
- **Date:** 2026-10-22
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0109](project-template/0109-a-lost-concurrency-race-is-a-store-neutral-exception.md)

## Context

[project-template ADR-0109](project-template/0109-a-lost-concurrency-race-is-a-store-neutral-exception.md) surfaces a
lost optimistic-concurrency race as one store-neutral exception that the unit of work translates, and answers the loser
of a single-use operation as a second attempt. This record applies it to the family's Entity Framework stores and
single-use grants, and decides who generates the tokens that detect the race.

Two kinds of token live on the mutable entities and must not be conflated:

- **Row-level `[ConcurrencyCheck]` tokens.** Every mutable EF entity (`PlatformEntity`, `TeamEntity`, `TenantEntity`,
  `ClientEntity`, `GrantEntity`, `SecretEntity` and the rest) has one `ConcurrencyToken`. EF treats it as an
  optimistic-concurrency token: it puts the original value in the `UPDATE … WHERE` clause and throws
  `DbUpdateConcurrencyException` on a stale write. It can be regenerated automatically on any mutation.
- **Slice version columns.** `SettingsConcurrencyToken` and `SecretsConcurrencyToken` are plain columns, not EF
  concurrency tokens. Each versions one slice of a row and must change only when that slice changes, so a running
  provider reloads just that slice. A mechanism that bumps them on any mutation would be wrong.

A single-use grant is spent by `IPersistedGrantService.ConsumeOnceOrDefault`: it reads the grant, refuses one that is
unknown, expired or already consumed, stamps `ConsumedWhen` and saves. The grant row's concurrency token makes the spend
atomic across replicas without a shared-state adapter: of two requests that spend one grant at the same moment, at most
one save succeeds, because the `UPDATE` of the other carries a stale token. Entity Framework reports the lost race as a
`DbUpdateConcurrencyException`, the type of one store technology. `NCode.Identity.OpenId.Authentication` does not
reference Entity Framework and a store need not be built on it, so a caller that handles the race must not depend on that
type. A loser that reached its client as `500` would answer a concurrent second spend differently from a sequential one,
which carries its protocol error.

## Decision

**A save regenerates the row-level concurrency tokens in one interceptor, a slice column is set only by the operation
that changes its slice, and a lost race surfaces as a store-neutral `StoreConcurrencyException`.**

1. **The interceptor owns the row-level tokens.** `ConcurrencyTokenSaveChangesInterceptor`, which `OpenIdDbContext` adds,
   overrides `SavingChanges` and `SavingChangesAsync` with one implementation, so every save, synchronous or
   asynchronous, regenerates every string concurrency token. It always assigns a new value on `Modified` or `Deleted`,
   so the row version changes. On `Added` it assigns one only when the token is empty, because a new row cannot conflict
   and a caller may pre-seed it.
2. **The interceptor's token is the one saved.** It overwrites any token a store assigns to a `Modified` row, so an
   update relies on no value of its own, and a store copies no freshly generated value onto the returned DTO. A store
   whose update changes only a child collection, such as `LocalAccountStore` with an account's claims, assigns the row
   token so that EF marks the row modified and the interceptor regenerates it.
3. **A create returns its token; an update does not.** A create assigns its token up front and returns it on the DTO, so
   a `POST` builds its `201` and `ETag` without a re-read. An update's token is assigned at save, after the store method
   returns, so the DTO cannot carry it: an updating caller re-reads or answers `204`.
4. **Only the operation that changes a slice sets its column.** `UpdateSettingsAsync` sets `SettingsConcurrencyToken` and
   the secret operations set `SecretsConcurrencyToken`. The interceptor never touches them, and they are not marked
   `[ConcurrencyCheck]`.
5. **`IStoreManager` owns `SaveChangesAsync`,** so several store operations commit as one unit of work, and
   `BaseStore.GetLocalOrDefault` finds an already-tracked entity before it queries, so a load-then-update flow, such as
   grant consume and revoke, makes no second round trip.
6. **A lost race is a `StoreConcurrencyException`.** `NCode.Identity.Persistence.Stores.StoreConcurrencyException`
   (`NCode.Identity.Persistence.Abstractions`, a `[PublicAPI] public class` with the three standard exception
   constructors) is thrown when a unit of work writes an entity that another unit of work changed after it was read, as
   the entity's concurrency token shows. The caller lost the race and must not retry the same write.
7. **The store manager translates a failed save.** `EntityStoreManager.SaveChangesAsync` catches Entity Framework's
   `DbUpdateConcurrencyException`, the failure of the `[ConcurrencyCheck]` in the `UPDATE` clause, and throws a
   `StoreConcurrencyException` that has it as its inner exception. Any other failure propagates unchanged, and a caller
   that saves through a `DbContext` directly receives Entity Framework's type.
8. **A store's own token check throws it too.** Every Entity Framework store that compares a caller-presented token with
   the stored one throws a `StoreConcurrencyException` when they differ: `PlatformStore`, `TeamStore`, `TenantStore`,
   `ClientStore`, `ClientGrantStore`, `GrantStore`, `ResourceServerStore`, `MasterKeyStore`, `OwnerRootKeyStore` and
   `SecretEnvelopeStore`. The management API checks a caller's staleness at the edge with `If-Match`, and answers a lost
   write `412` ([ADR-1483](1483-every-management-error-is-a-problem-document-that-its-route-declares.md)); the stores' own check is the
   second line.
9. **`ConsumeOnceOrDefault` answers the loser as a second spend.** It catches `StoreConcurrencyException` around its
   update and save and returns `null`, as it does for a grant that is unknown, expired or already consumed. Every caller
   answers `null` as a second spend, concurrent or sequential, never `500`:
   - an authorization code: `invalid_grant` (`400`), and the replay revokes the session's grants
     ([ADR-1112](1112-every-end-of-a-session-runs-one-termination-seam-once-revoking-the-grants-issued-under-it.md));
   - a device code: `invalid_grant` (`400`);
   - a CIBA `auth_req_id`: `invalid_grant` (`400`);
   - a pushed authorization request: `invalid_request_uri` (`400`), as for any `request_uri` that is unknown, expired or
     used;
   - an initial access token: `invalid_token` (`401`), and nothing is stored;
   - the `state` of a paused authorization flow at the continue endpoint: `400`.

## Options considered

- **Manual tokens without the interceptor.** Rejected: it duplicates the read, compare, assign and copy-to-DTO steps in
  every `AddAsync` and `UpdateAsync`, and a new store must remember them.
- **The interceptor also manages the slice columns.** Rejected: they are not row-level concurrency tokens and must change
  per slice, not per mutation. Marking them `[ConcurrencyCheck]` would make EF put every one in the `WHERE` clause and
  bump them on unrelated edits.
- **A store calls `SaveChanges` to back-fill the DTO's token.** Rejected: it breaks the unit of work, which `IStoreManager`
  owns so that several store operations commit together.
- **Surrogate ids on the DTOs, so a store can re-read by id.** Rejected: the `Local`-first lookup removes the re-read and
  keeps the DTO and entity boundary intact.
- **A conditional `ExecuteUpdateAsync` that spends the grant row.** Rejected: the EF InMemory provider does not support
  it, and it bypasses the interceptor-managed token.
- **An `IExpiringKeySet` spend record.** Rejected: the grant already has a row, and a second record would have to agree
  with it ([ADR-1164](1164-replicas-share-expiring-keys-values-and-indexes-through-ports-with-in-process-and-valkey-adapters.md)).
- **The interceptor for the row tokens, `StoreConcurrencyException` for the race, and the grant row's token as the spend
  (chosen).**

## Consequences

- Concurrency-token regeneration is verifiable end to end: a store test that saves through `SaveChangesAsync` and
  re-reads observes the token change. The EF InMemory provider applies the interceptor's writes but does not enforce
  `[ConcurrencyCheck]` in the `WHERE` clause, so the conflict test runs on SQLite (`GrantStoreConcurrencyTests`).
- Of two requests that spend one grant at the same moment, one receives the grant and the other receives the error of a
  second spend. No request reaches its client as `500` for it.
- Callers such as `NCode.Identity.OpenId.Authentication` handle a lost race without a dependency on Entity Framework, and
  a store built on another technology throws the same type.
- A caller handles a lost race around a store method or around `IStoreManager.SaveChangesAsync` by catching
  `StoreConcurrencyException`, never Entity Framework's `DbUpdateConcurrencyException`.
- Refresh-token rotation spends the presented token with its successor in one unit of work
  ([ADR-1216](1216-a-refresh-token-rotates-unless-it-is-bound-and-rotation-spends-it-with-its-successor-atomically.md)), and a revocation retries a lost race
  ([ADR-1222](1222-a-grant-is-changed-by-a-read-modify-write-and-a-revocation-that-retry-a-lost-race.md)).

## References

- [project-template ADR-0109](project-template/0109-a-lost-concurrency-race-is-a-store-neutral-exception.md)
- [ADR-1112](1112-every-end-of-a-session-runs-one-termination-seam-once-revoking-the-grants-issued-under-it.md),
  [ADR-1164](1164-replicas-share-expiring-keys-values-and-indexes-through-ports-with-in-process-and-valkey-adapters.md),
  [ADR-1216](1216-a-refresh-token-rotates-unless-it-is-bound-and-rotation-spends-it-with-its-successor-atomically.md),
  [ADR-1483](1483-every-management-error-is-a-problem-document-that-its-route-declares.md),
  [ADR-1222](1222-a-grant-is-changed-by-a-read-modify-write-and-a-revocation-that-retry-a-lost-race.md)
- Code: `NCode.Identity.OpenId.Persistence.EntityFramework.Configuration.ConcurrencyTokenSaveChangesInterceptor`,
  `NCode.Identity.OpenId.Persistence.EntityFramework.Stores.BaseStore`,
  `NCode.Identity.OpenId.Persistence.EntityFramework.Stores.EntityStoreManager`,
  `NCode.Identity.Persistence.Stores.StoreConcurrencyException`,
  `NCode.Identity.OpenId.Authentication.Persistence.DefaultPersistedGrantService`.
