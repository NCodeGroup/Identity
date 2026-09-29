# 0012. Concurrency tokens are interceptor-managed; the sub-resource version columns stay manual

- **Status:** Accepted
- **Date:** 2026-09-29
- **Deciders:** NCode.Identity maintainers

## Context

Every Entity Framework store (`ServerStore`, `TenantStore`, `ClientStore`, `GrantStore`, and the
per-secret operations on `ServerStore`) generated concurrency tokens **by hand** on every mutation: read
the entity, compare the caller's token, `entity.ConcurrencyToken = NextConcurrencyToken()`, and copy the
new value onto the returned DTO. The same three lines were duplicated across every `AddAsync` /
`UpdateAsync`.

There is already a `ConcurrencyTokenSaveChangesInterceptor` whose entire job is to regenerate string
concurrency-token properties automatically. But it only overrode the **synchronous** `SavingChanges`, and
every store persists through `SaveChangesAsync`, so the interceptor **never fired in production** — it was
dead code (see [ADR-0011](0011-secret-management-api.md)). The manual token juggling existed precisely
because the automatic mechanism silently did nothing.

Two kinds of token live on these entities and must not be conflated:

- **Row-level `[ConcurrencyCheck]` tokens** (`ServerEntity`, `TenantEntity`, `ClientEntity`,
  `GrantEntity`, `SecretEntity` all have one `ConcurrencyToken`). EF treats these as optimistic-concurrency
  tokens — it puts the original value in the `UPDATE … WHERE` clause and throws
  `DbUpdateConcurrencyException` on a stale write. These can be regenerated automatically on any mutation.
- **Sub-resource version columns** (`SettingsConcurrencyToken`, `SecretsConcurrencyToken` on the
  server/tenant/client rows). These are **plain columns**, not EF concurrency tokens. They version a
  _slice_ of the row and must change **only** when that slice changes (a settings edit bumps the settings
  token; a secret edit bumps the secrets token) so the running server/tenant/client provider knows to
  reload just that slice. A mechanism that bumps them on _any_ mutation would be wrong.

## Decision

- **The interceptor owns the row-level `[ConcurrencyCheck]` tokens.** It now overrides **both**
  `SavingChanges` and `SavingChangesAsync` (sharing one implementation), so every save — sync or async —
  regenerates those tokens for added/modified/deleted entities.
- **Stores stop bumping the row token by hand.** `AddAsync`/`UpdateAsync` no longer call
  `entity.ConcurrencyToken = NextConcurrencyToken()` for the row token, and no longer copy a
  freshly-generated value onto the DTO. A newly constructed entity leaves its row token empty for the
  interceptor to fill on save.
- **The sub-resource version columns stay manually managed** in the specific operation that changes their
  slice (`UpdateSettingsAsync` bumps `SettingsConcurrencyToken`; the secret operations bump
  `SecretsConcurrencyToken`). The interceptor deliberately does not touch them.
- **A mutation no longer populates the DTO's row token.** The token is assigned by the interceptor at
  save time, after the store method has returned, so the DTO cannot carry it. Callers that need the fresh
  token **re-read** the resource (the secret `POST` does this) or return `204 No Content` (`PUT`/`DELETE`).
  No existing caller depended on the store back-filling the token — the grant flows never read it, and the
  management read endpoints already load the resource fresh.
- **Optimistic concurrency for the row is EF-native plus API-level `If-Match`.** EF's `[ConcurrencyCheck]`
  detects a concurrent write during the transaction (throwing `DbUpdateConcurrencyException` at
  `SaveChangesAsync`); the management endpoints enforce caller staleness at the edge via `If-Match`. The
  stores keep a defensive token compare as belt-and-suspenders, but it is no longer the primary control.

## Options considered

- **Delete the dead interceptor and keep manual management.** Rejected: it entrenches the duplication the
  interceptor was built to remove and leaves a misleading "auto-manage" class that does nothing.
- **Fix the interceptor to also manage the sub-resource columns.** Rejected: they are not row-level
  concurrency tokens and must change per-slice, not per-mutation; marking them `[ConcurrencyCheck]` would
  make EF treat every one as a row concurrency token (all in the `WHERE` clause) and bump them on unrelated
  edits.
- **Keep populating the DTO token by having the store call `SaveChanges` itself.** Rejected: it breaks the
  unit-of-work (the `IStoreManager` owns `SaveChangesAsync` so multiple store operations commit together).

## Consequences

- The three-line manual token dance is gone from every store; adding a new store or mutation no longer has
  to remember it.
- Concurrency-token regeneration is now verifiable end-to-end: a store test that saves through
  `SaveChangesAsync` and re-reads observes the token change. (The EF **InMemory** provider does honor the
  interceptor's change-tracker writes, but does **not** enforce `[ConcurrencyCheck]` in the `WHERE` clause,
  so a true concurrent-conflict test needs a relational provider — noted for the test backlog.)
- **Follow-up (not in this change): eliminate the store-side re-read on update.** With the token
  auto-managed, an `UpdateAsync` no longer needs to re-read purely to source the token; it re-reads only to
  obtain the tracked entity to mutate. Removing that read (via a `Local`-first lookup when the entity is
  already tracked in the unit of work, or by exposing the surrogate id on the DTO for a `Find`) is a
  worthwhile optimization but changes read semantics in the auth-critical load paths, so it is deferred to
  its own focused pass.

## References

- [ADR-0011](0011-secret-management-api.md) — where the dead interceptor was first discovered.
- `NCode.Identity.OpenId.Persistence.EntityFramework.Configuration.ConcurrencyTokenSaveChangesInterceptor`
- `NCode.Identity.OpenId.Persistence.EntityFramework.Stores.BaseStore` and the four concrete stores.
