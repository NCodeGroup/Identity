# 1414. A tenant can wrap its master keys under a root key it controls

- **Status:** Accepted
- **Date:** 2026-11-05
- **Deciders:** NCode Group

## Context

A tenant that must hold the key to its own data, such as a regulated customer, needs its master keys wrapped by a key it
can revoke, with no operator recovery path. The key hierarchy of
[ADR-1235](1235-stored-secrets-and-grant-payloads-are-protected-by-a-key-hierarchy-under-an-external-root-key.md) wraps
every master key under the deployment's root key, so a tenant's key must be a second kind of root.

A key wrapper is bound to the key its options name; a tenant's key is chosen at run time. Unwrapping addresses a key
through `WrappedKey.KeyId` alone, independent of configuration.

The tenant's key often lives where the deployment's own workload identity has no grant. Each provider's cross-account
pattern is delegation: the tenant grants access to an identity it controls, and the caller acts through it without a
stored secret (AWS `sts:AssumeRole` with an `ExternalId`, Google Cloud service account impersonation, a multi-tenant
Microsoft Entra application the tenant consents to).

A revocation must take effect across replicas within a bounded period, without a KMS call per request.

## Decision

An owner other than the platform may bring its own root key, a key it controls in a key management service. The key
wraps the owner's master keys in place of the deployment root, with no recovery copy, and revoking or destroying the key
makes the owner's data unreadable wherever a copy is. The code says "owner root key"; the management API, the console
and the docs say "tenant root key".

1. **The tenant's key is an owner root key, a record of its own.** One `PersistedOwnerRootKey` row per secret owner
   lives in the table `OwnerRootKeys`, set and removed through `IOwnerRootKeyManager` and `PUT` and
   `DELETE /api/tenants/{tenantId}/encryption-keys/root-key`. It is not a setting of the cascade: a root key is accepted
   only after an asynchronous probe through its provider, changing it is an operation (rotate, then rewrap), a platform
   value or security profile must never move a tenant onto a key, the record carries operational state, and the secrets
   layer must not depend on the OpenID settings model.
2. **Registered policies decide who may bring a key.** `PlatformOwnerRootKeyPolicy` refuses the `platform` owner, whose
   master keys protect the Data Protection key ring and the host's own data. `ControlPlaneTenantRootKeyPolicy` refuses
   the control-plane tenant, which authenticates the management API
   ([ADR-1191](1191-a-management-request-is-authenticated-by-the-control-plane-tenant-and-scoped-by-its-routes-tenant.md)),
   because a revoked key would lock out its administrators. `PUT` on either answers `400`. A consumer refuses further
   owners by registering its own `IOwnerRootKeyPolicy`.
3. **The provider is any registered referenced key wrapper, and the operator opts in per provider.**
   `SecretProtection:OwnerRootKeys:Providers` is empty by default. `ValidationInterval` (default one hour, between one
   minute and one day) and `RetryInterval` (default one minute, between one second and `ValidationInterval`) are
   validated at start. The list governs `PUT` only: an owner bound to a provider that leaves it keeps working. A listed
   provider that is not a registered referenced wrapper stops the host. The chart renders
   `secretProtection.ownerRootKeys.providers` as `SecretProtection__OwnerRootKeys__Providers__<i>` and mounts a
   provider's credentials when it is the root provider or is listed.
4. **`IReferencedKeyWrapper : IKeyWrapper` wraps under a named key.** It lives in `NCode.Identity.Secrets.Abstractions`,
   namespace `NCode.Identity.Secrets.KeyWrapping`, and has four members:
   - `TryValidateKeyReference` checks the syntax of a reference only and returns a phrase that follows the field name
     when it fails;
   - `TryValidateAccessIdentity(string, out string? failure)` is the same syntax check for an access identity;
   - `WrapAsync(ReferencedKey, key, context, cancellationToken)` wraps under the named key with the same binding, checks
     and error mapping as the configured wrap, and throws `ArgumentException` for a reference that fails the syntax
     check;
   - `UnwrapAsync(WrappedKey, KeyAccess, context, destination, cancellationToken)` unwraps through the access the row
     records.

   `ReferencedKey(string KeyReference, KeyAccess? Access)` and `KeyAccess(string Identity, string Binding)` are records
   in the same namespace. A `null` access is the deployment's own identity and is `IKeyWrapper.UnwrapAsync`. The reported
   `WrappedKey.KeyId` is the identifier that addresses the key without configuration.
5. **Three adapters implement it, and process and deployment secrets do not.** `AwsKmsKeyWrapper` takes a KMS key or
   alias ARN (`AwsKmsKeyArn.IsValid`), `GoogleCloudKmsKeyWrapper` a crypto key name without a version
   (`GoogleCloudKmsKeyName.IsCryptoKeyName`; a foreign version the service reports throws `IOException`) and
   `AzureKeyVaultKeyWrapper` a key id (`AzureKeyVaultKeyId.IsKeyId`; a foreign version throws
   `InvalidOperationException`, and the 3072-bit floor applies). Each adapter's configured and referenced `WrapAsync`
   call one private `WrapUnderAsync`, so the two cannot drift. `EphemeralKeyWrapper`, `LocalKeyWrapper` and
   `RecoveryKeyWrapper` hold a process or deployment secret, never a key a third party controls, so they do not
   implement it. `IKeyWrapperCatalog.TryGetReferenced` and `GetReferenced` return the registered wrapper that implements
   the interface; `GetReferenced`, finding none, names the registered referenced wrappers.
6. **The tenant names an access identity, and the server never stores a secret.** It is an AWS role ARN, a Google Cloud
   service account email or a Microsoft Entra directory GUID, checked by `TryValidateAccessIdentity`. No access key,
   client secret or key file exists in configuration or the database. A key without one is reached with the deployment's
   own identity.
7. **The server derives an access binding from the owner.** The binding is `SecretOwner.Normalize().ToString()`, such as
   `tenant:acme`. The AWS adapter sends it as the `ExternalId` of the assume-role call, so a role another tenant trusts
   cannot be assumed on this tenant's behalf; the other adapters ignore it. `GET` reports it as `rootKeyAccessBinding`,
   so a tenant writes its trust policy before it sets the key.
8. **Each provider reaches the key through its own delegation, cached in `KeyAccessClientCache<TClient>`.** AWS uses an
   `AssumeRoleAWSCredentials` over the default credential chain with the session name `ncode-identity-root-key`; Google
   Cloud impersonates the service account with the KMS scopes; Azure uses a `DefaultAzureCredential` per directory, for
   the deployment's multi-tenant application consented in the tenant's directory. The cache, in
   `NCode.Identity.Secrets.Abstractions`, holds one client per key and access: a failed creation is not cached, the
   first successful call confirms and keeps an entry, a call that fails before any succeeded removes it, and an entry
   unused for one day expires. So an identity that never works leaves nothing behind, and a transient failure does not
   churn a working one. Each adapter's client factory runs its access-bound calls through the cache's `UseAsync` from
   its `ExecuteAsync`; a third-party referenced wrapper uses the same cache.
9. **The record holds a state and the access, never material.** `State` is one of the `OwnerRootKeyStates` `active`,
   `unavailable` or `removing`; an unknown state reads as `unavailable`. The record also holds the wrapper name, the key
   reference, `AccessIdentity` (nullable, at most 1024 characters), the times it was set, changed state and last
   validated, and the last failure.
10. **Each master key row names its root.** A row records its `RootKeyReference` and `RootKeyAccessIdentity`, null for
    the deployment root, because the row drives unwrapping. A row conforms to its owner's root only when its wrapper,
    reference and access identity match.
11. **A master key under an owner root key has no recovery copy.** `MasterKeyFactory` wraps through
    `IReferencedKeyWrapper.WrapAsync(ReferencedKey, ...)` and stores no recovery wrap whatever the recovery options say.
    The root context is the same key-id-and-owner binding, so a tenant key never unwraps another owner's rows.
12. **Setting or changing the key rotates.** `IOwnerRootKeyManager.SetAsync(owner, OwnerRootKeyRequest, cancellationToken)`
    takes the provider, reference and access identity. It checks the rotation spacing, probes the key, stores it
    `active`, marks the tenant's secrets changed
    ([ADR-1416](1416-a-replica-that-may-have-missed-announcements-evicts-only-teams-and-tenants-whose-row-changed.md)),
    notifies the observers, rotates with the reason `root_key_changed` so the successor is created under the tenant key,
    and rewraps retired rows with the engine of
    [ADR-1408](1408-an-offline-host-command-rewraps-every-master-key-under-the-configured-root-and-recovery-keys.md).
    Earlier material survives in backups under the deployment root, so only material created under the tenant key gives
    the tenant sole control. A rotation that loses a race or comes too soon is deferred to the validation service.
13. **A change of the access identity alone rewraps without rotating.** When only the access identity differs, the
    manager skips the rotation spacing check, probes the key through it, stores it and rewraps the nonconforming rows:
    the key is the same and no trust boundary is crossed. A change of the provider or the reference rotates.
    `IOwnerRootKeyManager` also exposes `TryValidateAccessIdentity` and `GetAccessBinding`.
14. **Removing the key does not rotate.** `RemoveAsync` sets `removing`, which wraps new master keys under the deployment
    root, rewraps every row under it with a recovery copy when one is configured, and deletes the record once every row
    moved. Rows that cannot be unwrapped keep it in `removing` and the validation service retries.
15. **A probe validates the key.** The server wraps and unwraps a random 32-byte value under the owner context through the
    key and access identity, compares in constant time and zeroes both. Any failure fails closed.
16. **The rewrap engine wraps each row under its owner's root.** The manager moves only rows that do not conform to
    their owner's root, with the engine of
    [ADR-1408](1408-an-offline-host-command-rewraps-every-master-key-under-the-configured-root-and-recovery-keys.md).
17. **Re-validation runs on one replica and the ring checks the rest.** `OwnerRootKeyValidationService` takes a lease
    every `RetryInterval` and validates an `active` key every `ValidationInterval`, an `unavailable` or `removing` key
    every `RetryInterval`. Every state change calls `IOwnerRootKeyChangeObserver`, whose OpenId.Core implementation
    sends the tenant-changed command, evicting the tenant's keys on every replica. The ring unwraps an owner-rooted
    master key again every `ValidationInterval` without cached material, refuses an `unavailable` owner and one whose
    load failed for `RetryInterval`, so a revocation takes effect within `ValidationInterval` on every replica.
18. **The server fails closed.** `OwnerRootKeyUnavailableException` does not derive from `CryptographicException`, so
    development pruning never deletes a tenant-keyed owner's master keys. `OwnerRootKeyUnavailableEndpointFilter` on the
    management route group answers `503` with `Retry-After` and `retryAfterSeconds`
    ([ADR-1483](1483-every-management-error-is-a-problem-document-that-its-route-declares.md)); a protocol endpoint
    answers `temporarily_unavailable` with `503` and `Retry-After` through `OwnerRootKeyUnavailableExceptionTranslator`
    ([ADR-1428](1428-a-package-translates-the-exceptions-it-owns-into-protocol-errors-through-registered-translators.md)).
19. **Audit.** `root_key.set`, `.removed`, `.unavailable` and `.restored` carry the provider, reference, `access_identity`,
    state and failure, never material; a rotation records `master_key.rotated` with the reason `root_key_changed`, and
    `master_key.*` events carry `root_key_access_identity`. The validation service records its events with the system
    actor.
20. **The management API.** `GET .../encryption-keys` reports the root key (provider, reference, access identity, state,
    times, failure), `rootKeyAccessBinding`, the allowed providers and each master key's `rootKeyAccessIdentity`. `PUT`
    accepts `accessIdentity` and answers `200`, `400` (field blank, not allowed, not registered or malformed), `409` with
    `Retry-After` (rotated within twice the refresh interval), `412` (lost race) or `422` (probe failed). `DELETE`
    answers `204`, or `202` while master keys remain under the tenant key. `POST .../root-key/validate` answers `200`,
    or `404` with no root key. The console's tenant encryption keys tab offers set, validate and remove.

## Options considered

- **A setting in the settings cascade.** Acceptance needs an asynchronous probe, and profiles must never move a tenant
  onto a key.
- **Rewrapping existing master keys without rotating when the key changes.** Their material is in backups under the
  deployment root, so whoever holds such a backup could read later values.
- **An operator recovery copy.** The tenant's control is the point.
- **A provider-specific "describe key" check.** It proves neither permission; the probe proves both through any
  provider.
- **Treating a transient failure as available, or checking the key on every request.** The server fails closed, and
  latency, cost and KMS quotas rule out per-request checks.
- **Every registered wrapper as a choice.** `local` and `ephemeral` are deployment secrets.
- **Destroying master keys when the key is unavailable.** The tenant may restore it, and the KMS already shreds.
- **A bring-your-own key for the platform owner.** Its master keys protect the host's own data.
- **A reference parameter on `IKeyWrapper.WrapAsync`, a wrapper per reference, or a marker interface.** The first
  forces wrappers that give it no meaning to throw, the second duplicates client caches, and the third has no members
  (as [ADR-1260](1260-a-key-wrapper-is-one-open-contract-and-each-key-management-service-has-one-adapter-package.md)
  concludes).
- **Stored credentials, the identity only on the owner record, or a random external ID.** A stored secret can leak; a
  row under an earlier identity could not be read mid-rewrap; a random ID must be shown anyway, while the owner-derived
  binding cannot be chosen by another tenant.

Chosen: an owner root key record set through a referenced key wrapper, reached through an identity the tenant grants,
rotating on every change of key.

## Consequences

- Data under a tenant-keyed master key is unreadable once the tenant revokes or destroys its key, in the live database
  and in every later backup. Older backups stay under the deployment root, and no operator recovery exists.
- The tenant's availability depends on its KMS and on the grants it gives.
- A third-party wrapper becomes a candidate by implementing the four members.
- The cost is one probe (two KMS calls) per owner root key per `ValidationInterval` by one replica, and one unwrap per
  cached master key per replica per `ValidationInterval`.
- A tenant administrator may name any key the deployment's identity can use, and with Google Cloud or Azure any
  identity another tenant granted to the deployment, because only AWS checks the binding; the owner-bound context keeps
  either from unwrapping another owner's rows, so an operator grants its identity a tenant's key only through that
  tenant's own key policy.
- Changing only the access identity creates no master key material.

## References

- [ADR-1235](1235-stored-secrets-and-grant-payloads-are-protected-by-a-key-hierarchy-under-an-external-root-key.md),
  [ADR-1260](1260-a-key-wrapper-is-one-open-contract-and-each-key-management-service-has-one-adapter-package.md),
  [ADR-1407](1407-a-master-key-rotates-its-data-keys-are-rewrapped-and-it-is-destroyed-once-unnamed.md),
  [ADR-1408](1408-an-offline-host-command-rewraps-every-master-key-under-the-configured-root-and-recovery-keys.md),
  [ADR-1416](1416-a-replica-that-may-have-missed-announcements-evicts-only-teams-and-tenants-whose-row-changed.md),
  [ADR-1428](1428-a-package-translates-the-exceptions-it-owns-into-protocol-errors-through-registered-translators.md),
  [ADR-1483](1483-every-management-error-is-a-problem-document-that-its-route-declares.md),
  [ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md),
  [ADR-1351](1351-work-that-follows-a-committed-change-runs-on-its-own-bounded-token.md)
- `src/NCode.Identity.Secrets.Persistence/Protection/DefaultOwnerRootKeyManager.cs`,
  `src/NCode.Identity.Secrets.Persistence/Protection/MasterKeyFactory.cs`,
  `src/NCode.Identity.OpenId.Core/SecretProtection/OwnerRootKeyValidationService.cs`,
  `src/NCode.Identity.Secrets.Abstractions/KeyWrapping/IReferencedKeyWrapper.cs`,
  `src/NCode.Identity.Secrets.AwsKms/AwsKmsKeyWrapper.cs`
- Auth0 "Bring Your Own Key"
