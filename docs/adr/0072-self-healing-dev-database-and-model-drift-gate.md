# 0072. Self-healing local developer database + a Definition-of-Done model-drift gate

- **Status:** Accepted
- **Date:** 2026-10-11
- **Deciders:** NCode Group

## Context

The Playground reference host bootstraps its database on startup (`InitializeDatabaseAsync`). For the zero-setup
local inner loop it uses a disposable **SQLite** file (`App_Data/openid-dev.db*`) when persistent developer keys are
enabled, or an in-memory database otherwise; a developer may also point it at a real **SQL Server** via a connection
string. EF Core migrations (the snapshot + migration classes) live in the Playground project.

Two recurring friction points motivated this decision:

- **Un-migrated model edits crash the local run.** While iterating on an entity mid-feature, the live model drifts
  ahead of the latest migration. `MigrateAsync` then throws `PendingModelChangesWarning`
  (`The model for context 'OpenIdDbContext' has pending changes`) and the host fails to start — even against a freshly
  deleted database, because the check compares the **model to the migration snapshot**, not to the database contents.
  Deleting the dev DB does not help.
- **Nothing caught the drift before it was pushed.** A model change committed without its migration (observed in the
  `U9` server-management change: a `DisplayName` column with no migration) was only discovered when someone next ran
  the app. Migrations are the source of truth for real databases, so drift must fail fast — ideally pre-push and in CI.

## Decision

Treat the local developer database as **disposable and self-healing**, and add a **drift gate** to the Definition of
Done so migrations stay authoritative for real databases.

**Self-healing (Playground `InitializeDatabaseAsync`, `Development` only):**

- **SQLite dev file — always reconciled to the live model, no manual intervention:**
  - **Pending model changes** (mid-feature, no migration yet) → rebuild the disposable DB directly from the model
    (`EnsureDeletedAsync` + `EnsureCreatedAsync`) and log a loud warning that a migration is still required before push.
  - **No drift but tables without a migrations-history row** (built by a prior model-rebuild) → discard it so
    `MigrateAsync` rebuilds a clean, migration-managed database. This replaces the old "delete the file yourself" error.
  - **No drift, otherwise** → `MigrateAsync` (the production-fidelity path).
- **SQL Server in Development is never self-healed.** It keeps the deliberate migrate-only path and the
  "not managed by migrations" guard that throws rather than auto-deleting — a real server is never dropped.
- **Production never auto-applies schema on startup** (unchanged): schema is applied deliberately (EF CLI / idempotent
  script).

**Drift gate ([`dod.ps1`](../../build/dod.ps1)):** after the build, run
`dotnet ef migrations has-pending-model-changes` (with `--no-build`, reusing the build output) against the Playground.
Exit code 1 (pending changes) fails the Definition of Done, so an un-migrated model edit fails pre-push (`-Fast`) and in
CI, at the moment the feature is added.

The two halves are complementary: self-healing keeps the **inner loop** frictionless (never crash, never hand-delete
the file), and the drift gate guarantees a migration exists before the change **leaves the machine**.

## Options considered

- **Migrate-on-startup only (status quo).** Simple, but crashes the local run on every un-migrated edit and catches
  drift only at runtime. Rejected.
- **`HasPendingModelChanges()` unit test in the EF test project.** The test project builds the model from contributors
  but holds **no migrations snapshot** (migrations live in the Playground host), so the test would have to reference the
  executable host — an ugly layering inversion. Rejected in favor of the CLI gate, which targets the project that owns
  the snapshot.
- **An Aspire migration-worker resource.** Aspire ([ADR-0071](0071-aspire-local-dev-orchestrator.md)) is a runtime
  orchestrator with no concept of model-vs-migration drift; a dedicated worker would relocate the failure to the
  dashboard but neither **prevent** drift nor remove the inner-loop friction, at the cost of a new project. Deferred as
  optional runtime polish; see [`BACKLOG.md`](../../BACKLOG.md).

## Consequences

- **Easier:** iterate on entity shapes freely — the local SQLite DB rebuilds itself to match the live model and the
  host always starts; no more manual `openid-dev.db*` deletion; drift is caught automatically pre-push and in CI.
- **Harder / watch for:** the drift gate adds a short `dotnet ef` step to every DoD run; because local dev no longer
  blocks on a missing migration, authoring the migration is enforced by the gate rather than by a crash, so the loud
  startup warning and the DoD failure are the reminders.
- **Smells that mean you chose wrong:** if self-heal ever deletes a **SQL Server** database, or runs outside
  `Development`, the disposability scope has leaked — it must stay gated to the SQLite dev file in Development.

## References

- [ADR-0071](0071-aspire-local-dev-orchestrator.md) — Aspire local-dev orchestrator (the host this runs under).
- EF Core `DatabaseFacade.HasPendingModelChanges()` and `dotnet ef migrations has-pending-model-changes`.
- [`build/dod.ps1`](../../build/dod.ps1) — the Definition of Done that runs the drift gate.
