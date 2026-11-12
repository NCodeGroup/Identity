# 1073. One PostgreSQL database holds the host's schema and key ring, migrated by a worker and drift-checked

- **Status:** Accepted
- **Date:** 2026-10-11
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0143](project-template/0143-migrations-run-before-the-rollout-and-only-expand.md)

## Context

The reference host persists the OpenID model through Entity Framework Core, and the ASP.NET Core Data Protection key
ring protects the signing keys stored in that database. Several forces shape where the schema lives and who applies it:

- The published `NCode.Identity.OpenId.Persistence.EntityFramework` package is provider-neutral. Migrations written in
  it would couple every consumer to one provider and one schema history.
- Developer state is more than one file. The relational data, the seeded master and signing keys, and the key ring that
  decrypts them must reset together; delete one without the others and a stored key becomes undecryptable.
- A migration that fails inside the `api` process crashes the host. A dedicated resource makes the failure visible and
  isolated.
- A model change without its migration breaks the first deployment that needs it, so the build has to catch drift.
- [project-template ADR-0143](project-template/0143-migrations-run-before-the-rollout-and-only-expand.md) fixes the
  generic rules (a design-time factory, no migration at startup outside development, a drift check). This record
  places them in the family's projects. The rollout and expand-and-contract rules are
  [ADR-1180](1180-migrations-run-before-the-rollout-and-keep-the-previous-release-working.md)'s.

The standing posture is one cohesive story, not provider sprawl patched over time.

## Decision

PostgreSQL through Npgsql is the host's only relational provider, and one database, `openiddb`, holds the schema and the
key ring. A worker applies the migrations, the host never does, and the build fails on drift.

- **One provider.** `NCode.Identity.OpenId.Host` reads the `openiddb` connection string, registers `OpenIdDbContext`
  with `UseOpenIdNpgsql`, and tags an `openiddb` `AddDbContextCheck<OpenIdDbContext>` with
  `HealthCheckTags.Readiness`. Without a connection string, as in hermetic test hosts that replace the context, the host
  uses an in-memory `OpenIdDbContext` and configures no Data Protection repository, which Production refuses.
- **A host-owned database project.** The published `NCode.Identity.OpenId.Persistence.EntityFramework` ships the model
  only. The unpacked `NCode.Identity.OpenId.Host.Database` holds the Npgsql wiring (`HostDatabase`, which points
  `MigrationsAssembly` at itself), the design-time factories `OpenIdDbContextDesignTimeFactory` and
  `DeveloperDataProtectionDbContextDesignTimeFactory`, so `dotnet ef` never boots the host, and both contexts'
  migrations.
- **The worker migrates, the host never does.** The host does not migrate on startup, in any environment.
  `NCode.Identity.OpenId.Host.MigrationService` applies both contexts' migrations through `MigrationRunner` and exits.
  The AppHost holds `api` with `WaitForCompletion(migrations)`, so a failed migration is a failed dashboard resource, and
  the chart's Job runs the same worker
  ([ADR-1180](1180-migrations-run-before-the-rollout-and-keep-the-previous-release-working.md)). Seeding stays in the
  host as idempotent seed handlers.
- **A persistent local database.** Locally the AppHost runs Postgres with the stable named volume
  `ncode-identity-openiddb-data`, so data survives restarts. `build/dev-db-reset.ps1`, which detects Docker or Podman,
  removes that volume; the next run recreates the schema and reseeds.
- **The key ring lives in the same database.** It is persisted with `PersistKeysToDbContext<DeveloperDataProtectionDbContext>()`
  under the application name `NCode.Identity.OpenId.Host`, in a host-owned context with its own migrations history in
  `openiddb`. One volume purge resets the data, the master keys and the ring together. The ring is never in
  `OpenIdDbContext`, so the published library imposes no `DataProtectionKeys` table.
- **A drift gate.** The `ef model drift` step of `build/dod.ps1` runs
  `dotnet ef migrations has-pending-model-changes --no-build` against the Database project, as project and startup
  project, for `OpenIdDbContext` and `DeveloperDataProtectionDbContext`. A model change without its migration fails
  pre-push and in CI.
- **Migrations are generated code.** Everything under `Migrations/` is `generated_code = true` in `.editorconfig` and
  listed in `.csharpierignore`. Hand-written types such as `ExpandOnlyRule` stay outside that folder.

## Options considered

- **Migrations in the published persistence library.** It couples the library to one provider. Rejected.
- **`EnsureCreated`.** It never evolves a schema. Rejected.
- **SQLite, or several providers.** SQLite is not a production-fidelity engine, and its per-process file cannot be
  shared with a dedicated migration process. Several providers mean a branch per provider. Rejected.
- **An ephemeral Postgres without a volume.** It resets every run, where developers want data to persist and to purge on
  demand. Rejected.
- **Data Protection keys in a file, or in `OpenIdDbContext`.** A file leaves two storage locations that a purge must keep
  in sync. A table in the shared context imposes `DataProtectionKeys` on every consumer of the library. Rejected.
- **A `HasPendingModelChanges` unit test.** The EF test project holds no model snapshot to compare. Rejected for the
  `dotnet ef` step in the Definition of Done.
- **One Postgres database, a host-owned Database project, a migration worker, a key ring in a host-owned context and a
  drift step in the gate.** Chosen.

## Consequences

- One production-fidelity local database; data persists across restarts; a single script purges data, signing keys and
  key ring consistently; a migration failure is a visible, isolated resource; and the drift gate keeps the migrations
  authoritative.
- The AppHost needs a container runtime (Docker or Podman). The host run standalone, without the orchestrator, uses an
  ephemeral in-memory database, so persistent local state requires the AppHost. A Postgres major-version upgrade can
  strand an old data volume; purge it with the reset script.
- Smells of a wrong choice: a provider branch returning to the host, a `DataProtectionKeys` table in `OpenIdDbContext`,
  or developer state under `App_Data/`.

## References

- [project-template ADR-0143](project-template/0143-migrations-run-before-the-rollout-and-only-expand.md), items 1, 2
  and 7, which this record implements.
- [ADR-1180](1180-migrations-run-before-the-rollout-and-keep-the-previous-release-working.md): the rollout, the
  expand-and-contract rule and the initial migration.
- [ADR-1071](1071-the-aspire-apphost-runs-the-local-stack-in-health-gated-order-after-checking-the-frontend-toolchain.md):
  the AppHost that runs the database and the worker.
- `hosts/NCode.Identity.OpenId.Host.Database`, `hosts/NCode.Identity.OpenId.Host.MigrationService`,
  `build/dev-db-reset.ps1`, `build/dod.ps1`.
- The Npgsql EF Core provider, `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore` `PersistKeysToDbContext`, and
  the Aspire PostgreSQL hosting integration (`WithDataVolume`, `WaitForCompletion`).
