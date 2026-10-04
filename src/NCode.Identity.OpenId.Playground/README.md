# NCode.Identity.OpenId.Playground

A runnable ASP.NET Core reference host for the OpenID Connect + OAuth 2.0 library family. This document covers the
**local database environment** and the **reset procedure**.

## Database providers

The host selects its persistence provider at startup (in `Program.cs`):

| Condition                                                        | Provider                                    | State                     | Typical use                                         |
| ---------------------------------------------------------------- | ------------------------------------------- | ------------------------- | --------------------------------------------------- |
| `ConnectionStrings:OpenId` is set                                | **SQL Server**                              | persistent                | a real/shared database                              |
| `DeveloperKeys:Mode=PersistentSigningKey` (no connection string) | **SQLite** file at `App_Data/openid-dev.db` | persistent, per-developer | local development with state that survives restarts |
| otherwise                                                        | **EF in-memory**                            | ephemeral                 | zero-setup runs and tests (rebuilt each run)        |

The persistent-SQLite mode is enabled together with persistent developer signing keys via
`DeveloperKeys:Mode=PersistentSigningKey` (set in `launchSettings.json` for the local profile, so test hosts keep the
ephemeral default). In that mode the host writes two things under `App_Data/` (both git-ignored):

- `App_Data/openid-dev.db` (plus the `-wal` / `-shm` sidecars) — the SQLite database.
- `App_Data/dp-keys/` — persistent developer signing keys (see ADR-0002). **Not** part of the database; leave it alone
  when resetting the database.

## Schema management (EF Core migrations)

The schema is managed by **EF Core migrations**, not `EnsureCreated()` (see
[ADR-0041](../../docs/adr/0041-host-owned-ef-migrations-deliberate-outside-development.md)). The migrations live in this
host under `Migrations/` (SQLite); the published persistence library ships none.

- **Development** auto-applies pending migrations on startup (`Database.Migrate()`), so the local database evolves in
  place without losing data.
- **Production** (any non-Development environment) never migrates on startup — apply schema changes deliberately with
  `dotnet ef database update` or a generated idempotent script (`dotnet ef migrations script --idempotent`).
- The **in-memory** provider has no migrations; it is built from the model each run.

Add a migration after changing the EF model:

```pwsh
dotnet tool restore   # first time only (provides dotnet-ef)
dotnet ef migrations add <Name> `
  --project src/NCode.Identity.OpenId.Playground `
  --startup-project src/NCode.Identity.OpenId.Playground `
  --output-dir Migrations
```

It is applied automatically on the next Development run (or `dotnet ef database update` to apply now).

## Resetting the local database

A reset means deleting the **disposable database**, not the committed migrations. Delete the SQLite files and restart;
the next Development run rebuilds the schema from migrations (and re-seeds):

```pwsh
Remove-Item "src/NCode.Identity.OpenId.Playground/App_Data/openid-dev.db*" -Force
```

Keep the `App_Data/dp-keys/` folder unless you also want to rotate the local developer signing keys.

### "table … already exists" on startup

If startup fails reporting that a table already exists, your database predates migrations (it was built by the old
`EnsureCreated()`) and has no migrations history, so it cannot be migrated in place. Development startup detects this and
fails with guidance to delete `App_Data/openid-dev.db*`; do that one-time reset and restart. The host never deletes the
database for you.
