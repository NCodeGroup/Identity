# 41. The reference host owns schema with EF Core migrations, applied deliberately outside Development

- **Status:** Accepted
- **Date:** 2026-10-03
- **Deciders:** NCode Group

## Context

The persistence layer is Entity Framework Core (`OpenIdDbContext` in the published
`NCode.Identity.OpenId.Persistence.EntityFramework` package). The `Playground` reference host previously created its
schema with `DbContext.Database.EnsureCreated()`. `EnsureCreated()` is all-or-nothing: it builds the full schema only
when the database is absent and **never evolves an existing one**. During pre-release development (ADR-0025) the EF model
churns constantly — a newly-added entity (for example `ResourceServerEntity`) silently fails at runtime with
`no such table: …` on an existing developer database, and the only remedy was to delete the whole database and lose any
local state. `EnsureCreated()` and migrations are also mutually exclusive: a database built by `EnsureCreated()` has no
`__EFMigrationsHistory`, so it cannot later be migrated.

## Decision

**Adopt EF Core migrations, owned by the host, and apply them deliberately everywhere except local Development.**

- **Migrations live in the reference host (`Playground`), not the published persistence library.** The library ships the
  model only; migrations are provider-specific SQL, and real consumers own their provider and schema lifecycle. Shipping
  migrations in the package would couple it to one provider and to consumers' deployment cadence.
- **Development auto-applies pending migrations on startup** (`Database.Migrate()`), so the local developer database
  evolves in place without losing data.
- **Production (and any non-Development environment) never migrates on startup.** Schema changes are applied
  deliberately out-of-band — the EF CLI (`dotnet ef database update`) or, preferably, a generated **idempotent SQL
  script** (`dotnet ef migrations script --idempotent`) run by a pipeline step or DBA. The application only ever reads a
  schema it did not just mutate.
- **The in-memory provider keeps `EnsureCreated()`.** It has no migrations and is rebuilt from the model each run.
- **SQLite is the only provider with migrations today** (the local-dev provider). SQL Server — used when a connection
  string is configured — gets its own migration set when a real deployment needs it; a SQLite migration cannot run on
  SQL Server.

The EF tools build the context through an `IDesignTimeDbContextFactory<OpenIdDbContext>` in the host (so `dotnet ef`
never boots the full web host), and the runtime registration points `MigrationsAssembly` at the host so `Migrate()`
finds them.

## Options considered

- **Keep `EnsureCreated()`.** Rejected: cannot evolve an existing schema, which is the entire failure mode.
- **`EnsureDeleted()` + `EnsureCreated()` on each model change.** Rejected: rebuilds the database and loses local data,
  which is exactly what we set out to avoid.
- **Auto-migrate on startup in production.** Rejected: schema changes against a shared production database must be
  deliberate and observable, not a side effect of a rolling deploy (and unsafe with multiple instances starting at once).
- **Put migrations in the published EF library.** Rejected: couples the package to a provider and to consumer schema
  lifecycle; consumers should generate their own.

## Consequences

- The local developer database evolves without data loss; adding an entity or column is `dotnet ef migrations add
<Name> --project src/NCode.Identity.OpenId.Playground --startup-project src/NCode.Identity.OpenId.Playground`, applied
  on the next Development startup.
- **One-time switchover cost:** a database previously built by `EnsureCreated()` has no migrations history and must be
  deleted once so the baseline migration can create it cleanly. Development startup detects this (a relational database
  with tables but no applied migrations) and fails fast with an actionable message to delete `App_Data/openid-dev.db*`,
  rather than surfacing a raw `table "…" already exists` provider error; it never auto-deletes the database.
- Production has a reviewable, idempotent SQL artifact per release; the app never changes the schema implicitly.
- Generated migration files are treated as generated code — excluded from CSharpier (`.csharpierignore`) and from the
  analyzer / warnings-as-errors gates (`.editorconfig` `generated_code = true`).
- Adding persistent SQL Server (or another provider) later means a second, provider-specific migration set.

## References

- Code: `NCode.Identity.OpenId.Playground` — `Program` (host wiring, `MigrationsAssembly`, and `InitializeDatabase`),
  `OpenIdDbContextDesignTimeFactory`, `Migrations/`; `OpenIdDbContext`.
- Prior art: [ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md) (pre-release churn),
  [ADR-0008](0008-conventions-and-lessons-live-in-the-repository.md) (decisions live in the repo).
- EF Core: [Applying migrations](https://learn.microsoft.com/ef/core/managing-schemas/migrations/applying),
  [`EnsureCreated()` vs migrations](https://learn.microsoft.com/ef/core/managing-schemas/ensure-created).
