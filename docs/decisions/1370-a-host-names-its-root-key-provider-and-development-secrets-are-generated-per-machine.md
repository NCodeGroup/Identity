# 1370. A host names its root key provider, and development secrets are generated per machine

- **Status:** Accepted
- **Date:** 2026-11-02
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0119](project-template/0119-production-refuses-development-only-settings.md)
- **Amends:** [ADR-1235](1235-stored-secrets-are-protected-by-a-key-hierarchy-under-an-external-root-key.md) and
  [ADR-1236](1236-a-password-is-judged-by-one-policy-wherever-it-is-set.md)

## Context

An unset `SecretProtection:RootKeyProvider` selected the `ephemeral` provider. A host that forgot the setting started
with a root key that is lost on restart, which made every stored secret unreadable, and only Production refused it.

[project-template ADR-0119](project-template/0119-production-refuses-development-only-settings.md) has a development
convenience do nothing unless configured and forbids committed secrets. The reference host's `launchSettings.json` held
`BootstrapAdmin__ClientSecret` and `DeveloperInteractiveSeed__AdminPassword`, so two development credentials were
committed to the repository.

## Decision

No root key provider is a default, and no development credential is committed.

- **No provider is a default.** `SecretProtectionRegistration` validates on start that `SecretProtection:RootKeyProvider`
  is not blank, so a host that forgets it stops before it serves, and `DefaultKeyWrapperCatalog.GetConfigured` throws
  instead of choosing `ephemeral`, so a library caller that composes the secret services without the server fails on its
  first wrap with the setting named.
- **`ephemeral` is an explicit opt-in.** Only the host's `appsettings.Development.json` and
  `appsettings.Conformance.json` select it, and `AddProductionSafety` refuses both `ephemeral` and an unset provider in
  Production. The Helm chart refuses an empty `secretProtection.rootKeyProvider` in every environment.
- **Development credentials are generated per machine.** The LocalDev AppHost generates `bootstrap-admin-secret` and
  `admin-password` once, beside `root-key` in `LocalApplicationData/NCode.Identity/LocalDev`, and passes them to the
  hosts it starts as environment variables. A host run on its own reads them from its user secrets, and without them
  seeds neither administrator.

## Options considered

- Keep the fallback outside Production. Rejected: a Staging or self-hosted deployment then loses every secret on
  restart, silently.
- Refuse `ephemeral` outside Development. Rejected: tests and the conformance profile run elsewhere with throwaway
  databases, and ADR-0119 checks only Production.
- Validate the provider name at startup. Not needed: the warmup, the first hosted service, already stops the host on an
  unknown name.
- Have the seeder generate and print the bootstrap secret. Rejected: a printed secret lands in logs, and both OP
  instances that share the database must agree on it.

## Consequences

- A host without the setting stops with an error naming it.
- Tests that compose the secret services select `ephemeral`.
- Developers read the generated credentials from their files.
- ADR-1236's consequence that the developer password is `dev-admin-password` no longer holds.

## References

- [`SecretProtectionRegistration.cs`](../../src/NCode.Identity.Server/SecretProtection/SecretProtectionRegistration.cs),
  [`DefaultKeyWrapperCatalog.cs`](../../src/NCode.Identity.Secrets.Persistence/Protection/DefaultKeyWrapperCatalog.cs),
  [`ProductionSafetyRegistration.cs`](../../src/NCode.Identity.Server/ProductionSafety/ProductionSafetyRegistration.cs)
  and [`DevelopmentSecretFile.cs`](../../hosts/NCode.Identity.OpenId.LocalDev/DevelopmentSecrets/DevelopmentSecretFile.cs).
- [NCodeGroup/identity#495](https://github.com/NCodeGroup/identity/issues/495) and
  [#496](https://github.com/NCodeGroup/identity/issues/496).
