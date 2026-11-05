# 1407. A master key rotates, its data keys are rewrapped in the background, and it is destroyed once nothing names it

- **Status:** Accepted (amended by [ADR-1413](1413-the-platform-master-key-protects-the-data-protection-key-ring-and-the-hosts-own-data.md):
  the owners are the platform and each tenant, the platform's master key rotates and is rewrapped like a tenant's but
  protects only the Data Protection key ring and the host's own data, the lease is `master-key-maintenance:platform`,
  and the platform's keys are reported and rekeyed under `/api/platform/encryption-keys` with the
  `platform_encryption_keys` scope family)
- **Date:** 2026-11-04
- **Deciders:** NCode Group
- **Amends:** [ADR-1235](1235-stored-secrets-are-protected-by-a-key-hierarchy-under-an-external-root-key.md), decision 2's
  versioning of an owner's master keys (an owner's versions rotate and a retired key is destroyed) and decision 13's
  key cache (cached keys reload after a refresh interval and a destroyed key leaves memory);
  [ADR-1247](1247-a-data-protection-key-the-server-cannot-decrypt-stops-the-host.md), decision 1's warmup (it loads
  the retired server keys that a live ring key names);
  [ADR-1011](1011-secret-management-api.md): key material is immutable, and a rotation rewrites the stored envelope
  of the same material; [ADR-1221](1221-intervals-and-retentions-are-configured-and-validated-when-the-host-starts.md):
  the master key intervals and retentions are configured under `SecretProtection:MasterKeys` and validated when the host
  starts

## Context

[ADR-1235](1235-stored-secrets-are-protected-by-a-key-hierarchy-under-an-external-root-key.md) protects every stored
secret with a per-value data key, wraps the data key under a master key of the secret's owner (a tenant, or the
server), and wraps the master key under an external root key. A master key has a version, and the envelope of a value
names the master key that wraps its data key, but the payload is bound to no master key. NIST SP 800-57 bounds the
cryptoperiod of a key-encryption key, and Auth0 exposes a rekey operation for the same reason, so the master key of an
owner has to rotate: on a schedule, on an administrator's request, and without touching a payload.

Rotation has to hold across replicas that share one store and have no leader. A replica caches the unwrapped master keys
of an owner, a broadcast that tells the others is best effort, a key may be destroyed only when nothing can still need
it, and the sources of protected values are not a closed list: grants, stored secrets and the Data Protection key ring are
the family's, and a package protects its own values with `ISecretProtector` too.

## Decision

A master key rotates to a new version, the data keys it wraps are rewrapped under the active key in the background, and
a retired key is destroyed once a scan finds no value that still names it. The payloads are never read or rewritten.

1. **Rotation creates the successor first, retires the active key, then stores the successor.**
   `RotateAsync(owner)` reads the owner's rows, creates version `latest + 1`'s material through the root key provider,
   retires the active row with its concurrency token checked, and stores the successor on a bounded token of its own;
   a store failure with no other active row re-activates the retired row, and a successor that another replica's ring
   stored first is adopted, and the repair after a failed insert runs on a bounded token of its own. An owner without any key receives its first and reports no retired key; an owner with
   retired keys and no active key, and the second of two concurrent rotations, fail with `StoreConcurrencyException`.
   A rotation within twice `RefreshInterval` of the owner's last one fails with `MasterKeyRotationTooSoonException`,
   since every replica has not yet loaded the active key.
2. **A cached key set is bounded in age.** A ring entry records when it was loaded. A call that finds an entry older than
   `SecretProtection:MasterKeys:RefreshInterval` (five minutes) reads the active row and the rows of every cached key
   again, reuses the key material it already holds, makes no new unwrap or root key call, and drops the keys whose rows
   do not exist. A lost broadcast therefore delays a replica by at most one interval.
3. **Rewrap changes the data key's wrap and nothing else.** `ISecretProtector.Rewrap` decodes the envelope, unwraps the
   data key with the key-encryption key of the master key the envelope names, wraps it under the active master key with
   a fresh nonce, writes the active key id into the header, and copies the payload nonce, tag and ciphertext unchanged.
   The result has the length of the input. A value already under the active key is copied. The payload's associated data
   carries no key id, so the payload stays valid. A failure throws `CryptographicException` that names the owner and the
   key ids and never material. `ISecretProtectorProvider.TryReadMasterKeyId` exposes the key id of an envelope, which is
   not secret, without exposing the layout.
4. **Protected values are found through an open strategy collection.** `IProtectedValueSource` has two members:
   `RewrapAsync(owner, masterKeyIds)` rewraps every value of the source that names one of the keys and answers a
   `ProtectedValueRewrapResult`, and `FindReferencedAsync(owner, masterKeyIds)` answers the subset that a value still
   names. Every source is asked once per owner. A source ignores an owner it holds nothing for. The family registers
   three: the stored secrets of a tenant, its clients and its resource servers, or of the server; the grant payloads of a
   tenant; and the Data Protection key ring of the server. A package that protects its own values registers a source for
   them ([ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md)): a value that no source reports is not
   rewrapped and becomes unreadable once its key is destroyed.
5. **Sources page in one pass per owner, rewrap in units of work, and isolate each value.**
   `ISecretProtectorProvider.GetMasterKeyIdPrefix` lets the store return only the rows whose value starts with a
   requested key's prefix. A source reads those rows in pages of 100 by row id, obtains one protector per page, rewraps
   each value and writes the page in one unit of work with each row's token checked. A value that cannot be rewrapped is
   logged by record id and counted failed, a vanished row is skipped, and a page whose conflicts outlast three attempts
   is counted failed; the pass goes on. A failed value still names its key, so the key is kept. A rewritten row does not
   match, so a retry is idempotent.
6. **Stored envelopes are rewritten without a schema change.** `ISecretEnvelopeStore` pages the protected values of the
   secrets of an owner that start with a prefix and replaces one, reporting a missing row with `false`, bumping the secrets concurrency token of the record that owns the secret so that
   every replica reloads its secrets; `IGrantStore` pages and replaces, with the same prefixes and result, the protected payloads of a tenant's grants.
   Material stays immutable: the envelope of the same material is replaced.
7. **Destroy needs a grace, no reference and a switch.** `IMasterKeyManager.DestroyRetiredAsync(owner)` asks each source
   once for every candidate and removes each retired row whose `RetiredWhen + RetirementGrace` has passed and that no
   source reports, each in its own unit of work
   with its token checked, and evicts the owner and tells the observers of every key it removed, also when a later
   removal fails. `SecretProtection:MasterKeys:DestroyRetired`
   set to `false` keeps retired keys for good. The grace (30 days) covers in-flight protectors, replicas that have not
   reloaded and Data Protection payloads that a key protected just before it expired. A backup keeps its own copies of
   the destroyed key.
8. **Data Protection ring keys are never re-encrypted.** `IXmlRepository` stores key elements and never rewrites one.
   New ring keys use the active server key through the holder. `ServerSecretProtectorLoader` reads the key elements of
   the ring, keeps every retired server key that a live ring key names (its expiration plus the retirement grace is in the
   future), logs and skips a key that is missing or does not unwrap, so the startup gate of
   [ADR-1247](1247-a-data-protection-key-the-server-cannot-decrypt-stops-the-host.md) still reports or revokes that ring
   key, and publishes the protector to the holder. The warmup, a refresh service every `RefreshInterval` and a handler
   for the server's broadcast run it. The ring source reports a reference while a live ring key names the key, and
   always when the ring cannot be read, so the server key is then never destroyed automatically.
9. **Announcement and rewrap job.** `IMasterKeyChangeObserver` receives a rotation and a destruction after the commit, on
   a bounded token of its own, and a failing observer is logged and swallowed. The family's observer publishes the owner
   on the `master-key-changed` channel, which every replica's handler answers by evicting the owner, and after a rotation
   dispatches a rewrap job delayed by twice the refresh interval, by which time every replica has reloaded. The
   dispatcher is not durable, so the job only brings the work forward.
10. **A background pass is the durable path.** `MasterKeyMaintenanceService` waits for the server to materialize and then
    runs every `MaintenanceInterval` (one day). One replica takes the lease `master-key-maintenance:{serverId}` for half the
    interval and rotates every active key older than `RotationPeriod` (365 days), then rewraps and destroys for every
    owner that has retired keys, which it reads in keyset pages that carry only the owner of each retired row. Each owner runs in its own try block, and the audit event of a change is published after
    its commit through `PostCommitWork` ([ADR-1351](1351-a-committed-change-is-applied-and-announced-on-its-own-bounded-token.md)).
    The pass never runs on the dispatcher.
11. **Options are validated when the host starts.** `SecretProtection:MasterKeys` binds strictly:
    `RotationPeriod` (at least one day), `RefreshInterval` (one second to one hour), `RetirementGrace` (at least twice
    `RefreshInterval`), `MaintenanceInterval` (at least one minute and a period a timer accepts) and `DestroyRetired`.
    Every message names the full key.
12. **One seam reports and rotates.** `IMasterKeyManager` has `GetStatusAsync`, `RotateAsync`, `RewrapAsync` and
    `DestroyRetiredAsync`. Its DTOs (`MasterKeyInfo`, `MasterKeyStatus`, `MasterKeyRotation`) carry no wrapped key and no
    key check.
13. **The management API reports and rekeys.** `GET` and `POST …/encryption-keys[/rekey]` on a tenant and on a server
    answer `EncryptionKeysResource` and `EncryptionKeyRotationResource`. The owner must exist (404 otherwise, and a key
    is never created for an unknown owner), and the caller needs `Read` or `Update` on it. The scope families are
    `encryption_keys` in the tenant plane and `server_encryption_keys` in the control plane. A rekey that loses a race to
    another rekey answers 412 through `StoreConcurrencyEndpointFilter`
    ([ADR-1214](1214-a-management-write-that-loses-a-race-is-answered-412.md)), and a rekey within twice
    `RefreshInterval` of the last rotation answers 409 with `Retry-After`.
14. **Master key changes are audit events.** `master_key.rotated` and `master_key.destroyed` belong to the `key` category
    with the resources `master_key` and the owner. The reason is `requested`, `rotation_period_elapsed` or
    `unreferenced`; the source is `management` with the administrator for a rekey through the API and `openid` with the
    system actor for the background pass. No event carries material.
15. **The console shows both.** A tenant's `encryption-keys` surface and a server's `Encryption keys` tab list the keys
    and offer a confirmed "Rekey" action. The view shows no material, because the API returns none.

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
- **Destroying at retirement.** In-flight protectors, replicas that missed a broadcast and cookies protected by a ring key
  just before it expired still need the key.
- **A closed list of envelope holders.** Packages protect their own data, so the holders are an open collection.
- **Owner-scoped event names (`tenant.master_key.rotated`, `server.master_key.rotated`).** The owner is a resource of
  the event, so one name covers every owner type, and an owner type a package adds needs no new event name
  ([ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md)).

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

- [ADR-1235](1235-stored-secrets-are-protected-by-a-key-hierarchy-under-an-external-root-key.md),
  [ADR-1408](1408-an-offline-host-command-rewraps-every-master-key-under-the-configured-root-and-recovery-keys.md)
- `src/NCode.Identity.Secrets.Persistence/Protection/DefaultMasterKeyManager.cs`,
  `src/NCode.Identity.Secrets.Persistence/Protection/DefaultMasterKeyRing.cs`,
  `src/NCode.Identity.OpenId.Core/SecretProtection/`, `src/NCode.Identity.Server/SecretProtection/`
- Issue [#171](https://github.com/NCodeGroup/Identity/issues/171)
