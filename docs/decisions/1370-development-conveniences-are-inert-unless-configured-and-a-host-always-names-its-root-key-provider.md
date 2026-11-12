# 1370. Development conveniences are inert unless configured, and a host always names its root key provider

- **Status:** Accepted
- **Date:** 2026-11-02
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0119](project-template/0119-production-refuses-development-only-settings.md)

## Context

[project-template ADR-0119](project-template/0119-production-refuses-development-only-settings.md) has a development
convenience do nothing unless configured, run through the production code path, be refused by one Production check, and
keep committed secrets out of the repository. The family's development conveniences are developer signing keys, the
developer CORS policy, the developer interactive seed and the conformance seed. They are held to these forces:

- A convenience that replaces the tenant factory intercepts the tenant's key read. What a Development host runs is then
  not what a Production host runs, and the runtime, the JWKS endpoint and the management API disagree about which keys
  exist. An in-memory key also regenerates on every restart, differs across replicas and invalidates the tokens issued
  before.
- A library that carries one host's identifiers, such as dev-server origins, client ids and redirect URIs, as defaults
  cannot be configured to drop them. The configuration binder appends configured entries to a collection that already
  holds some, so configuration can extend such a default but never replace it
  (issue [#98](https://github.com/NCodeGroup/identity/issues/98)).
- Seeded clients are stored in the database, so they outlive the process that created them. A seed that runs in every
  non-Production host persists clients in hosts that never asked for them, such as the Conformance environment, which
  drives an entirely different set of clients.
- Two places that decide the environment can disagree about what Production refuses.
- A root key provider that falls back to `ephemeral` starts a host with a root key that is lost on restart, which makes
  every stored secret unreadable, and only Production would refuse it.
- A launch profile that commits a bootstrap secret or an administrator password puts a development credential in the
  repository.

## Decision

Every development convenience does nothing unless its configuration section is present, and `AddProductionSafety`
refuses each section in Production. No host creates or intercepts keys in memory, no root key provider is a default,
and no development credential is committed.

### Inert unless configured

- `AddDevelopmentEnvironment(configuration)` has no environment check. Each convenience it composes registers nothing
  unless its section is present: `DeveloperKeys`, `Cors`, `DeveloperInteractiveSeed` and the conformance seed.
- `AddProductionSafety` is the one place that decides the environment. In Production it refuses every such section in
  one report that names each section.

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
  `https://localhost:5174` and `https://localhost:5175`, and its Conformance overlay lists `https://localhost:5175`.
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
  select it. Production refuses both `ephemeral` and an unset provider, and the Helm chart refuses an empty
  `secretProtection.rootKeyProvider` in every environment.

### Development credentials are generated per machine

- No development credential is committed. The LocalDev AppHost generates once per machine, under
  `LocalApplicationData/NCode.Identity/LocalDev` (`DevelopmentSecretFile`, `DevelopmentKeyFile` and
  `ConformanceSeedSecrets`), the `root-key`, `bootstrap-admin-secret`, `admin-password`, `mgmt-client.pem`,
  `mgmt-dpop.pem` and the conformance seed secrets. It hands them to the hosts as environment variables with the
  `local` root key provider.
- A host run on its own reads them from its user secrets, and without them seeds neither administrator.

## Options considered

- Keep in-memory ephemeral keys as an explicit opt-in that intercepts the tenant's key read. Rejected: the keys
  regenerate on restart, differ across replicas and diverge from what the management API shows, so what Development
  runs is not what Production runs.
- Ship built-in seed clients or CORS origins in the library. Rejected: a library then carries one host's identifiers,
  configuration can extend but never replace them, and a host that never asked for them persists the clients.
- Keep the built-in clients and add an opt-out switch. Rejected: it adds public surface to preserve a default that
  belongs to one host.
- Keep an environment check in `AddDevelopmentEnvironment`. Rejected: two places then decide what Production refuses.
- Keep a fallback root key provider outside Production. Rejected: a Staging or self-hosted deployment then loses every
  secret on restart, silently.
- Refuse `ephemeral` outside Development. Rejected: tests and the conformance profile run elsewhere with throwaway
  databases, and project-template ADR-0119 checks only Production.
- Have the seeder generate and print the bootstrap secret. Rejected: a printed secret lands in logs, and both OP
  instances that share the database must agree on it.
- Do nothing unless configured, run through the production path, and name the provider. Chosen.

## Consequences

- A host without `SecretProtection:RootKeyProvider` stops with an error naming it.
- A Production deployment that carries a `DeveloperKeys`, `Cors`, `DeveloperInteractiveSeed` or conformance seed
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
  and [`ProductionSafetyRegistration.cs`](../../src/NCode.Identity.Server/ProductionSafety/ProductionSafetyRegistration.cs).
- [`SecretProtectionRegistration.cs`](../../src/NCode.Identity.Server/SecretProtection/SecretProtectionRegistration.cs),
  [`DefaultKeyWrapperCatalog.cs`](../../src/NCode.Identity.Secrets.Persistence/Protection/DefaultKeyWrapperCatalog.cs)
  and [`DevelopmentSecretFile.cs`](../../hosts/NCode.Identity.OpenId.LocalDev/DevelopmentSecrets/DevelopmentSecretFile.cs).
- `hosts/NCode.Identity.OpenId.Host/appsettings.Development.json` and
  `tests/NCode.Identity.OpenId.Integration.Tests/Endpoints/DeveloperSpaClientSeedingTests.cs`.
- [ADR-1169](1169-every-tenant-is-provisioned-with-a-persisted-key-for-each-configured-algorithm-it-uses.md) and
  [ADR-1247](1247-the-platform-master-key-protects-the-data-protection-ring-and-an-unreadable-key-stops-the-host.md).
- [NCodeGroup/identity#98](https://github.com/NCodeGroup/identity/issues/98),
  [#441](https://github.com/NCodeGroup/identity/issues/441),
  [#495](https://github.com/NCodeGroup/identity/issues/495) and [#496](https://github.com/NCodeGroup/identity/issues/496).
