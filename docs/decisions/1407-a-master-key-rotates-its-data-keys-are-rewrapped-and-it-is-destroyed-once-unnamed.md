# 1407. A master key rotates, its data keys are rewrapped, and it is destroyed once unnamed

- **Status:** Accepted
- **Date:** 2026-11-04
- **Deciders:** NCode Group

## Context

[ADR-1235](1235-stored-secrets-and-grant-payloads-are-protected-by-a-key-hierarchy-under-an-external-root-key.md)
protects every stored secret with a per-value data key, wraps the data key under a master key of the secret's owner (a
tenant, or the platform), and wraps the master key under an external root key. A master key has a version, and the
envelope of a value names the master key that wraps its data key, but the payload is bound to no master key. NIST SP
800-57 bounds the cryptoperiod of a key-encryption key, and Auth0 exposes a rekey operation for the same reason, so the
master key of an owner has to rotate: on a schedule, on an administrator's request, and without touching a payload.

Rotation has to hold across replicas that share one store and have no leader. A replica caches the unwrapped master keys
of an owner, a broadcast that tells the others is best effort, a key may be destroyed only when nothing can still need
it, and the sources of protected values are not a closed list: grants, stored secrets and the Data Protection key ring
are the family's, and a package protects its own values with `ISecretProtector` too.

## Decision

A master key rotates to a new version, the data keys it wraps are rewrapped under the active key in the background, and
a retired key is destroyed once a scan finds no value that still names it. The payloads are never read or rewritten.

1. **One seam reports, rotates, rewraps and destroys.** `IMasterKeyManager` has `GetStatusAsync`, `RotateAsync`,
   `RewrapAsync` and `DestroyRetiredAsync`. Its DTOs (`MasterKeyInfo`, `MasterKeyStatus`, `MasterKeyRotation`) carry no
   wrapped key and no key check.
2. **Rotation creates the successor first, retires the active key, then stores the successor.** `RotateAsync(owner)`
   creates version `latest + 1` through the owner's root, which is the deployment root or the owner's own root key
   ([ADR-1414](1414-a-tenant-can-wrap-its-master-keys-under-a-root-key-it-controls.md)), retires the active row with its
   concurrency token checked, and stores the successor on a bounded token of its own. A failed insert with no other
   active row re-activates the retired row, and a successor that another replica stored first is adopted. An owner
   without any key receives its first and reports no retired key. An owner with retired keys and no active key, whose
   successor another rotation is storing, and a concurrent second rotation fail with `StoreConcurrencyException`. A
   rotation within twice `RefreshInterval` of the owner's last one fails with `MasterKeyRotationTooSoonException`,
   since every replica has not yet loaded the active key.
3. **A cached key set is bounded in age.** A cache entry records when it was loaded. A call that finds an entry older
   than `SecretProtection:MasterKeys:RefreshInterval` (five minutes) reads the active row and the rows of every cached
   key again, reuses the key material it already holds, makes no unwrap call, and drops the keys whose rows do not
   exist. A lost broadcast therefore delays a replica by at most one interval.
4. **Rewrap changes the data key's wrap and nothing else.** `ISecretProtector.Rewrap` unwraps the data key under the key
   the envelope names, wraps it under the active master key with a fresh nonce, writes the active key id into the
   header, and copies the payload nonce, tag and ciphertext unchanged. The result has the length of the input, and a
   value already under the active key is copied. The payload's associated data carries no key id, so the payload stays
   valid. A failure throws `CryptographicException` that names the owner and the key ids and never material.
   `ISecretProtectorProvider.TryReadMasterKeyId` exposes the key id of an envelope, which is not secret, without
   exposing the layout.
5. **Protected values are found through an open strategy collection.** `IProtectedValueSource` has two members:
   `RewrapAsync(owner, masterKeyIds)` rewraps every value of the source that names one of the keys, and
   `FindReferencedAsync(owner, masterKeyIds)` answers the subset that a value still names. Every source is asked once
   per owner, and a source ignores an owner it holds nothing for. The family registers three: the stored secrets of a
   tenant (its tenant record, clients and resource servers), the grant payloads of a tenant, and the
   platform's Data Protection key ring. A package that protects its own values registers a source for them
   ([ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md)); a value that no source reports is not rewrapped
   and becomes unreadable once its key is destroyed.
6. **Sources page, rewrap in units of work, and isolate each value.** `ISecretProtectorProvider.GetMasterKeyIdPrefix`
   lets the store return only the rows whose value starts with a requested key's prefix. A source reads those rows in
   pages of 100 by row id, rewraps each value and writes the page in one unit of work with each row's token checked. A
   value that cannot be rewrapped is logged by record id and counted failed, a vanished row is skipped, and a page
   whose conflicts outlast three attempts is counted failed; the pass goes on. A failed value still names its key, so
   the key is kept. A rewritten row does not match the prefix, so a retry is idempotent.
7. **Stored envelopes are rewritten without a schema change.** `ISecretEnvelopeStore` pages the protected values of an
   owner's secrets that start with a prefix and replaces one, reporting a missing row with `false` and bumping the
   secrets concurrency token of the record that owns the secret, so every replica reloads its secrets. `IGrantStore`
   pages and replaces, with the same prefixes and result, the protected payloads of a tenant's grants. Key material
   stays immutable: the envelope of the same material is replaced.
8. **Destroy needs a grace, no reference and a switch.** `DestroyRetiredAsync(owner)` asks each source once for every
   candidate and removes each retired row whose `RetiredWhen + RetirementGrace` (30 days) has passed and that no source
   reports, each in its own unit of work with its token checked. It then evicts the owner and tells the observers of
   every key it removed, also when a later removal fails. `SecretProtection:MasterKeys:DestroyRetired` set to `false`
   keeps retired keys for good. The grace covers in-flight protectors, replicas that have not reloaded and Data
   Protection payloads that a key protected just before it expired. A backup keeps its own copies of the destroyed key.
9. **Data Protection ring keys are never rewritten.** `IXmlRepository` stores key elements and never rewrites one. New
   ring keys use the active platform key. `PlatformSecretProtectorLoader` reads the key elements of the ring and keeps
   every retired platform key that a live ring key names, meaning its expiration plus the retirement grace is in the
   future. It logs and skips a key that is missing or does not unwrap, so the startup gate of
   [ADR-1247](1247-the-platform-master-key-protects-the-data-protection-ring-and-an-unreadable-key-stops-the-host.md)
   still reports or revokes that ring key, and it publishes the protector to the holder. The warmup, a refresh service
   every `RefreshInterval` and a handler for the platform's broadcast run it. The ring source reports a reference while
   a live ring key names the key, and always when the ring cannot be read, so the platform key is then never destroyed
   automatically.
10. **Announcement and rewrap job.** `IMasterKeyChangeObserver` receives a rotation and a destruction after the commit,
    on a bounded token of its own, and a failing observer is logged and swallowed. The family's observer publishes the
    owner on the `master-key-changed` channel, which every replica's handler answers by evicting the owner, and after a
    rotation dispatches a `MasterKeyRewrapJob` delayed by twice the refresh interval, by which time every replica has
    reloaded. The dispatcher is not durable, so the job only brings the work forward.
11. **A background pass is the durable path.** `MasterKeyMaintenanceService` waits for the platform to materialize and
    then runs every `MaintenanceInterval` (one day).
    One replica takes the lease `master-key-maintenance:platform` for half the interval and rotates every active key
    older than `RotationPeriod` (365 days), then rewraps and destroys for every owner that has retired keys, which it
    reads in keyset pages that carry only the owner of each retired row. Each owner runs in its own try block, and the
    audit event of a change is published after its commit through `PostCommitWork`
    ([ADR-1351](1351-work-that-follows-a-committed-change-runs-on-its-own-bounded-token.md)). The pass never runs on
    the dispatcher.
12. **Options are validated when the host starts.** `SecretProtection:MasterKeys` binds strictly
    ([ADR-1137](1137-configuration-binds-strictly-and-every-option-timer-period-and-retention-is-validated-at-startup.md)):
    `RotationPeriod` (at least one day), `RefreshInterval` (one second to one hour), `RetirementGrace` (at least twice
    `RefreshInterval`), `MaintenanceInterval` (at least one minute and a period a timer accepts) and `DestroyRetired`.
    Every message names the full key.
13. **The management API reports and rekeys.** `GET` and `POST api/tenants/{tenantId}/encryption-keys[/rekey]` (scope
    family `encryption_keys`) and `api/platform/encryption-keys[/rekey]` (scope family `platform_encryption_keys`)
    answer `EncryptionKeysResource` and `EncryptionKeyRotationResource`. Status needs `Read` and rekey needs `Update`.
    An unknown owner answers `404` and a key is never created for it. A rekey that loses a race to another rekey
    answers `412`, and a rekey within twice `RefreshInterval` of the last rotation answers `409` with `Retry-After`
    and `retryAfterSeconds` in the problem document
    ([ADR-1483](1483-every-management-error-is-a-problem-document-that-its-route-declares.md)). The console offers a
    confirmed Rekey action and shows no material, because the API returns none.
14. **Master key changes are audit events.** `master_key.rotated` and `master_key.destroyed` belong to the `key`
    category. The reason is `requested`, `rotation_period_elapsed`, `unreferenced` or `root_key_changed`. The source is
    `management` with the administrator for a rekey through the API, and `openid` with the system actor for the
    background pass. The events are published after commit through `PostCommitWork` and never carry material.

## Options considered

- **One unit of work that retires the old row and inserts the new one.** It depends on the provider issuing the update
  before the insert under the filtered unique index on active keys, which EF Core does not guarantee. Retire-then-insert
  is correct by construction. Creating the successor's material first keeps the root key call out of the window between
  the two, and a failed insert re-activates the retired row.
- **Re-encrypting payloads.** A rotation would touch every payload, and the data key's wrap is what the master key
  protects.
- **Rewrapping Data Protection ring keys in the database.** `IXmlRepository` never rewrites an element, and editing its
  table behind it bypasses Data Protection's own cache and format.
- **Rotating on the Dispatch port alone.** The dispatcher is not durable. The pass under a lease is the durable path and
  the job only makes it prompt.
- **Destroying at retirement.** In-flight protectors, replicas that missed a broadcast and cookies protected by a ring
  key just before it expired still need the key.
- **A closed list of envelope holders.** Packages protect their own data, so the holders are an open collection.
- **Owner-scoped event names (`tenant.master_key.rotated`, `platform.master_key.rotated`).** The owner is a resource of
  the event, so one name covers every owner type, and an owner type a package adds needs no new event name
  ([ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md)).

The chosen design rotates by retire-then-insert, rewraps data keys through an open collection of sources, and destroys a
key only after a grace and a scan that finds no reference.

## Consequences

- A rotation does not heal a compromised data key. It protects against later copies of the database under the old master
  key; the response to a compromised secret is rotating that secret.
- A backup keeps the retired key it held.
- A value that no registered source reports is lost when its key is destroyed unless `DestroyRetired` is `false`.
- The rewrap of a large tenant's grants holds one dispatcher worker for its duration.
- A rekey and a destruction are audited and a status read is not; the console shows the same view as the API.
- A pass reads only the rows under a retired key, so an owner whose values are all under its active key costs one
  filtered query per source.

## References

- [ADR-1235](1235-stored-secrets-and-grant-payloads-are-protected-by-a-key-hierarchy-under-an-external-root-key.md),
  [ADR-1408](1408-an-offline-host-command-rewraps-every-master-key-under-the-configured-root-and-recovery-keys.md)
- `src/NCode.Identity.Secrets.Persistence/Protection/DefaultMasterKeyManager.cs`,
  `src/NCode.Identity.Secrets.Persistence/Protection/DefaultMasterKeyRing.cs`,
  `src/NCode.Identity.OpenId.Core/SecretProtection/`, `src/NCode.Identity.Server/SecretProtection/`
