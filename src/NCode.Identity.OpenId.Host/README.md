# NCode.Identity.OpenId.Host

A runnable ASP.NET Core reference host for the OpenID Connect + OAuth 2.0 library family. This document covers the
**local database environment**, **schema management**, the **purge/reset procedure**, and the **container image**.

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

The single `openiddb` database holds the OpenID data, the **signing keys every tenant is provisioned with** (encrypted
under the tenants' master keys, which are rows of the same database), and the **DataProtection key ring** (cookies and
anti-forgery tokens; persisted via `PersistKeysToDbContext`, not a file under `App_Data/`). Because they live together,
one purge resets all of them consistently.

### Secret protection

Every stored secret, including the key ring, is encrypted under a key hierarchy whose root key lives **outside** the
database ([ADR-0235](../../docs/adr/0235-stored-secrets-are-protected-by-a-key-hierarchy-under-an-external-root-key.md)).
A **Production** host refuses to start without a real root key, and a root key must never be committed. The
`SecretProtection` section selects it (environment variables use `__`, for example
`SecretProtection__RootKeyProvider=local`):

| Setting                                           | Meaning                                                        |
|---------------------------------------------------|----------------------------------------------------------------|
| `SecretProtection:RootKeyProvider`                | `ephemeral`, `local`, `aws-kms`, or another registered name    |
| `SecretProtection:RootKeyProviders:Local:KeyPath` | File with a base64url 32-byte key (the `local` provider)       |
| `SecretProtection:RootKeyProviders:Local:Key`     | The same key inline: Development and tests only                |
| `SecretProtection:Recovery:PublicKeyPath`         | Offline recovery RSA public key (PEM) that also wraps the keys |
| `SecretProtection:Recovery:Disabled`              | `true` only to acknowledge running without a recovery key      |

Further providers ship as packages that register themselves by name, such as `aws-kms`, which takes
`SecretProtection:RootKeyProviders:AwsKms:KeyArn` and finds its AWS credentials through the AWS SDK's default
credential chain rather than configuration. The [Helm chart](../../charts/ncode-identity-host/README.md) wires these
settings for a deployment.

**Local development.** Nothing to do: on first run the AppHost writes one development root key to
`LocalApplicationData/NCode.Identity/LocalDev/root-key` (owner-only on Unix) and hands it to both OP instances it
starts, the `api` and the conformance instance, which share the one database. That key lives outside the repository.
`build/dev-db-reset.ps1` resets the database and keeps the key; to replace the key, stop the AppHost, delete the file
and reset the database together. A Development host started without the AppHost uses `ephemeral`, which suits tests and
throwaway databases. After pulling a change that alters how secrets are protected, existing development tenants lose
their unreadable keys automatically and are provisioned again; reset the database if client secrets matter.

## Schema management (EF Core migrations)

The schema is managed by **EF Core migrations**, not `EnsureCreated()`
([ADR-0041](../../docs/adr/0041-host-owned-ef-migrations-deliberate-outside-development.md)). The migrations live in the
host-owned **`NCode.Identity.OpenId.Host.Database`** project under `Migrations/OpenId` and
`Migrations/DataProtection`; the published persistence library ships none.

- The **migration worker** applies pending migrations before the `api` starts — the `api` never applies schema itself.
- **Production** (any non-Development deployment) applies schema deliberately, once per release, before the rollout.
  The Helm chart runs the same worker from the host's image as a `pre-install` and `pre-upgrade` Job, and a failed
  migration stops the release before any pod changes
  ([ADR-0180](../../docs/adr/0180-migrations-run-before-the-rollout-and-keep-the-previous-release-working.md)). Without the
  chart, run the worker the same way, or apply a generated idempotent script
  (`dotnet ef migrations script --idempotent`).

### Every migration keeps the previous release working

The previous release keeps serving while the new schema is applied and until the rollout replaces its last pod, and a
rollback returns to it. So every migration must work with the release before it — **expand, then contract**:

- **Expand** in the release that needs the change: add a table, add a nullable column or one with a default, or add an
  index. Never rename, drop, or tighten anything the previous release reads or writes.
- **Migrate the data** in code or in a later migration while both shapes are valid.
- **Contract** only in a later release, once no running version uses the old shape: drop the old column or table, or
  make the new column required.

A rename is an add, a copy, and a later drop: three steps across at least two releases.

The Definition of Done enforces the rule. `ExpandOnlyRuleTests` reads every migration's `Up` operations and fails one
that drops or renames a table or column, or adds or alters a column in a way the previous release cannot read or write.
A deliberate contract step carries `[ContractMigration("why")]`, whose reason names the release that stopped reading the
old shape ([ADR-0184](../../docs/adr/0184-a-migration-only-expands-the-schema-unless-it-is-marked-a-contract-step.md)).
Raw SQL is not checked, so review a data change by hand.

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

## Container image

The `Dockerfile` builds the production image from the repository root. Its base images are pinned by tag and digest, and
Dependabot moves the digests
([ADR-0249](../../docs/adr/0249-the-host-images-base-images-are-pinned-by-digest-and-tracked-by-dependabot.md)); the
Node image's major matches the single pin in `src/frontend/.node-version`, which the `Host image` workflow checks:

```bash
docker build --file src/NCode.Identity.OpenId.Host/Dockerfile --tag ncode-identity-host .
```

The image runs on a chiseled ASP.NET Core base with no shell. It runs as the non-root `app` user (UID 1654), listens on
port **8080** over plain HTTP, and serves the `apps/auth` login bundle from `/app/auth`. TLS terminates at the ingress.
Probe `/health/live` for liveness and `/health/ready` for readiness. The image writes one JSON object per line to
standard output, with a UTC timestamp, so a log collector keeps an entry and its exception in one record; set
`Logging__Console__FormatterName=simple` to write text
([ADR-0248](../../docs/adr/0248-the-host-image-writes-one-line-json-logs.md)). CI builds it for `linux/amd64` and
`linux/arm64`
([ADR-0178](../../docs/adr/0178-the-host-image-is-chiseled-non-root-multi-arch-and-carries-the-login-bundle.md)).

The image starts in the `Production` environment, which refuses to start until the deployment provides:

- `AllowedHosts`: the public host names. A missing value or `*` is refused.
- `DeveloperKeys__Mode=None`: every tenant gets persisted signing keys instead of developer keys.
- `SecretProtection__RootKeyProvider`: a real root key provider (not `ephemeral`) with its settings, such as
  `SecretProtection__RootKeyProviders__Local__KeyPath` for `local`, and either
  `SecretProtection__Recovery__PublicKeyPath` or `SecretProtection__Recovery__Disabled=true`, which acknowledges running
  without a recovery key
  ([ADR-0235](../../docs/adr/0235-stored-secrets-are-protected-by-a-key-hierarchy-under-an-external-root-key.md)).
- `ConnectionStrings__openiddb`: the Postgres database. It also stores the key ring, so the host refuses to start
  without it.
- `OpenId__TenantResolution__StaticSingle__Issuer`: the public issuer URL, or another tenant resolution mode. A served
  tenant without an issuer fails closed
  ([ADR-0165](../../docs/adr/0165-every-served-tenant-has-an-issuer-and-its-strategy-supplies-the-default.md)).

The host does not migrate on boot. The image also carries the migration worker, which runs from `/app/migrations` with
`dotnet NCode.Identity.OpenId.Host.MigrationService.dll` and the same `ConnectionStrings__openiddb`.

The Helm chart in [`charts/ncode-identity-host`](../../charts/ncode-identity-host/README.md) deploys the image with
these settings and refuses a release that lacks them.
