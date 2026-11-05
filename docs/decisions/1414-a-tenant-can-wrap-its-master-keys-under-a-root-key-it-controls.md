# 1414. A tenant can wrap its master keys under a root key it controls

- **Status:** Accepted (amended by
  [ADR-1422](1422-a-management-problem-that-asks-the-caller-to-wait-carries-the-wait-in-retry-after-seconds.md): its 409
  and 503 problems also carry the wait in retryAfterSeconds)
- **Date:** 2026-11-05
- **Deciders:** NCode Group
- **Amends:** [ADR-1235](1235-stored-secrets-are-protected-by-a-key-hierarchy-under-an-external-root-key.md), decision 1
  (an owner other than the platform may name its own root key, which wraps its master keys in place of the deployment
  root), decision 2 (such a master key has no recovery copy), decision 9 (revoking or destroying the owner's key shreds
  its data wherever a copy is) and decision 13 (an owner-rooted cache entry is unwrapped again every validation interval
  and refused while its key is unavailable);
  [ADR-1407](1407-a-master-key-rotates-its-data-keys-are-rewrapped-in-the-background-and-it-is-destroyed-once-nothing-names-it.md)
  (rotation creates the successor under the owner's root key, the status reports the root key, the reason
  `root_key_changed` exists, and the ring re-validates);
  [ADR-1408](1408-an-offline-host-command-rewraps-every-master-key-under-the-configured-root-and-recovery-keys.md) (the
  rewrap engine wraps each row under its owner's root, skips a row under an owner root key, never adds a recovery copy to
  it, unwraps it through its own key with a recovery private key, and also serves the online conformance);
  [ADR-1260](1260-azure-key-vault-and-google-cloud-kms-are-root-key-providers.md), decision 8 (the chart mounts a
  provider's credentials when it is the root or an owner root key provider)

## Context

A tenant that must hold the key to its own data, such as a regulated customer, needs its master keys wrapped by a key it
can revoke, with no operator recovery path. The server reaches that key with its own workload identity. A revocation has
to take effect within a bounded period across replicas without a KMS call per request.

## Decision

1. **The tenant's key is an owner root key, a record of its own.** One `PersistedOwnerRootKey` row per secret owner lives
   beside the master keys (table `OwnerRootKeys`). It is set and removed through `IOwnerRootKeyManager` and the endpoints
   `PUT` and `DELETE /api/tenants/{tenantId}/encryption-keys/root-key`. It is not a setting of the settings cascade: the
   cascade validates synchronously and value by value, while a root key is accepted only after an asynchronous round trip
   through its provider; changing the key is an operation (rotate, then rewrap every master key row); a platform-scope value
   or a security profile must never move a tenant onto a key; the record carries operational state that does not belong in
   a settings document; and the secrets layer must not depend on the OpenID settings model. Which owners can bring a key is
   decided by the registered `IOwnerRootKeyPolicy` implementations, and an owner that any of them refuses cannot set one.
   The `platform` owner cannot bring
   a key, because its master keys protect the deployment's Data Protection key ring and the host's own data. The control-plane tenant cannot bring a key
   either, because the management API authenticates against it ([ADR-1191](1191-the-management-api-authenticates-against-the-control-plane-tenant.md)),
   so a revoked key would lock out the administrators who restore it; `PUT` on it answers `400`. Each rule is a registered
   policy (`PlatformOwnerRootKeyPolicy`, `ControlPlaneTenantRootKeyPolicy`), and a consumer refuses further owners by registering
   its own `IOwnerRootKeyPolicy`. The code says "owner root key"; the
   management API, the console and the docs say "tenant root key".
2. **The provider is any registered referenced key wrapper** ([ADR-1415](1415-a-key-wrapper-can-wrap-under-a-key-the-caller-names.md)),
   and the operator opts in per provider with `SecretProtection:OwnerRootKeys:Providers`, which is empty by default,
   because the server calls the tenant's KMS with the deployment's own identity. `ValidationInterval` (default one hour,
   between one minute and one day) and `RetryInterval` (default one minute, between one second and `ValidationInterval`)
   are validated when the host starts. The allowed list governs `PUT` only: an owner bound to a provider that leaves the
   list keeps working, because its data stays readable. A listed provider that is not a registered referenced key
   wrapper stops the host when the key wrapper catalog is composed, which the server does as it starts.
3. **The record holds a state, never material.** `State` is `active`, `unavailable` or `removing`; an unknown state reads
   as `unavailable`. The record also holds the key wrapper name, the key reference, when it was set, when its state last
   changed, the last successful validation, the last failure and its description. A master key row names the reference it
   is wrapped under in `RootKeyReference`, null for the deployment root.
4. **A master key under an owner root key has no recovery copy.** `MasterKeyFactory` wraps the material through
   `IReferencedKeyWrapper.WrapAsync(reference, ...)` and stores no recovery wrap whatever the recovery options say. Every
   master key row of an owner whose root key is `active` or `unavailable` names the key's wrapper and reference and has no
   recovery copy. The root context is the same key-id-and-owner binding, so a tenant key never unwraps another owner's
   rows.
5. **Setting or changing the key rotates.** `IOwnerRootKeyManager.SetAsync` checks the rotation spacing, probes the key,
   stores the binding as `active`, evicts the owner and notifies the observers, rotates the owner's master key so the
   successor is created under the tenant key, and rewraps every retired row under it with the rewrap engine. The earlier
   material exists in backups under the deployment root and its recovery copy, so only material created under the tenant
   key gives the tenant sole control of every value written or rewrapped afterwards. A rotation that loses a race or comes
   too soon is deferred to the validation service.
6. **Removing the key does not rotate.** `RemoveAsync` sets the state to `removing`, which wraps new master keys under the
   deployment root, rewraps every row under it with a recovery copy when one is configured, and deletes the record once
   every row is moved. Rows that cannot be unwrapped keep the record in `removing` and the validation service retries.
7. **A probe validates the key.** The server wraps a random 32-byte value through the tenant's key under its own context
   label, unwraps it, compares the two in constant time and zeroes both. It works for any provider and proves the encrypt
   and the decrypt permission. Any failure, transient or definitive, fails closed.
8. **The rewrap engine wraps each row under its owner's root.** `MasterKeyRewrapRequest.NonconformingOnly` limits a run
   to rows that do not conform to their owner's root. A row that conforms to an owner root key is examined only, and a dry
   run still unwraps and verifies it. With a recovery private key, a row with a `RootKeyReference` unwraps through its own
   wrapper. A recovery-only run removes the recovery copy a row under an owner root key carries.
9. **Audit.** `root_key.set`, `root_key.removed`, `root_key.unavailable` and `root_key.restored` carry the provider, the
   reference, the state and the failure, never material. A rotation onto the key records `master_key.rotated` with the
   reason `root_key_changed`. The management API records the events of its calls; the validation service records the
   events of its own with the system actor.
10. **Re-validation runs on one replica and the ring checks the rest.** `OwnerRootKeyValidationService` takes a lease
    every `RetryInterval` and validates the keys that come due: an `active` key every `ValidationInterval`, an
    `unavailable` or `removing` key every `RetryInterval`. Every state change calls `IOwnerRootKeyChangeObserver`, whose
    OpenId.Core implementation sends the tenant-changed command, which evicts the tenant's keys on every replica. The ring
    unwraps an owner-rooted master key again every `ValidationInterval` without reusing cached material, refuses an
    `unavailable` owner, and refuses an owner whose load failed for `RetryInterval`. A revocation therefore takes effect
    within `ValidationInterval` on every replica, whatever the shared state.
11. **The server fails closed.** `OwnerRootKeyUnavailableException` does not derive from `CryptographicException`, so
    development pruning never deletes a tenant-keyed owner's master keys. The management API answers `503` with
    `Retry-After`; a protocol endpoint answers as for any unhandled failure.
12. **The management API.** `GET` reports the root key (provider, reference, state, times, failure) and the allowed
    providers. `PUT` answers `200`, or `400` for a field that is blank, not allowed, not registered or malformed, `409`
    with `Retry-After` when the owner rotated within twice the refresh interval, `412` on a lost race and `422` when the
    probe fails. `DELETE` answers `204`, or `202` while master keys remain under the tenant key. `POST .../validate`
    answers `200`, or `404` when no root key is set. The console's tenant encryption keys tab offers set, validate and
    remove.
13. **The chart.** `secretProtection.ownerRootKeys.providers` renders `SecretProtection__OwnerRootKeys__Providers__<i>`,
    and a provider's workload credentials are mounted when it is the root provider or is listed there.

## Options considered

- **A setting in the settings cascade.** Rejected for the reasons in decision 1.
- **Rewrapping the existing master keys without rotating.** Their material is in backups under the deployment root and the
  recovery key, so values written later would stay readable to whoever holds such a backup.
- **Keeping an operator recovery copy.** The tenant's control is the point.
- **A provider-specific "describe key" check.** It is provider-specific and proves neither permission; the probe proves
  both through any provider.
- **Treating a transient failure as available.** The server fails closed; an unavailable key is retried every
  `RetryInterval` and restored at its first success.
- **Checking the tenant's key on every request.** Latency, cost and the tenant's KMS quotas rule it out.
- **Every registered wrapper as a choice.** `local` and `ephemeral` are deployment secrets, and the operator opts in per
  provider because the deployment's identity makes the calls.
- **Destroying the master keys when the key becomes unavailable.** The tenant may restore its key, and the KMS already
  shreds.
- **A bring-your-own key for the platform owner.** Its master keys protect the deployment's Data Protection key ring and the
  host's own data.

## Consequences

- Data under a tenant-keyed master key is unreadable once the tenant revokes or destroys its key, in the live database and
  in every backup.
- A backup taken before the key is set keeps the earlier master keys under the deployment root and their recovery copies,
  so the values in it stay readable to whoever holds that backup and the deployment root until it expires. Values written
  or rewrapped after the set never exist under a key the operator controls.
- The tenant's availability depends on its KMS and on the grants it gives the deployment's identity.
- No operator can recover a tenant-keyed owner.
- The cost is one probe (two KMS calls) per owner root key per `ValidationInterval` by one replica, and one unwrap per cached
  master key per replica per `ValidationInterval`.
- A tenant administrator may name any key the deployment's identity can use. The owner-bound context keeps that key from
  unwrapping another owner's rows, so an operator grants its identity a tenant's key only through that tenant's own key
  policy.

## References

- [ADR-1235](1235-stored-secrets-are-protected-by-a-key-hierarchy-under-an-external-root-key.md),
  [ADR-1260](1260-azure-key-vault-and-google-cloud-kms-are-root-key-providers.md),
  [ADR-1279](1279-key-wrapping-is-a-secrets-abstraction-shared-by-the-root-and-recovery-wrappers.md),
  [ADR-1407](1407-a-master-key-rotates-its-data-keys-are-rewrapped-in-the-background-and-it-is-destroyed-once-nothing-names-it.md),
  [ADR-1408](1408-an-offline-host-command-rewraps-every-master-key-under-the-configured-root-and-recovery-keys.md),
  [ADR-1415](1415-a-key-wrapper-can-wrap-under-a-key-the-caller-names.md),
  [ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md),
  [ADR-1351](1351-a-committed-change-is-applied-and-announced-on-its-own-bounded-token.md)
- `src/NCode.Identity.Secrets.Persistence/Protection/DefaultOwnerRootKeyManager.cs`,
  `src/NCode.Identity.Secrets.Persistence/Protection/MasterKeyFactory.cs`,
  `src/NCode.Identity.Secrets.Persistence/Protection/DefaultMasterKeyRing.cs`,
  `src/NCode.Identity.OpenId.Core/SecretProtection/OwnerRootKeyValidationService.cs`
- Issue [#174](https://github.com/NCodeGroup/Identity/issues/174), Auth0 "Bring Your Own Key"
