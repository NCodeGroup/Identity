# 1423. Developer key pruning starts the platform over when its master keys cannot be unwrapped

- **Status:** Accepted
- **Date:** 2026-11-05
- **Deciders:** NCode Group
- **Amends:** [ADR-1313](1313-development-conveniences-are-inert-unless-configured-and-run-through-the-production-path.md),
  the key pruning opt-in (`DeveloperKeys:PruneUnreadableKeys` also starts the platform over with a new master key);
  [ADR-1413](1413-the-platform-master-key-protects-the-data-protection-key-ring-and-the-hosts-own-data.md),
  decision 4 (with developer key pruning a platform master key that cannot be unwrapped is replaced before the warmup
  loads the protector);
  [ADR-1247](1247-a-data-protection-key-the-server-cannot-decrypt-stops-the-host.md), decision 4 (developer key
  pruning revokes the ring keys of the platform master keys it replaces)

## Context

The platform's master keys are wrapped by the root key
([ADR-1413](1413-the-platform-master-key-protects-the-data-protection-key-ring-and-the-hosts-own-data.md)). A changed
local root key makes them unwrappable, and `PlatformSecretProtectorWarmup` stops the host when it cannot load the
platform's protector. The tenants' master keys fail for the same cause, and developer key pruning already heals them
when a tenant is seeded
([ADR-1313](1313-development-conveniences-are-inert-unless-configured-and-run-through-the-production-path.md)). A
developer who changes the root key still has to reset the database by hand before the host starts, although the
platform holds no stored secret of the family and every Data Protection key of the ring is encrypted under the platform
master keys that cannot be unwrapped.

## Decision

**With developer key pruning on, a host whose platform master keys cannot be unwrapped removes them, revokes the ring,
and starts the platform over with a new master key.**

1. **The heal belongs to the key pruning opt-in.** `DeveloperKeys:PruneUnreadableKeys=true` registers it through
   `AddDeveloperKeyPruning()`, never through an `IsDevelopment()` check, so one section decides the environment and
   Production refuses it with the rest of `DeveloperKeys`.
2. **It runs in `StartingAsync`.** `DeveloperPlatformMasterKeyPruning` is an `IHostedLifecycleService`, and the host
   runs `StartingAsync` before the `StartAsync` of every hosted service, so the heal precedes
   `PlatformSecretProtectorWarmup` whatever the registration order.
3. **It probes the platform's protector.** It calls `GetProtectorAsync(SecretOwner.Platform, [], …)` and acts only on
   a `CryptographicException`. Any other exception propagates and stops the host.
4. **`DeveloperMasterKeyReset` removes the keys and evicts the owner.** It removes every master key of the platform in
   its own committed unit of work and then evicts the platform from the protector provider, so the next protector
   starts the platform over with a new master key. The heal of a tenant's master keys uses the same type, so both keep
   the committed-unit-of-work-then-evict order.
5. **It revokes every Data Protection key.** Every key of the ring is encrypted under the master keys that were
   removed, so it calls `IKeyManager.RevokeAllKeys(now, reason)` after the reset, and then logs
   `DeveloperPlatformMasterKeysReplaced` as a warning. The heal is complete in any environment that registers it, the
   Conformance profiles included, and the warmup creates the new platform master key.

## Options considered

- **An `IsDevelopment()` check in the warmup.** Rejected: a second place decides the environment
  ([ADR-1313](1313-development-conveniences-are-inert-unless-configured-and-run-through-the-production-path.md)), and
  it would heal without the opt-in.
- **A hosted service placed before the warmup by index.** Rejected: it is correct for one registration order only.
- **Leave the host stopped until the database is reset.** Rejected: a developer then repeats a manual reset after every
  change of the root key, and the opt-in exists to avoid it.

## Consequences

- The sign-in cookies and anti-forgery tokens that were protected before the heal stop working.
- The host data that a host protects under the platform is unreadable after the heal.
- A host without the opt-in, Production included, never registers the heal, so a platform master key that cannot be
  unwrapped still stops the host.
- A tenant's secrets do not depend on the platform master keys, so the heal leaves them untouched.

## References

- [ADR-1313](1313-development-conveniences-are-inert-unless-configured-and-run-through-the-production-path.md),
  [ADR-1413](1413-the-platform-master-key-protects-the-data-protection-key-ring-and-the-hosts-own-data.md),
  [ADR-1247](1247-a-data-protection-key-the-server-cannot-decrypt-stops-the-host.md)
- `src/NCode.Identity.Server/DevelopmentEnvironment/DeveloperPlatformMasterKeyPruning.cs`,
  `src/NCode.Identity.Server/DevelopmentEnvironment/DeveloperMasterKeyReset.cs`
- Issue [#569](https://github.com/NCodeGroup/Identity/issues/569)
