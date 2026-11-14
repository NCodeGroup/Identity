# 1370. A host runs the deployment posture unless configured for local development, and always names its root key provider

- **Status:** Accepted
- **Date:** 2026-11-02
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0119](project-template/0119-production-refuses-development-only-settings.md)
- **Supersedes:** project-template ADR-0119's decision 5, and decision 3's condition that the host environment is Production

## Context

[project-template ADR-0119](project-template/0119-production-refuses-development-only-settings.md) has a development
convenience do nothing unless configured, run through the production code path, be refused by one startup check, and
keep committed secrets out of the repository. It runs that check only in the Production environment. The family
supersedes that condition: an environment name is a deploy-time label that operators reuse freely (`Development`
clusters, `Conformance` deployments), so the strict posture keys on an explicit local-development switch and never on
the name. The family's development conveniences are developer signing keys, the developer CORS policy, the developer
interactive seed and the conformance seed. They are held to these forces:

- A convenience that replaces the tenant factory intercepts the tenant's key read. What a local-development host runs
  is then not what a deployment runs, and the runtime, the JWKS endpoint and the management API disagree about which
  keys exist. An in-memory key also regenerates on every restart, differs across replicas and invalidates the tokens
  issued before.
- A library that carries one host's identifiers, such as dev-server origins, client ids and redirect URIs, as defaults
  cannot be configured to drop them. The configuration binder appends configured entries to a collection that already
  holds some, so configuration can extend such a default but never replace it
  (issue [#98](https://github.com/NCodeGroup/identity/issues/98)).
- Seeded clients are stored in the database, so they outlive the process that created them. A seed that runs in every
  host persists clients in hosts that never asked for them, such as the Conformance environment, which drives an
  entirely different set of clients.
- Two places that decide whether a host is local can disagree about what a deployment refuses. A name-based default
  beside a setting is two sources of truth.
- A root key provider that falls back to `ephemeral` starts a host with a root key that is lost on restart, which makes
  every stored secret unreadable, and only a deployment check would refuse it.
- A launch profile that commits a bootstrap secret or an administrator password puts a development credential in the
  repository.

## Decision

**A host runs the deployment posture in every host environment unless the explicit, fail-closed switch
`LocalDevelopment:Enabled` is set.** Every development convenience does nothing unless its configuration section is
present, and `AddDeploymentSafety` refuses each section in every host that does not set the switch. No host creates or
intercepts keys in memory, no root key provider is a default, and no development credential is committed.

### One explicit switch decides

- **The switch.** `LocalDevelopmentOptions` in `NCode.Identity.AspNetCore` binds the `LocalDevelopment` section
  strictly and validates it when the host starts. `Enabled` is `false` by default, so an absent section is the strict
  posture. `AddLocalDevelopmentOptions(configuration)` registers it, and `IsLocalDevelopment()` reads it for the choices
  a host makes while it registers services.
- **Fail-closed in both directions.** `LocalDevelopmentOptionsValidator` fails the start when `Enabled` is `true` and the
  host environment is `Production`, which catches a leaked variable. It is the one place that reads an environment
  name, and it only tightens.
- **The name never relaxes anything.** Development without the switch, Staging, Conformance-named deployments and
  custom names all run the strict posture.
- **What it relaxes, and nothing else.**
  - The deployment gate and the Kestrel transport check are skipped
    ([ADR-1504](1504-the-hosts-serve-https-only-and-verify-their-certificate-at-startup.md)).
  - No HSTS header is sent, and the HTTPS redirect is added
    ([ADR-1447](1447-each-host-composes-forwarded-header-trust-and-strict-transport-security-from-one-general-package.md)).
  - Setup is served over plain HTTP, only from a loopback remote address. A null remote address is not loopback
    ([ADR-1512](1512-an-instance-is-set-up-once-through-a-one-time-setup-token-that-creates-a-root-global-administrator.md)).
  - The management console accepts a wildcard or empty `AllowedHosts`, and BFF and console validation accept `http`
    URLs.
  - The Data Protection key-ring gate revokes a key it cannot decrypt
    ([ADR-1247](1247-the-platform-master-key-protects-the-data-protection-ring-and-an-unreadable-key-stops-the-host.md)).
  - The local-root-key warning is not logged.
  - The bootstrap-token OpenAPI example and the API-reference secret pre-fill are served
    ([ADR-1481](1481-the-composition-layer-serves-the-api-reference-only-where-a-host-enables-it-and-each-management-problem-says-when-it-occurs.md)).
- **Who sets it.** The repository's local profiles set it in configuration they own: `appsettings.Development.json` of
  both reference hosts and `appsettings.Conformance.json` of the OpenID host, whose overlay runs an `ephemeral` root
  key and `environment_type: testing`. The chart never sets it, and its `environment` value stays `Production`.
  Neither reference host publishes its `appsettings.Development.json` or `appsettings.Conformance.json`, so an image
  run under any environment name loads no overlay that sets the switch. A test host sets it in its own in-memory
  configuration when it needs the relaxations, and a test of the strict posture runs a host that does not set it.

### Inert unless configured

- `AddDevelopmentEnvironment(configuration)` has no environment check. Each convenience it composes registers nothing
  unless its section is present: `DeveloperKeys`, `Cors`, `DeveloperInteractiveSeed` and the conformance seed.
- `AddDeploymentSafety` is the one place that decides what a deployment refuses. In a host without the switch it
  refuses every such section in one report that names each section.

### Keys come from the production path

- Every host signs with the persisted keys that tenant provisioning creates through the store
  ([ADR-1169](1169-every-tenant-is-provisioned-with-a-persisted-key-for-each-configured-algorithm-it-uses.md)), read back through the production path. The
  runtime, the JWKS endpoint and the management API's tenant secrets endpoints show the same keys.
- A test host stays hermetic through its own in-memory database and `ephemeral` root key. Tests provision RS256, PS256
  and ES256 keys per host.
- `DeveloperKeys:PruneUnreadableKeys=true` registers `AddDeveloperKeyPruning()`. `DeveloperSecretPruningSeedHandler`
  runs first on the `SeedTenantCommand` path and removes a tenant secret that cannot be read, and the tenant master
  keys that cannot be unwrapped. Key provisioning then replaces them. It also heals the platform master keys
  ([ADR-1247](1247-the-platform-master-key-protects-the-data-protection-ring-and-an-unreadable-key-stops-the-host.md)).
  An absent section registers nothing.
- The host's launch profiles set `DeveloperKeys__PruneUnreadableKeys=true`, so a debugging session survives a change of
  the root key. The chart sets no `DeveloperKeys` value.

### CORS and seeded clients are declared by the host

- `AddDeveloperCors(configuration)` registers nothing without a `Cors` section. The host's Development overlay lists
  `https://localhost:5174`, `https://localhost:5175`, `http://localhost:4173` and `https://localhost:4173`, and its
  Conformance overlay lists `https://localhost:5175`.
- The `Cors` section binds strictly into `DeveloperCorsOptions` and is validated when the host starts.
  `DeveloperCorsOptionsValidator` requires each `AllowedOrigins` entry to be an origin as a browser sends it in its
  `Origin` header: an `http` or `https` scheme and a host with an optional port, without user information, path,
  query, fragment or trailing slash. An entry that can never match a request stops the host with an error that names
  its `Cors:AllowedOrigins:<index>` entry.
- `DeveloperInteractiveSeedOptions.SpaClients` and `BffClients` start empty, and the seeder registers exactly what
  configuration declares. With nothing configured it registers none, which is the shape of the conformance seed's
  `Clients`. A host's identifiers are not a library default: the host's environment overlay declares the entries, and
  the collection rule of project-template ADR-0119 applies.
- `appsettings.Development.json` declares the SPA client `playground-spa` beside the `Frontend` section that publishes
  that id, and the BFF client `mgmt-console`, whose JWKS the AppHost supplies. One file holds both halves of each
  surface's sign-in coordinates.
- `DeveloperSpaClientSeedingTests` proves that every client id the host publishes is seeded and that each seeded client
  resolves at the authorization endpoint with each of its redirect URIs, so the two halves cannot drift apart.
- The seeder is idempotent. It never changes a stored SPA client, and it reconciles a stored BFF client with its
  configuration, replacing one that differs together with its grants, codes and tokens.

### A root key provider is always named

- No root key provider is a default. `SecretProtectionRegistration` validates on start that
  `SecretProtection:RootKeyProvider` is not blank, so a host that forgets it stops before it serves.
  `DefaultKeyWrapperCatalog.GetConfigured` throws, naming the setting, instead of choosing a provider, so a library
  caller that composes the secret services without the server fails on its first wrap.
- `ephemeral` is an explicit opt-in. Only `appsettings.Development.json`, `appsettings.Conformance.json` and tests
  select it. A host without `LocalDevelopment:Enabled` refuses both `ephemeral` and an unset provider, and the Helm
  chart refuses an empty `secretProtection.rootKeyProvider` in every environment.

### Development credentials are generated per machine

- No development credential is committed. The LocalDev AppHost generates once per machine, under
  `LocalApplicationData/NCode.Identity/LocalDev` (`DevelopmentSecretFile`, `DevelopmentKeyFile` and
  `ConformanceSeedSecrets`), the `root-key`, `bootstrap-admin-secret`, `admin-password`, `mgmt-client.pem`,
  `mgmt-dpop.pem` and the conformance seed secrets. It hands them to the hosts as environment variables with the
  `local` root key provider.
- A host run on its own reads them from its user secrets, and without them seeds neither administrator.

## Options considered

- Keep in-memory ephemeral keys as an explicit opt-in that intercepts the tenant's key read. Rejected: the keys
  regenerate on restart, differ across replicas and diverge from what the management API shows, so what a
  local-development host runs is not what a deployment runs.
- Ship built-in seed clients or CORS origins in the library. Rejected: a library then carries one host's identifiers,
  configuration can extend but never replace them, and a host that never asked for them persists the clients.
- Keep the built-in clients and add an opt-out switch. Rejected: it adds public surface to preserve a default that
  belongs to one host.
- Keep an environment check in `AddDevelopmentEnvironment`. Rejected: two places then decide what a deployment refuses.
- Key the posture on the host environment's name. Rejected: an environment name is a deploy-time label that operators
  reuse freely, and a name-based default beside a setting is two sources of truth. An explicit setting fails closed
  and opts in to the relaxations deliberately.
- Keep a fallback root key provider in a host that is not local. Rejected: a Staging or self-hosted deployment then
  loses every secret on restart, silently.
- Refuse `ephemeral` only by environment name. Rejected: a deployment under any name then starts with a root key that
  is lost on restart. Tests and the conformance profile run with throwaway databases and set the switch.
- Have the seeder generate and print the bootstrap secret. Rejected: a printed secret lands in logs, and both OP
  instances that share the database must agree on it.
- Do nothing unless configured, run through the production path, and name the provider. Chosen.

## Consequences

- A host without `LocalDevelopment:Enabled` runs the deployment posture under every environment name, and the
  Production environment refuses the switch.
- A host without `SecretProtection:RootKeyProvider` stops with an error naming it.
- A deployment that carries a `DeveloperKeys`, `Cors`, `DeveloperInteractiveSeed` or conformance seed
  section, or selects `ephemeral`, fails to start, and the report names each offender.
- A host that wants a frontend surface to sign in declares that surface's client under
  `DeveloperInteractiveSeed:SpaClients`. The reference host does, so F5, the Aspire stack and the integration tests
  agree.
- Tests that compose the secret services select `ephemeral`.
- Developers read the generated credentials from their files; `build/dev-db-reset.ps1` purges the rest of a
  development database.

## References

- [`DeveloperKeysRegistration.cs`](../../src/NCode.Identity.Server/DevelopmentEnvironment/DeveloperKeysRegistration.cs),
  [`DeveloperCorsRegistration.cs`](../../src/NCode.Identity.Server/DevelopmentEnvironment/DeveloperCorsRegistration.cs),
  [`DevelopmentEnvironmentRegistration.cs`](../../src/NCode.Identity.Server/DevelopmentEnvironment/DevelopmentEnvironmentRegistration.cs)
  and [`DeploymentSafetyRegistration.cs`](../../src/NCode.Identity.Server/DeploymentSafety/DeploymentSafetyRegistration.cs).
- [`LocalDevelopmentOptions.cs`](../../src/NCode.Identity.AspNetCore/Options/LocalDevelopmentOptions.cs),
  [`LocalDevelopmentOptionsValidator.cs`](../../src/NCode.Identity.AspNetCore/Options/LocalDevelopmentOptionsValidator.cs)
  and [`LocalDevelopmentRegistration.cs`](../../src/NCode.Identity.AspNetCore/LocalDevelopmentRegistration.cs).
- [`SecretProtectionRegistration.cs`](../../src/NCode.Identity.Server/SecretProtection/SecretProtectionRegistration.cs),
  [`DefaultKeyWrapperCatalog.cs`](../../src/NCode.Identity.Secrets.Persistence/Protection/DefaultKeyWrapperCatalog.cs)
  and [`DevelopmentSecretFile.cs`](../../hosts/NCode.Identity.OpenId.LocalDev/DevelopmentSecrets/DevelopmentSecretFile.cs).
- `hosts/NCode.Identity.OpenId.Host/appsettings.Development.json` and
  `tests/NCode.Identity.OpenId.Integration.Tests/Endpoints/DeveloperSpaClientSeedingTests.cs`.
- [`NCode.Identity.OpenId.Host.csproj`](../../hosts/NCode.Identity.OpenId.Host/NCode.Identity.OpenId.Host.csproj) and
  [`NCode.Identity.ManagementConsole.Host.csproj`](../../hosts/NCode.Identity.ManagementConsole.Host/NCode.Identity.ManagementConsole.Host.csproj),
  which keep the local-development overlays out of the published output.
- [ADR-1169](1169-every-tenant-is-provisioned-with-a-persisted-key-for-each-configured-algorithm-it-uses.md),
  [ADR-1247](1247-the-platform-master-key-protects-the-data-protection-ring-and-an-unreadable-key-stops-the-host.md),
  [ADR-1447](1447-each-host-composes-forwarded-header-trust-and-strict-transport-security-from-one-general-package.md),
  [ADR-1504](1504-the-hosts-serve-https-only-and-verify-their-certificate-at-startup.md) and
  [ADR-1512](1512-an-instance-is-set-up-once-through-a-one-time-setup-token-that-creates-a-root-global-administrator.md).
- [NCodeGroup/identity#98](https://github.com/NCodeGroup/identity/issues/98),
  [#441](https://github.com/NCodeGroup/identity/issues/441),
  [#495](https://github.com/NCodeGroup/identity/issues/495) and [#496](https://github.com/NCodeGroup/identity/issues/496).
