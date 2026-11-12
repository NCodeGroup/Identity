# 1313. Development conveniences are inert unless configured and run through the production path

- **Status:** Accepted (amended by
  [ADR-1247](1247-the-platform-master-key-protects-the-data-protection-ring-and-an-unreadable-key-stops-the-host.md): key
  pruning also starts the platform over with a new master key)
- **Date:** 2026-11-01
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0119](project-template/0119-production-refuses-development-only-settings.md)
- **Supersedes:** [ADR-1002](1002-ephemeral-development-keys.md)
- **Amends:** [ADR-1021](1021-developer-signing-keys-seeded-through-persistence.md),
  [ADR-1139](1139-setting-names-are-a-closed-vocabulary.md),
  [ADR-1146](1146-a-development-seeder-registers-only-the-clients-its-configuration-declares.md),
  [ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md),
  [ADR-1169](1169-every-tenant-is-provisioned-with-persisted-keys.md) and
  [ADR-1248](1248-the-host-and-console-images-are-chiseled-non-root-multi-arch-digest-pinned-and-log-one-line-json.md)

## Context

[project-template ADR-0119](project-template/0119-production-refuses-development-only-settings.md) has a development
convenience do nothing unless configured, run through the production code path, and be refused by one Production check.
The family's conveniences differed in four ways:

- [ADR-1002](1002-ephemeral-development-keys.md)'s ephemeral keys replaced the tenant factory, so they intercepted the
  tenant's key read, and what a Development host ran was not what a Production host ran.
- An unset `DeveloperKeys:Mode` meant `Ephemeral`, so a Production deployment had to state `None`.
- The library carried the five dev-server origins of one host as the developer CORS defaults.
- `AddDevelopmentEnvironment` checked the environment itself, beside `AddProductionSafety`.

## Decision

Every development convenience does nothing unless its configuration section is present, and `AddProductionSafety`
refuses each section in Production.

- **There are no ephemeral keys.** `DeveloperKeyMode`, `AddDeveloperKeys(DeveloperKeyMode)`,
  `AddEphemeralDeveloperKeys()` and `EphemeralOpenIdTenantFactory` are removed. Every host signs with the persisted keys
  that tenant provisioning creates through the store ([ADR-1169](1169-every-tenant-is-provisioned-with-persisted-keys.md)).
  A test host stays hermetic through its own in-memory database and root key.
- **Key pruning is the opt-in.** `DeveloperKeys:PruneUnreadableKeys=true` registers the existing pruner through
  `AddDeveloperKeyPruning()`, which removes a key that can no longer be read when the tenant is seeded, and provisioning
  replaces it. An absent section registers nothing.
- **The developer CORS policy carries no origins.** `AddDeveloperCors(configuration)` registers nothing without a
  `Cors` section, and the host's Development overlay lists the origins.
- **One check decides the environment.** `AddDevelopmentEnvironment(configuration)` has no environment check.
  `AddProductionSafety` refuses a `DeveloperKeys`, `Cors`, `DeveloperInteractiveSeed` or conformance seed section in one
  report.

## Options considered

- Keep ephemeral keys as an explicit opt-in. Rejected: they intercept a read, so what Development runs is not what
  Production runs.
- Keep the CORS defaults in Development only. Rejected: a library then carries one host's identifiers.
- Keep the environment check in `AddDevelopmentEnvironment`. Rejected: two places then decide what Production refuses.

## Consequences

- Tests provision RS256, PS256 and ES256 keys per host.
- Launch profiles set `DeveloperKeys__PruneUnreadableKeys=true`, so a debugging session survives a change of the root
  key.
- The chart sets no `DeveloperKeys` value.
- A Production deployment that carries a `DeveloperKeys` section fails to start, and the report names the section.

## References

- [`DeveloperKeysRegistration.cs`](../../src/NCode.Identity.Server/DevelopmentEnvironment/DeveloperKeysRegistration.cs),
  [`DeveloperCorsRegistration.cs`](../../src/NCode.Identity.Server/DevelopmentEnvironment/DeveloperCorsRegistration.cs),
  [`DevelopmentEnvironmentRegistration.cs`](../../src/NCode.Identity.Server/DevelopmentEnvironment/DevelopmentEnvironmentRegistration.cs)
  and [`ProductionSafetyRegistration.cs`](../../src/NCode.Identity.Server/ProductionSafety/ProductionSafetyRegistration.cs).
- [NCodeGroup/identity#441](https://github.com/NCodeGroup/identity/issues/441).
