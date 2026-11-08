# NCode.Identity.OpenId.Host

A runnable ASP.NET Core reference host for the OpenID Connect + OAuth 2.0 library family. This document covers the
**local database environment**, **schema management**, the **purge/reset procedure**, and the **container image**.

## Database provider

The dev database is **PostgreSQL**, orchestrated by the Aspire AppHost
([ADR-1073](../../docs/decisions/1073-postgres-dev-database-and-aspire-migration-worker.md)). Run the stack through the
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
database ([ADR-1235](../../docs/decisions/1235-stored-secrets-are-protected-by-a-key-hierarchy-under-an-external-root-key.md)).
A **Production** host refuses to start without a real root key, and a root key must never be committed. The
`SecretProtection` section selects it (environment variables use `__`, for example
`SecretProtection__RootKeyProvider=local`):

| Setting                                           | Meaning                                                                                            |
|---------------------------------------------------|----------------------------------------------------------------------------------------------------|
| `SecretProtection:RootKeyProvider`                | Required: `local`, `aws-kms`, `azure-key-vault`, `google-cloud-kms`, `ephemeral` or another name   |
| `SecretProtection:RootKeyProviders:Local:KeyPath` | File with a base64url 32-byte key (the `local` provider)                                           |
| `SecretProtection:RootKeyProviders:Local:Key`     | The same key inline: Development and tests only                                                    |
| `SecretProtection:Recovery:PublicKeyPath`         | Offline recovery RSA public key (PEM) that also wraps the keys                                     |
| `SecretProtection:Recovery:Disabled`              | `true` only to acknowledge running without a recovery key                                          |
| `SecretProtection:MasterKeys:RotationPeriod`      | How long an active master key lives before the background pass rotates it (default `365.00:00:00`) |
| `SecretProtection:MasterKeys:RefreshInterval`     | How long a replica keeps an owner's master keys in memory before it reads them again (`00:05:00`)  |
| `SecretProtection:MasterKeys:RetirementGrace`     | How long a retired master key is kept before it can be destroyed (default `30.00:00:00`)           |
| `SecretProtection:MasterKeys:MaintenanceInterval` | How often the background pass rotates, rewraps and destroys (default `1.00:00:00`)                 |
| `SecretProtection:MasterKeys:DestroyRetired`      | `false` keeps retired master keys for good (default `true`)                                        |

The setting is required: a host that leaves it unset stops when it starts, naming it. `ephemeral` keeps the root key in
memory, so it suits only development and tests, and Production refuses it
([ADR-1370](../../docs/decisions/1370-a-host-names-its-root-key-provider-and-development-secrets-are-generated-per-machine.md)).

Further providers ship as packages that register themselves by name. The reference host registers the three below, and
each stays inert until `SecretProtection:RootKeyProvider` selects it. `aws-kms` takes
`SecretProtection:RootKeyProviders:AwsKms:KeyArn`, a key or alias ARN, and uses the AWS SDK's default credential chain.
`azure-key-vault` takes `SecretProtection:RootKeyProviders:AzureKeyVault:KeyId`, the identifier of an RSA key of 3072
bits or more in Key Vault or Managed HSM, with or without a version. It uses Azure.Identity's `DefaultAzureCredential`.
`google-cloud-kms` takes `SecretProtection:RootKeyProviders:GoogleCloudKms:KeyName`, the resource name of a symmetric
crypto key (`projects/<project>/locations/<location>/keyRings/<ring>/cryptoKeys/<key>`). It uses Google Application
Default Credentials. None of them reads a credential from configuration. The
[Helm chart](../../charts/ncode-identity-host/README.md) wires these settings for a deployment.

**Local development.** Nothing to do: on first run the AppHost writes one development root key to
`LocalApplicationData/NCode.Identity/LocalDev/root-key` (owner-only on Unix) and hands it to both OP instances it
starts, the `api` and the conformance instance, which share the one database. That key lives outside the repository.
`build/dev-db-reset.ps1` resets the database and keeps the key; to replace the key, stop the AppHost, delete the file
and reset the database together. A host started without the AppHost selects `ephemeral` in
`appsettings.Development.json`, and in `appsettings.Conformance.json` for the `Conformance` profile, which suits its
in-memory database: its stored secrets do not survive a restart. The server never discards or replaces a key it cannot
unprotect: a tenant whose keys were protected under another root key is not served, and its error says that the key
that protected them is missing or has changed
([ADR-1169](../../docs/decisions/1169-every-tenant-is-provisioned-with-persisted-keys.md)). Restore the root key, or
reset the database with it.

### Remote signing keys

A tenant's signing keys can be held in a key management service that signs every token, so the private key never
leaves it. Each of the three provider packages holds both roles: it wraps the root key and holds the tenant's remote
signing keys. Each package's `Add<Vendor>RemoteKeyProviderIfConfigured` method registers its provider only when its
configuration section exists, and the reference host calls it, because registering a
provider selects it and validates its settings when the host starts. A tenant selects a registered provider through the
tenant setting `OpenId:Platform:Settings:tenant_signing_key_provider` (`aws-kms`, `azure-key-vault` or
`google-cloud-kms`).

| Setting                                                                | Meaning                                                                                  |
|------------------------------------------------------------------------|------------------------------------------------------------------------------------------|
| `SecretProtection:RemoteKeyProviders:AwsKms:Region`                    | AWS region in which keys are created, such as `us-west-2`                                |
| `SecretProtection:RemoteKeyProviders:AwsKms:PendingWindowInDays`       | Days, 7 to 30, that AWS KMS waits before it deletes a destroyed key (default `30`)       |
| `SecretProtection:RemoteKeyProviders:AzureKeyVault:VaultUri`           | URI of the Key Vault or Managed HSM, such as `https://contoso.vault.azure.net`           |
| `SecretProtection:RemoteKeyProviders:AzureKeyVault:HardwareProtected`  | `false` creates software-protected keys (default `true`)                                 |
| `SecretProtection:RemoteKeyProviders:GoogleCloudKms:KeyRing`           | Resource name of the key ring, `projects/<project>/locations/<location>/keyRings/<ring>` |
| `SecretProtection:RemoteKeyProviders:GoogleCloudKms:HardwareProtected` | `false` creates software-protected keys (default `true`)                                 |

Each provider uses its SDK's default credential chain, and no credential is read from configuration. The server creates
sign-only keys and destroys only the keys it created. The deployment's identity needs these permissions:

- AWS: `kms:CreateKey`, `kms:TagResource`, `kms:GetPublicKey`, `kms:Sign`, `kms:ScheduleKeyDeletion` and
  `kms:DescribeKey`.
- Azure: the `Key Vault Crypto Officer` role on the vault.
- Google: `cloudkms.cryptoKeys.create` and `cloudkms.cryptoKeyVersions.create`, `.get`, `.viewPublicKey`, `.useToSign`
  and `.destroy` on the key ring.

The management API creates a tenant's key in a registered provider with `POST /api/tenants/{tenantId}/secrets/remote`
and binds a key that a provider already holds, once it proves it signs, with
`POST /api/tenants/{tenantId}/secrets/remote/import`. See
[Remote signing keys](../../docs/capabilities/remote-signing-keys.md#management-api).

### Master keys

Each tenant and the platform have one active master key. It rotates to a new version after
`SecretProtection:MasterKeys:RotationPeriod` or when an administrator rekeys through the management API
(`POST /api/tenants/{tenantId}/encryption-keys/rekey`, `POST /api/platform/encryption-keys/rekey`), the old
version retires, and a tenant's values, and a platform value that a registered protected-value source reports, are
rewrapped under the new version in the background. The platform's next version protects the Data Protection keys and
host data created afterwards, and an existing Data Protection key is never re-encrypted. A retired key is destroyed
once its grace has passed and no stored secret, grant payload, live Data Protection key or reported platform value
names it, so the platform's retired version stays while a live Data Protection key names it
([ADR-1407](../../docs/decisions/1407-a-master-key-rotates-its-data-keys-are-rewrapped-in-the-background-and-it-is-destroyed-once-nothing-names-it.md)).

The host also runs one offline command from its own image, for a one-off Job (the Helm chart's
`secretProtection.rewrapJob` runs it): it rewraps every master key under the configured root key provider and recovery
public key, which moves the keys to a new root key provider or root key, and it replaces the recovery copies when the
recovery key pair changes
([ADR-1408](../../docs/decisions/1408-an-offline-host-command-rewraps-every-master-key-under-the-configured-root-and-recovery-keys.md)):

```sh
dotnet NCode.Identity.OpenId.Host.dll secret-protection rewrap [--recovery-private-key <file>] [--owner <type:id>] [--dry-run]
dotnet NCode.Identity.OpenId.Host.dll secret-protection rewrap-recovery [--recovery-private-key <file>] [--owner <type:id>] [--dry-run]
```

`rewrap` wraps each master key under the configured root key provider and, when a recovery public key is configured,
under it too. `rewrap-recovery` replaces only the recovery copies and needs a configured recovery public key. Each key
is unwrapped by the root key provider it names, which must stay registered and configured for the run, or from its
recovery copy when `--recovery-private-key` names the file of the recovery private key. The key is read from that file
and never from configuration, is held in memory that is zeroed when the command ends, and never reaches a serving
process. `--owner` limits the run to one owner such as `tenant:acme`, and `--dry-run` unwraps and verifies every key
without writing. The exit code is `0` when no key failed, `1` otherwise or when the configuration is invalid, and `2`
for a usage error. A run is repeatable.

### Development credentials

No development credential is committed
([ADR-1370](../../docs/decisions/1370-a-host-names-its-root-key-provider-and-development-secrets-are-generated-per-machine.md)).
On first run the AppHost writes `bootstrap-admin-secret` and `admin-password` beside `root-key` in
`LocalApplicationData/NCode.Identity/LocalDev` (`%LOCALAPPDATA%` on Windows, `~/.local/share` on Linux,
`~/Library/Application Support` on macOS) and hands them to the hosts. It also writes `mgmt-client.pem` and
`mgmt-dpop.pem`, the management console's client and DPoP keys, and hands the public key set of the client key to this
host's developer seed. Sign in to the management console at `https://localhost:5005` as `admin` with the text of
`admin-password`. The `bootstrap-admin` client authenticates with `client_secret_basic` and the text of
`bootstrap-admin-secret`, which the Development API reference prefills. Delete a file and restart the AppHost to get a
new value; the developer seed updates the console's registered key on that start when `mgmt-client.pem` is the file
deleted.

A host run without the AppHost has no credential in its launch profiles, so set them in its user secrets (loaded in
Development only):

```pwsh
$secret = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_')
$encoded = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($secret)).TrimEnd('=').Replace('+', '-').Replace('/', '_')
dotnet user-secrets set BootstrapAdmin:ClientSecret $encoded --project src/NCode.Identity.OpenId.Host
dotnet user-secrets set DeveloperInteractiveSeed:AdminPassword '<at least 15 characters>' --project src/NCode.Identity.OpenId.Host
```

`$secret` is the client secret to present. Without a value neither administrator is seeded. The `Conformance` profile
reads `BootstrapAdmin__ClientSecret` from the environment, because user secrets load only in Development.

The conformance seed's credentials are generated the same way ([#514](https://github.com/NCodeGroup/Identity/issues/514)):
`appsettings.Conformance.json` lists the conformance clients and the `conformance-user` test user without a secret or
password. The AppHost and
[`build/conformance/Get-ConformanceSeedCredentials.ps1`](../../build/conformance/Get-ConformanceSeedCredentials.ps1)
write `conformance-secret-client`, `conformance-secret-client2`, `conformance-secret-client-secret-post` and
`conformance-user-password` to the same directory and hand them to the conformance OP as `ConformanceSeed__` environment
variables; each client secret's file text is the secret the client presents, and the password is the file's text.
Without them the seed skips those clients and the test user. To start the `Conformance` profile by hand, set them in the
shell first:

```pwsh
./build/conformance/Get-ConformanceSeedCredentials.ps1 -SetEnvironment
dotnet run --project src/NCode.Identity.OpenId.Host --launch-profile Conformance
```

## Schema management (EF Core migrations)

The schema is managed by **EF Core migrations**, not `EnsureCreated()`
([ADR-1041](../../docs/decisions/1041-host-owned-ef-migrations-deliberate-outside-development.md)). The migrations live in the
host-owned **`NCode.Identity.OpenId.Host.Database`** project under `Migrations/OpenId` and
`Migrations/DeveloperDataProtection`; the published persistence library ships none.

- The **migration worker** applies pending migrations before the `api` starts — the `api` never applies schema itself.
- **Production** (any non-Development deployment) applies schema deliberately, once per release, before the rollout.
  The Helm chart runs the same worker from the host's image as a `pre-install` and `pre-upgrade` Job, and a failed
  migration stops the release before any pod changes
  ([ADR-1180](../../docs/decisions/1180-migrations-run-before-the-rollout-and-keep-the-previous-release-working.md)). Without the
  chart, run the worker the same way, or apply a generated idempotent script
  (`dotnet ef migrations script --idempotent`).

### Each context has one `InitialCreate` until the first stable release

Until the first stable release ([#535](https://github.com/NCodeGroup/Identity/issues/535)), each context has one
migration, `InitialCreate`, which creates its whole model and changes no data
([ADR-1394](../../docs/decisions/1394-until-the-first-stable-release-each-host-database-context-has-one-initial-migration.md)).
After changing the EF model, delete the context's migration, designer file and model snapshot, and scaffold
`InitialCreate` again (the `Database` project carries the design-time factories, so it is both `--project` and
`--startup-project`):

```pwsh
dotnet tool restore   # first time only (provides dotnet-ef)
dotnet ef migrations add InitialCreate `
  --project src/NCode.Identity.OpenId.Host.Database `
  --startup-project src/NCode.Identity.OpenId.Host.Database `
  --context OpenIdDbContext `
  --output-dir Migrations/OpenId
```

The Data Protection context scaffolds the same way with `--context DeveloperDataProtectionDbContext` and
`--output-dir Migrations/DeveloperDataProtection`. Strip the UTF-8 BOM the scaffolder writes from each generated file.
`InitialCreateTests` fails a context that holds any migration but `InitialCreate`, and a migration that changes data.
Renaming, retiring or narrowing a setting edits its descriptor and needs no migration: a stored settings document that
names an unregistered setting fails closed
([ADR-1396](../../docs/decisions/1396-until-the-first-stable-release-a-setting-is-renamed-removed-or-narrowed-outright.md)).
A re-scaffold gives `InitialCreate` a fresh identifier. Before it applies anything, the migration worker compares the
migration identifiers each context's history records with the migrations the `Database` project holds, and when the
history records one the project lacks it applies nothing and fails with a message that names the context and says to
reset the local database with `build/dev-db-reset.ps1` (below).

### The expand-only rule governs migrations from the first stable release

From the first stable release ([#535](https://github.com/NCodeGroup/Identity/issues/535)), each context's
`InitialCreate` is its baseline and every model change adds a migration, which only expands the schema unless it is
marked a contract step
([ADR-1184](../../docs/decisions/1184-a-migration-only-expands-the-schema-unless-it-is-marked-a-contract-step.md) and
[ADR-1250](../../docs/decisions/1250-a-migration-classifies-its-data-change-and-a-constraint-added-to-an-existing-table-contracts.md)).
The Definition of Done enforces the rule over every migration, the `InitialCreate` migrations included, which pass
unmarked. `ExpandOnlyRuleTests` runs `ExpandOnlyRule.Check` and fails a migration that drops or renames a table or
column; adds a required column without a default; makes a column required, removes the default of a required column,
changes its type, or shortens it or its precision; adds a unique index, unique constraint, primary key, check
constraint or foreign key to an existing table; or adds a foreign key that restricts deleting rows of an existing
table. A migration that runs raw SQL or changes seed data classifies the change with a marker:
`[ExpandMigration("why")]` when the code that reads the existing schema still reads every row it writes, and
`[ContractMigration("why")]` when it does not. A deliberate contract step, such as dropping a column or making one
required, carries `[ContractMigration("why")]`, whose reason says why nothing reads what it removes. A migration that
carries both markers fails.

The Definition of Done fails if the model has changes no migration captures (`dotnet ef migrations
has-pending-model-changes`, run for both contexts), so scaffold the migration before pushing.

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
([ADR-1249](../../docs/decisions/1249-the-host-images-base-images-are-pinned-by-digest-and-tracked-by-dependabot.md)); the
Node image's major matches the single pin in `src/frontend/.node-version`, which the `Host image` workflow checks:

```bash
docker build --file src/NCode.Identity.OpenId.Host/Dockerfile --tag ncode-identity-host .
```

The image runs on a chiseled ASP.NET Core base with no shell. It runs as the non-root `app` user (UID 1654), listens on
port **8080** over plain HTTP, and serves the `apps/auth` login bundle from `/app/auth`. TLS terminates at the ingress.
Probe `/health/live` for liveness and `/health/ready` for readiness. The image writes one JSON object per line to
standard output, with a UTC timestamp, so a log collector keeps an entry and its exception in one record; set
`Logging__Console__FormatterName=simple` to write text
([ADR-1248](../../docs/decisions/1248-the-host-image-writes-one-line-json-logs.md)). CI builds it for `linux/amd64` and
`linux/arm64`
([ADR-1178](../../docs/decisions/1178-the-host-image-is-chiseled-non-root-multi-arch-and-carries-the-login-bundle.md)).

The image starts in the `Production` environment, which refuses to start until the deployment provides:

- `AllowedHosts`: the public host names. A missing value or `*` is refused.
- `SecretProtection__RootKeyProvider`: a real root key provider (not `ephemeral`) with its settings, such as
  `SecretProtection__RootKeyProviders__Local__KeyPath` for `local`, and either
  `SecretProtection__Recovery__PublicKeyPath` or `SecretProtection__Recovery__Disabled=true`, which acknowledges running
  without a recovery key
  ([ADR-1235](../../docs/decisions/1235-stored-secrets-are-protected-by-a-key-hierarchy-under-an-external-root-key.md)).
- `ConnectionStrings__openiddb`: the Postgres database. It also stores the key ring, so the host refuses to start
  without it.
- `OpenId__TenantResolution__StaticSingle__Issuer`: the public issuer URL, or another tenant resolution mode. A served
  tenant without an issuer fails closed
  ([ADR-1165](../../docs/decisions/1165-every-served-tenant-has-an-issuer-and-its-strategy-supplies-the-default.md)).

The host does not migrate on boot. The image also carries the migration worker, which runs from `/app/migrations` with
`dotnet NCode.Identity.OpenId.Host.MigrationService.dll` and the same `ConnectionStrings__openiddb`.

The Helm chart in [`charts/ncode-identity-host`](../../charts/ncode-identity-host/README.md) deploys the image with
these settings and refuses a release that lacks them.
