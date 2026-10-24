# 247. A Data Protection key the server cannot decrypt stops the host

- **Status:** Accepted
- **Date:** 2026-10-24
- **Deciders:** NCode Group

## Context

The Data Protection key ring is encrypted under the server's master key
([ADR-0235](0235-stored-secrets-are-protected-by-a-key-hierarchy-under-an-external-root-key.md)). The master key is a
row of the same database, wrapped by the root key. If the master key rows are lost and recreated while the key ring
survives, such as after restoring part of a backup, the server's master key cannot decrypt the keys of the ring.

Data Protection does not stop on such a ring. It treats the keys it cannot decrypt as ineligible, silently writes a
default key of its own, and carries on. What the old keys protected, the sign-in cookies and the anti-forgery tokens, is
unreadable, and the operator learns it from the users who are signed out, not from the host (backlog `R48`).

## Decision

**The host refuses to start while the Data Protection key ring holds a live key that the server's master key cannot
decrypt, and `DataProtectionKeyRing:RevokedKeyIds` revokes keys by ID so that an operator can recover.**

1. **The gate is a hosted service that starts after the warmup.** `DataProtectionKeyRingGate` is an `IHostedService`.
   `AddSecretProtection` inserts `ServerSecretProtectorWarmup` at index 0 and the gate at index 1, ahead of the hosted
   service of Data Protection, which loads the key ring. A startup validator cannot hold the check: `ValidateOnStart`
   and every startup validator run before any hosted service starts, so before the warmup has loaded the protector that
   decrypts the keys.
2. **A live key is not revoked and expires after the current time.** The gate reads the `KeyId`, `IsRevoked` and
   `ExpirationDate` of each key before it decrypts anything. A key that is revoked or has expired is ignored, however
   unreadable it is.
3. **Outside Development the host refuses to start.** For each live key that is not listed for revocation, the gate
   reads its `Descriptor`, which decrypts the key material and is where a foreign master key fails. It logs
   `KeyRingKeyUndecryptable` as an error for each key that fails, and then throws an `InvalidOperationException` that
   names every such key and the setting that revokes them.
4. **Development revokes and warns.** The gate revokes a live key that it cannot decrypt and logs `KeyRingKeyRevoked`
   with the reason as a warning. It is the heal that development secret pruning applies to the master keys of tenants
   after the root key changes (ADR-0235), and it lets a developer's host start over a database whose key ring an earlier
   run wrote.
5. **Revocation by ID is configuration.** `DataProtectionKeyRingOptions` is a public class in
   `NCode.Identity.Server.SecretProtection`, bound from the `DataProtectionKeyRing` section and validated when the host
   starts, as the other options of `AddSecretProtection` are
   ([ADR-0137](0137-configuration-binds-strictly-and-is-validated-at-startup.md)). Its `RevokedKeyIds` is an array of
   `Guid`, so an environment variable names each ID as `DataProtectionKeyRing__RevokedKeyIds__0`, `__1`, and so on. The
   gate revokes a live key that is listed, with the reason `listed in DataProtectionKeyRing:RevokedKeyIds`, without
   decrypting it. Revoking is idempotent: a key that is already revoked is skipped, because the revocation is stored in
   the key ring, so the entries can stay or be removed once the host has started. When no live key is left, Data
   Protection creates one as it starts.
6. **`AddSecretProtection` registers Data Protection.** It calls `AddDataProtection()`, so the `IKeyManager` that the
   gate needs always resolves, and it registers `TimeProvider.System` unless the host registered a time provider.
7. **Test hosts keep their key ring in their own store.** A host that starts reads the machine's key directory unless it
   has a repository, and the gate would then judge keys that an earlier run wrote under another master key. Each
   `HostApplicationFactory` keeps its key ring in its own in-memory `DeveloperDataProtectionDbContext` database, and a
   bare test host that starts sets an in-memory `XmlRepository`. The reference host's branch without a connection string
   registers no repository, so Production refuses to start there without a database.

## Options considered

- **`ValidateOnStart`.** Rejected: it runs before the protector loads, so it cannot decrypt a key.
- **Let Data Protection carry on.** Rejected: it writes a default key and logs a failure, and what the old keys
  protected is lost without the host saying so.
- **Revoke an undecryptable key in every environment.** Rejected: in Production it hides a lost master key, which the
  operator may be able to restore, and a revoked key never protects its data again.
- **A management API endpoint that revokes keys.** Rejected: the host must start to serve it, and the failure is that
  it does not start.

## Consequences

- A deployment that lost its master keys stops until an operator decides. The recovery is one setting and a restart,
  described in the
  [chart README](../../charts/ncode-identity-host/README.md#a-key-the-master-key-cannot-decrypt): restore the master
  keys, or list the IDs that the error names.
- A revoked key never decrypts what it protected, so the sign-in cookies and the anti-forgery tokens that it protected
  stop working.
- A key that has expired or been revoked is ignored however unreadable it is, so it never stops the host.
- A Development host without a database still keeps its key ring in the default key directory, where the gate revokes
  the keys of earlier runs.
- This implements backlog `R48`.

## References

- [ADR-0137](0137-configuration-binds-strictly-and-is-validated-at-startup.md) — options are bound strictly and
  validated at startup.
- [ADR-0235](0235-stored-secrets-are-protected-by-a-key-hierarchy-under-an-external-root-key.md) — the key ring is
  encrypted under the server's master key.
- [Chart README](../../charts/ncode-identity-host/README.md#a-key-the-master-key-cannot-decrypt) — the recovery.
- Code: `NCode.Identity.Server.SecretProtection.DataProtectionKeyRingGate`,
  `NCode.Identity.Server.SecretProtection.DataProtectionKeyRingOptions`,
  `NCode.Identity.Server.SecretProtection.SecretProtectionRegistration`,
  `NCode.Identity.Server.SecretProtection.ServerSecretProtectorWarmup`,
  `tests/NCode.Identity.OpenId.IntegrationTests/Infrastructure/HostApplicationFactory.cs`.
