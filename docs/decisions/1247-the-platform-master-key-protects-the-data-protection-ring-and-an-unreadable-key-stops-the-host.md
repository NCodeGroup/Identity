# 1247. The platform master key protects the Data Protection ring, and an unreadable key stops the host

- **Status:** Accepted
- **Date:** 2026-11-05
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0127](project-template/0127-the-data-protection-key-ring-is-shared-and-encrypted-at-rest.md)

## Context

[project-template ADR-0127](project-template/0127-the-data-protection-key-ring-is-shared-and-encrypted-at-rest.md)
holds the generic rules for the Data Protection key ring: it is shared by every replica, encrypted at rest, and checked
when the host starts, so that outside Development a live key the host cannot decrypt stops it with the key ids to
revoke rather than letting Data Protection write a default key of its own. This record holds the family's names for
those rules and the master key that implements them.

The platform and each tenant own master keys, which are rows of the database wrapped by the root key
([ADR-1235](1235-stored-secrets-and-grant-payloads-are-protected-by-a-key-hierarchy-under-an-external-root-key.md)). The platform is the
deployment root and a team groups a customer's tenants
([ADR-1411](1411-the-platform-is-the-deployment-root-a-team-groups-tenants-and-tenants-share-one-database.md)); the platform is
`SecretOwner.Platform` (owner type `platform`, id `platform`), and a team has no master key. The Data Protection key
ring belongs to the host as a whole, so no tenant can own it, and no stored secret of the family depends on it.

If the platform's master key rows are lost and recreated while the key ring survives, such as after restoring part of a
backup, the platform master key cannot decrypt the keys of the ring. A developer who changes the local root key meets
the same failure: the platform's master keys cannot be unwrapped, the warmup stops the host, and the database must be
reset by hand although the platform holds no stored secret of the family.

## Decision

**The platform master key protects the Data Protection key ring and the application data a host protects under
`SecretOwner.Platform`; the host refuses to start while the ring holds a live key that the platform master key cannot
decrypt; and `DataProtectionKeyRing:RevokedKeyIds` revokes keys by ID so that an operator can recover.**

1. **Scope of the platform master key.** It protects the Data Protection key ring and the application data a host
   protects under `SecretOwner.Platform`, and no stored secret of the family. A tenant's secrets never depend on it.
2. **`AddSecretProtection` wires Data Protection.** It calls `AddDataProtection()`, so the `IKeyManager` that the gate
   needs always resolves. It sets `KeyManagementOptions.XmlEncryptor` to `MasterKeyXmlEncryptor` over the platform
   protector, and registers `TimeProvider.System` unless the host registered a time provider.
3. **Production refuses an unprotected ring.** `AddProductionSafety` validates `KeyManagementOptions` on start, refusing
   a `null` `XmlRepository` (`KeyRingRepositoryMissingMessage`) and a `null` `XmlEncryptor`
   (`KeyRingUnprotectedMessage`) before the server binds a port. The reference host's branch without a connection
   string therefore cannot start in Production.
4. **The gate is a hosted service that starts after the warmup.** `AddSecretProtection` inserts
   `PlatformSecretProtectorWarmup` at hosted-service index 0 and `DataProtectionKeyRingGate` at index 1, ahead of the
   hosted service of Data Protection, which loads the key ring. A startup validator cannot hold the check: every
   startup validator runs before any hosted service starts, so before the warmup has loaded the protector that decrypts
   the keys.
5. **The gate ignores dead keys and revokes listed ones without decrypting.** A key that is revoked or expired is
   ignored, however unreadable it is. A live key listed in `DataProtectionKeyRingOptions.RevokedKeyIds` (section
   `DataProtectionKeyRing`, an array of `Guid`, so an environment variable names each ID as
   `DataProtectionKeyRing__RevokedKeyIds__0`, `__1`, and so on) is revoked without decrypting it, with the reason
   `listed in DataProtectionKeyRing:RevokedKeyIds`. The options bind strictly and are validated on start
   ([ADR-1137](1137-configuration-binds-strictly-and-every-option-timer-period-and-retention-is-validated-at-startup.md)).
   Revoking is idempotent: the revocation is stored in the key ring, so the entries can stay or be removed once the host
   has started.
6. **Any other live key is read.** The gate reads its `Descriptor`, which decrypts the key material and is where a
   foreign master key fails. When the host environment is not Development, it logs `KeyRingKeyUndecryptable` as an
   error and throws one `InvalidOperationException` that names every such key and the setting that revokes them. In
   Development it revokes the key and logs `KeyRingKeyRevoked` as a warning, so a developer's host starts over a
   database whose key ring an earlier run wrote.
7. **Test hosts keep their ring in their own store.** A host that starts reads the machine's key directory unless it
   has a repository, and the gate would then judge keys that an earlier run wrote under another master key. Each
   `HostApplicationFactory` uses its own in-memory `DeveloperDataProtectionDbContext`, and a bare test host uses an
   in-memory `XmlRepository`.
8. **Rotation and destruction follow ADR-1407.** The platform master key rotates and is destroyed under the rules of
   [ADR-1407](1407-a-master-key-rotates-its-data-keys-are-rewrapped-and-it-is-destroyed-once-unnamed.md):
   new ring keys and host data use the new version. An existing ring key is never re-encrypted
   (`DataProtectionKeyRingValueSource.RewrapAsync` rewraps nothing), so a retired version stays while a live ring key
   names it, and the warmup loads it. A host that protects its own data under the platform registers a protected-value
   source for it. The maintenance lease is `master-key-maintenance:platform`. `GET /api/platform/encryption-keys` and
   `POST /api/platform/encryption-keys/rekey` use the `platform_encryption_keys` scope family, authorized at the
   platform node
   ([ADR-1424](1424-a-management-route-authorizes-at-the-node-it-names-before-it-reads-through-replaceable-validators.md)).
9. **Developer key pruning starts the platform over.** Only with `DeveloperKeys:PruneUnreadableKeys=true`
   (`AddDeveloperKeyPruning()`, never an `IsDevelopment()` check, as
   [ADR-1370](1370-development-conveniences-are-inert-unless-configured-and-a-host-always-names-its-root-key-provider.md)
   decides), `DeveloperPlatformMasterKeyPruning`, an `IHostedLifecycleService`, runs in `StartingAsync`, before every
   `StartAsync`. It calls `GetProtectorAsync(SecretOwner.Platform, [], …)` and acts only on a `CryptographicException`.
   `DeveloperMasterKeyReset` then removes the platform's master keys in their own committed unit of work and evicts the
   owner. The heal calls `IKeyManager.RevokeAllKeys` and logs `DeveloperPlatformMasterKeysReplaced`, and the warmup
   creates a new key. Without the opt-in an unwrappable platform key always stops the host.

## Options considered

- **`ValidateOnStart` for the gate.** It runs before the warmup loads the protector, so it cannot decrypt a key.
  Rejected.
- **Revoke undecryptable keys in every environment.** It hides a lost master key and silently ends the sessions the
  keys protected. Rejected; only Development revokes.
- **A revocation endpoint.** Recovery is an operator's configuration change at start, not a runtime API that could
  revoke the ring of a running host. Rejected.
- **A team master key.** The ring belongs to the host as a whole, which the platform owns. Rejected.
- **Secrets at the root.** The root key only wraps master keys; it never protects a ring or data directly. Rejected.
- **An `IsDevelopment()` heal.** The environment name is not an opt-in, and a development convenience runs through the
  production path. Rejected.
- **Ordering the heal by hosted-service index.** An `IHostedLifecycleService` runs in `StartingAsync` before every
  `StartAsync`, which holds regardless of registration order. Rejected.
- **The chosen design.** The platform master key protects the ring, a gate stops the host on an unreadable live key, and
  a listed ID revokes it.

## Consequences

- A deployment that lost its master keys stops until an operator restores them or lists the IDs to revoke; the chart
  README, "A key the master key cannot decrypt", describes the recovery.
- Revoked keys end the cookies and anti-forgery tokens they protected.
- A production host never creates a default key of its own, and a ring without a repository or an encryptor never
  reaches a port.
- A developer who changes the root key restarts with a new platform master key, without resetting the database, only
  when the opt-in is set.
- The gate, the options, the warmup order, the heal and the test hosts' own stores are covered by tests of the secret
  protection and host start-up.

## References

- [project-template ADR-0127](project-template/0127-the-data-protection-key-ring-is-shared-and-encrypted-at-rest.md)
  — the generic ring rules
- [ADR-1235](1235-stored-secrets-and-grant-payloads-are-protected-by-a-key-hierarchy-under-an-external-root-key.md) — the key hierarchy
- [ADR-1411](1411-the-platform-is-the-deployment-root-a-team-groups-tenants-and-tenants-share-one-database.md) — the platform and the
  team
- [ADR-1407](1407-a-master-key-rotates-its-data-keys-are-rewrapped-and-it-is-destroyed-once-unnamed.md)
  — master key rotation and destruction
- [ADR-1370](1370-development-conveniences-are-inert-unless-configured-and-a-host-always-names-its-root-key-provider.md) —
  development conveniences
- `src/NCode.Identity.Server/SecretProtection/` — `DataProtectionKeyRingGate`, `DataProtectionKeyRingOptions`,
  `PlatformSecretProtectorWarmup`, `MasterKeyXmlEncryptor`
- `charts/ncode-identity-host/README.md` — "A key the master key cannot decrypt"
