# 0073. PostgreSQL developer database with an Aspire migration-worker, and data-protection keys stored in the database

- **Status:** Accepted (supersedes [ADR-0072](0072-self-healing-dev-database-and-model-drift-gate.md); amends
  [ADR-0021](0021-developer-signing-keys-seeded-through-persistence.md),
  [ADR-0041](0041-host-owned-ef-migrations-deliberate-outside-development.md), and
  [ADR-0071](0071-aspire-local-dev-orchestrator.md))
- **Date:** 2026-10-11
- **Deciders:** NCode Group

## Context

The reference host bootstrapped its database **in-process** on startup, selecting one of three providers:
a disposable **SQLite** file (persistent developer keys), a developer-supplied **SQL Server**, or an **in-memory**
store. [ADR-0072](0072-self-healing-dev-database-and-model-drift-gate.md) made the SQLite file self-healing to keep the
inner loop from crashing on un-migrated model edits. The SQLite provider was a convenience added early to "have
something to play with"; it is not a production-fidelity relational engine, and it cannot cleanly support a dedicated
migration process (its file path is per-process, so two processes cannot share one database the Aspire-idiomatic way).

Persistent developer signing keys ([ADR-0021](0021-developer-signing-keys-seeded-through-persistence.md)) added a
**second** piece of developer state on the filesystem: the ASP.NET Core Data Protection key ring under
`App_Data/dp-keys` (it protects the signing key stored in the database). Resetting the dev environment therefore meant
hand-deleting the SQLite files **and** the key-ring directory, and keeping the two in sync — delete one but not the
other and the stored signing key becomes undecryptable.

The AppHost ([ADR-0071](0071-aspire-local-dev-orchestrator.md)) already orchestrates the local stack but had no
database resource, and [#144](https://github.com/NCodeGroup/Identity/issues/144) tracked relocating the database bootstrap out of the
`api` process into a dedicated Aspire resource so a migration failure surfaces as a failed dashboard resource rather
than an `api` crash.

The standing posture is a single, cohesive local-dev story — not provider sprawl patched over time.

## Decision

Make the local developer database **PostgreSQL**, orchestrated by the Aspire AppHost, with a dedicated migration
worker; and persist the **Data Protection key ring in that same database** so all developer state lives in one place.

- **One provider: Postgres.** The host's SQLite, SQL Server, and in-memory provider branches are removed. The
  `api` reads an `openiddb` connection string (injected by Aspire via `WithReference`) and uses Npgsql. With no
  connection string (hermetic tests that boot the composition root and override the context), it falls back to an
  in-memory store — the only remaining non-Postgres path, and it never touches the developer-state wiring.
- **Aspire owns the database, persistently.** The AppHost adds a Postgres container with a **stable named data volume**
  (`ncode-identity-openiddb-data`), so data **survives restarts** by default. A single
  [`build/dev-db-reset.ps1`](../../build/dev-db-reset.ps1) removes that volume for an **on-demand purge**; the next run
  rebuilds the schema and re-seeds. The script auto-detects Docker or Podman, matching Aspire's own runtime detection.
- **Dedicated migration worker ([#144](https://github.com/NCodeGroup/Identity/issues/144)).** A new `NCode.Identity.OpenId.Host.MigrationService` worker applies the
  migrations and exits. The `api` `WaitForCompletion`s on it, so a migration failure is a **failed dashboard resource**
  that blocks the `api` instead of crashing it. Schema application leaves the `api`; **seeding** (admin, SPA client,
  signing key) stays in the `api` as idempotent seed handlers.
- **Shared database project.** A new host-owned `NCode.Identity.OpenId.Host.Database` project owns the Npgsql
  wiring, the design-time factories, and the migrations, so the `api` and the worker consume one source of truth.
  Migrations move here from the host (amending [ADR-0041](0041-host-owned-ef-migrations-deliberate-outside-development.md),
  whose "host owns migrations, applied deliberately outside Development" policy otherwise stands).
- **Data Protection keys in the database.** The key ring is persisted with `PersistKeysToDbContext` into a small
  host-owned `DeveloperDataProtectionDbContext` (its own migrations-history table) targeting the **same** Postgres
  database — not a file under `App_Data/`. The OpenID data, the seeded signing key, and the ring that protects it now
  reset **together** with one volume purge, with nothing left on the filesystem. The context is deliberately **not**
  the shared `OpenIdDbContext`, so the published persistence library never forces a `DataProtectionKeys` table on other
  hosts. `NCode.Identity.Server`'s `AddDeveloperKeys`/`AddDeveloperSigningKey` no longer choose a key-ring location —
  the host owns Data Protection persistence (amending [ADR-0021](0021-developer-signing-keys-seeded-through-persistence.md)).
- **Drift gate covers both contexts.** [`dod.ps1`](../../build/dod.ps1) runs `has-pending-model-changes` for the
  `OpenIdDbContext` and the `DeveloperDataProtectionDbContext` against the `Database` project.

## Options considered

- **Keep SQLite, add self-healing only (ADR-0072, status quo).** Not production-fidelity and cannot host a shared
  migration-worker process; purge still means hand-deleting two filesystem locations. Rejected as the duct-tape path.
- **A shared SQLite file with an explicit absolute path passed to both processes.** Avoids a container runtime but
  needs bespoke path plumbing, keeps SQLite's non-production semantics, and still splits dev state (DB file + key-ring
  file). Rejected in favor of the idiomatic container model.
- **Ephemeral Postgres (no volume), fresh every run.** Maximal self-heal, but the user wants data to persist across
  restarts and purge only on demand. Rejected for the persistent-volume + reset-script model.
- **Data Protection keys as a file, purged by the reset script alongside the volume.** Works, but leaves two storage
  locations and a filesystem artifact. Rejected: one database is more cohesive and the purge is genuinely single-action.
- **Put `DataProtectionKeys` in the shared `OpenIdDbContext`.** Simpler wiring, but imposes a Data Protection table on
  every consumer of the published persistence library. Rejected; a host-owned context keeps the library pristine.

## Consequences

- **Easier:** one cohesive, production-fidelity local database; data persists across restarts; a single
  `build/dev-db-reset.ps1` purges everything (data + signing key + key ring) consistently; a migration failure is a
  visible, isolated dashboard resource; the drift gate keeps migrations authoritative.
- **Harder / watch for:** a **container runtime (Docker or Podman) is now required** to run the AppHost — this reverses
  the "no container-runtime prerequisite" note in [ADR-0071](0071-aspire-local-dev-orchestrator.md). Running the
  The host **standalone** (without the local-dev orchestrator) now uses an ephemeral in-memory database, so persistent local state
  requires the AppHost. Postgres major-version upgrades can strand an old data volume (purge it with the reset script).
- **Smells that mean you chose wrong:** a provider branch (SQLite/SQL Server) creeping back into the host; a
  `DataProtectionKeys` table appearing in the shared `OpenIdDbContext`; or developer state reappearing under
  `App_Data/` so that a purge no longer resets everything in one action.

## References

- [ADR-0072](0072-self-healing-dev-database-and-model-drift-gate.md) — the self-healing SQLite model this supersedes.
- [ADR-0071](0071-aspire-local-dev-orchestrator.md) — the AppHost this adds the database + worker resources to.
- [ADR-0041](0041-host-owned-ef-migrations-deliberate-outside-development.md) — host-owned migrations (now in the
  host database project).
- [ADR-0021](0021-developer-signing-keys-seeded-through-persistence.md) — persistent developer signing keys (key ring
  now in the database).
- Npgsql EF Core provider; `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore` `PersistKeysToDbContext`;
  Aspire PostgreSQL hosting integration (`WithDataVolume`, `WaitForCompletion`).
