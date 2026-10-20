# NCode.Identity.OpenId.Host

A runnable ASP.NET Core reference host for the OpenID Connect + OAuth 2.0 library family. This document covers the
**local database environment**, **schema management**, and the **purge/reset procedure**.

## Database provider

The dev database is **PostgreSQL**, orchestrated by the Aspire AppHost
([ADR-0073](../../docs/adr/0073-postgres-dev-database-and-aspire-migration-worker.md)). Run the stack through the
AppHost (not this project directly) so the database, migrations, and host come up in the right order:

```pwsh
dotnet run --project src/NCode.Identity.OpenId.LocalDev
```

The AppHost:

- starts a **Postgres container** (auto-detecting Docker or Podman) with a stable, named data volume
  (`ncode-identity-openiddb-data`) so data **persists across restarts**;
- runs the **migration worker** (`NCode.Identity.OpenId.Host.MigrationService`), which applies the Npgsql
  migrations for both contexts and then exits — a migration failure shows up as a **failed dashboard resource** that
  holds back the `api`, instead of crashing it;
- starts this `api` host, which receives the `openiddb` connection string via Aspire service discovery.

A container runtime (Docker Desktop or Podman) must be running when you launch the AppHost.

> Running this project **standalone** (without the AppHost) has no `openiddb` connection string, so it falls back to an
> ephemeral **EF in-memory** database — convenient for a quick boot or for tests, but state does not survive restarts.

### One database holds everything

The single `openiddb` database holds the OpenID data, the **signing keys every tenant is provisioned with**, and the
**DataProtection key ring** that protects them (persisted via `PersistKeysToDbContext`, not a file under `App_Data/`).
Because they live together, one purge resets all three consistently, with nothing left on the filesystem.

Because the ring sits beside the data it protects, a **Production** host refuses to start until the ring is encrypted
at rest by a key held outside the database
([ADR-0177](../../docs/adr/0177-the-data-protection-key-ring-is-encrypted-at-rest-in-production.md)). Mount an RSA
certificate from the secret store and point `DataProtection:KeyProtection:Certificate` at it: set `CertificatePath` and
`PrivateKeyPath` for PEM files, or `CertificatePath` and `Password` for a PKCS#12 bundle. List the outgoing certificate
under `PreviousCertificates` when rotating. A key management service registered on the server's `DataProtectionBuilder`
also satisfies the check.

## Schema management (EF Core migrations)

The schema is managed by **EF Core migrations**, not `EnsureCreated()`
([ADR-0041](../../docs/adr/0041-host-owned-ef-migrations-deliberate-outside-development.md)). The migrations live in the
host-owned **`NCode.Identity.OpenId.Host.Database`** project under `Migrations/OpenId` and
`Migrations/DataProtection`; the published persistence library ships none.

- The **migration worker** applies pending migrations before the `api` starts — the `api` never applies schema itself.
- **Production** (any non-Development deployment) applies schema deliberately with `dotnet ef database update` or a
  generated idempotent script (`dotnet ef migrations script --idempotent`).

Add a migration after changing the EF model (the `Database` project carries the design-time factories, so it is both
`--project` and `--startup-project`):

```pwsh
dotnet tool restore   # first time only (provides dotnet-ef)
dotnet ef migrations add <Name> `
  --project src/NCode.Identity.OpenId.Host.Database `
  --startup-project src/NCode.Identity.OpenId.Host.Database `
  --context OpenIdDbContext `
  --output-dir Migrations/OpenId
```

The Definition of Done fails if the model has changes no migration captures (`dotnet ef migrations
has-pending-model-changes`, run for both contexts), so author the migration before pushing.

## Purging / resetting the local database

Data is persistent by default. To purge on demand, stop the AppHost and remove the data volume with the helper script —
the next AppHost run rebuilds the schema (migration worker) and re-seeds the dev data:

```pwsh
./build/dev-db-reset.ps1
```

The script auto-detects Docker or Podman and removes the `ncode-identity-openiddb-data` volume (equivalently,
`docker volume rm ncode-identity-openiddb-data`). This is the only reset needed — the provisioned signing keys and the
DataProtection key ring are in the same database and are purged with it.
