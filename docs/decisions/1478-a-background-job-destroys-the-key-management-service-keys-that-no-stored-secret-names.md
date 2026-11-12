# 1478. A background job destroys the key management service keys that no stored secret names

- **Status:** Accepted (amends [ADR-1471](1471-a-tenants-signing-and-encryption-keys-can-be-held-in-a-key-management-service.md), [ADR-1474](1474-a-remote-key-provider-is-described-by-data-creates-a-key-once-and-signs-into-the-callers-buffer.md), [ADR-1471](1471-a-tenants-signing-and-encryption-keys-can-be-held-in-a-key-management-service.md); amended by
  [ADR-1489](1489-a-remote-key-provider-registers-without-a-key-creation-location-and-only-one-that-has-one-creates-and-sweeps-keys.md):
  the sweep and the deployment name concern only providers that create keys)
- **Date:** 2026-11-08
- **Deciders:** NCode Group

## Context

A key that a seed, a rollover or a management create made in a key management service (KMS) can end up with no stored
secret that names it:

- **A creation that cannot be recorded.** A lost commit race or a crashed process leaves the key in the service with no
  secret that references it.
- **A destruction that fails.** A failed `IRemoteSecretDestroyer` call leaves the key in the service after its secret is
  gone.

Such a key stays in the service, billed and usable by the deployment's identity
([ADR-1471](1471-a-tenants-signing-and-encryption-keys-can-be-held-in-a-key-management-service.md)
Consequences). Keys carry an `ncid-` group name, but nothing scopes a key to one deployment, and deployments can share
an AWS account, an Azure Key Vault or a Google Cloud KMS key ring. A tenant's rollover pass sees only the keys of its
own secrets, so it never finds the keys of a deleted tenant.

## Decision

**A background job lists the keys a deployment created in each provider, and destroys through the existing destroyer
every listed key that no stored secret names and that is older than a grace period.**

1. **A deployment names itself.** `SecretProtection:RemoteKeys:Deployment` (lowercase letters and digits in
   hyphen-separated groups, at most 40 characters) is required when any remote key provider is described. It tags every
   created key (`ncode-identity:deployment` on AWS, `ncode-identity-deployment` on Azure tags and Google crypto key
   labels) and is hashed first into the group name, so deployments that share an account, a vault or a key ring never
   share a group and never sweep each other's keys. A deployment is one database of stored secrets: every replica and
   release that shares the database uses its name, and no other deployment in the same AWS account, vault or key ring
   uses it, because each deployment destroys every key of its name that its own database does not name.
2. **The provider lists its keys.** `IRemoteKeyProvider.ListKeysAsync(RemoteKeyListRequest { Deployment },
   CancellationToken)` returns an `IAsyncEnumerable<RemoteKeyListing { KeyId, GroupName, CreatedWhen, OwnerId?,
   SecretId? }>` of the enabled keys (key versions) in the provider's configured location that carry an `ncid-` group
   and the deployment. The listing is large and I/O-backed, so it is streamed.
   - AWS KMS: `ListKeys` in the region, `ListResourceTags` and `DescribeKey` (customer-managed and `Enabled`).
   - Azure Key Vault: keys named `ncid-*`, then their enabled versions tagged with the deployment.
   - Google Cloud KMS: the key ring's crypto keys labelled with the deployment and an `ncid-` group, then their
     `ENABLED` versions.

   A key whose tags cannot be read is skipped.
3. **The store lists the stored references.** `IRemoteKeyReferenceStore.GetKeyIdPageAsync(string provider, PageRequest
   page, CancellationToken)` pages, by the secret's surrogate id, the key identifiers of every stored secret whose
   encoding is `remote` and whose reference names the provider, for every owner.
4. **A hosted service dispatches the sweep.** `RemoteKeySweepService` (a `BackgroundService` in
   `NCode.Identity.OpenId.Core`) waits for the platform, then at start and every `SweepInterval`, under the shared-state
   lease `remote-key-sweep:platform` that lasts half the interval, dispatches one `RemoteKeySweepJob { Provider }` per
   described provider through `IJobDispatcher`. The dispatch port gives each provider its own retries and lets a
   durable dispatcher run it. A provider whose dispatch fails is logged, and the others are dispatched.
5. **A job handler sweeps one provider.** `RemoteKeySweepJobHandler` lists the provider's keys created at or before now
   minus `SweepGracePeriod`, only then reads every stored reference of that provider, and destroys each listed key that
   no reference names through `IRemoteSecretDestroyer.DestroyKeyAsync(RemoteKeyReference)`, the destroyer's bounded,
   logged path. Listing before reading means a key recorded during the pass is never swept, and the grace period means
   a key whose creation is in flight is never swept. A failed list or read throws so the dispatcher retries, and a
   repeated sweep is harmless.
6. **The options.** `SecretProtection:RemoteKeys` binds strictly to `RemoteKeyOptions`, and the `SecretProtection`
   section recognizes the `RemoteKeys` and `RemoteKeyProviders` children.

   | Option | Default | Bounds |
   | --- | --- | --- |
   | `Deployment` | none | required with a provider; the rule 1 format |
   | `SweepEnabled` | `true` | |
   | `SweepInterval` | one day | at least one minute, at most the timer maximum |
   | `SweepGracePeriod` | one day | between one hour and 365 days |

7. **A destruction is audited.** A destroyed key emits `RemoteKeyAuditEvent`: event `remote_key.destroyed`, category
   `key`, actor `system`, resource `remote_key` whose identifier is the reference text, in the control-plane tenant,
   because the KMS key identifier never reaches a tenant's audit trail
   ([ADR-1475](1475-the-management-api-creates-a-tenants-key-in-a-key-management-service-and-binds-one-that-proves-it-signs.md)
   rule 1 keeps it from the tenant's API). The details are `reason` (`orphaned`), `provider`, `key_id`, `group`,
   `owner` and `secret_id` when the tags carry them, and `key_created_when`.
8. **Every destruction stays recoverable for the service's window.** AWS schedules a pending deletion, Azure disables
   the version and Google schedules the destruction.
9. **Permissions.** AWS needs `kms:ListKeys`, `kms:ListResourceTags` and `kms:DescribeKey`. Azure `Key Vault Crypto
   Officer` lists keys. Google needs `cloudkms.cryptoKeys.list` and `cloudkms.cryptoKeyVersions.list`. Scoping the
   identity to the deployment's keys limits what it destroys: on AWS, `kms:ScheduleKeyDeletion` conditioned on
   `aws:ResourceTag/ncode-identity:deployment` equal to the deployment name, and `kms:CreateKey` and `kms:TagResource`
   on `aws:RequestTag/ncode-identity:deployment` (`kms:ListKeys` names no resource, and a bound key carries no such
   tag); on Azure, a vault of its own and a custom role without delete and purge, because a role assignment cannot be
   conditioned on a key's tags; on Google, a key ring of its own, because a grant on a key ring covers every crypto key
   in it.

## Options considered

- **Sweeping inside the rollover pass.** Rejected: a tenant pass never sees the keys of a deleted tenant.
- **Relying on a dedicated account, vault or key ring alone.** Rejected: shared scopes are common, and a sweep that
  trusts the scope would destroy another deployment's keys.
- **Destroying without a grace period.** Rejected: it races an in-flight creation, whose key exists before its secret
  is committed.
- **Running the sweep inline in the hosted service rather than through the dispatcher.** Rejected: there would be no
  per-provider retry and no durable backend.

## Consequences

- A deployment names itself before it registers a provider.
- Two deployments that share a scope and a name sweep each other's keys, so a name is unique to its database within a
  scope.
- Each sweep costs list calls proportional to the keys in the scope.
- A destroyed key stays recoverable for the service's window.
- A key whose tags cannot be read is never swept, so a provider that strips tags leaves its keys in place.

## References

- [ADR-1471](1471-a-tenants-signing-and-encryption-keys-can-be-held-in-a-key-management-service.md)
- [ADR-1474](1474-a-remote-key-provider-is-described-by-data-creates-a-key-once-and-signs-into-the-callers-buffer.md)
- [ADR-1475](1475-the-management-api-creates-a-tenants-key-in-a-key-management-service-and-binds-one-that-proves-it-signs.md)
- [ADR-1471](1471-a-tenants-signing-and-encryption-keys-can-be-held-in-a-key-management-service.md)
- Issue [#654](https://github.com/NCodeGroup/identity/issues/654)
