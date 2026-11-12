# 1478. A background job destroys the key management service keys that no stored secret names

- **Status:** Accepted
- **Date:** 2026-11-08
- **Deciders:** NCode Group

## Context

A key that a seed, a rollover or a management create made in a key management service (KMS) can end up with no stored
secret that names it:

- **A creation that cannot be recorded.** A lost commit race or a crashed process leaves the key in the service with no
  secret that references it.
- **A destruction that fails.** A failed destroyer call leaves the key in the service after its secret is gone.

Such a key stays in the service, billed and usable by the deployment's identity
([ADR-1471](1471-a-tenants-signing-and-encryption-keys-can-be-held-in-a-key-management-service.md)). Keys carry an
`ncid-` group name, but nothing scopes a key to one deployment, and deployments can share an AWS account, an Azure Key
Vault or a Google Cloud KMS key ring. A tenant's rollover pass sees only the keys of its own secrets, so it never finds
the keys of a deleted tenant.

## Decision

**A background job lists the keys a deployment created in each provider, and destroys through the one destroyer every
listed key that no stored secret names and that is older than a grace period.**

1. **One destroyer.** `IRemoteSecretDestroyer` (`DefaultRemoteSecretDestroyer`, with `DestroyAsync(removedSecrets)` and
   `DestroyKeyAsync(RemoteKeyReference)`) is the one place keys are destroyed. It skips secrets that are not `remote`
   or whose `Origin` is not `generated`, runs each provider's `DestroyKeyAsync` through `PostCommitWork.RunAsync`
   without a caller token
   ([ADR-1351](1351-work-that-follows-a-committed-change-runs-on-its-own-bounded-token.md)), never throws, and logs
   a removed secret's failure with its secret id and provider and an orphaned key's failure with its provider and KMS
   key id. Its callers are the seed, the rollover pass, the management secret delete and
   tenant delete (after the commit and after the tenant is announced), management create when its record fails to
   commit, the generator when anything after creation fails, and the sweep.
2. **A deployment names itself.** `SecretProtection:RemoteKeys:Deployment` (lowercase letters and digits in
   hyphen-separated groups, at most 40 characters) is required only when a provider that creates keys is described. It
   tags every created key (`ncode-identity:deployment` on AWS, `ncode-identity-deployment` on Azure tags and Google
   crypto key labels) and is hashed first into the group name, so deployments that share an account, a vault or a key
   ring never share a group. A deployment is one database of stored secrets: every replica and release that shares the
   database uses its name, and no other deployment in the same AWS account, vault or key ring uses it, because each
   deployment destroys every key of its name that its own database does not name.
3. **The provider lists its keys.** `IRemoteKeyProvider.ListKeysAsync(RemoteKeyListRequest { Deployment },
   CancellationToken)` streams `RemoteKeyListing { KeyId, GroupName, CreatedWhen, OwnerId?, SecretId? }` of the enabled
   keys in the provider's configured location that carry an `ncid-` group and the deployment. The listing is large and
   I/O-backed, so it is streamed.
   - AWS KMS: `ListKeys` in the region, then `ListResourceTags` and `DescribeKey`.
   - Azure Key Vault: keys named `ncid-*`, then their enabled versions tagged with the deployment.
   - Google Cloud KMS: the key ring's labeled crypto keys with `ENABLED` versions.

   A key whose tags cannot be read is skipped.
4. **The store lists the stored references.** `IRemoteKeyReferenceStore.GetKeyIdPageAsync(string provider, PageRequest
   page, CancellationToken)` pages, by surrogate id, the key identifiers of every stored `remote` secret of the
   provider, for every owner.
5. **A hosted service dispatches the sweep.** `RemoteKeySweepService` (a `BackgroundService` in
   `NCode.Identity.OpenId.Core`) waits for the platform, then at start and every `SweepInterval`, under the shared-state
   lease `remote-key-sweep:platform` that lasts half the interval, dispatches one `RemoteKeySweepJob { Provider }` per
   provider that creates keys through `IJobDispatcher`. The dispatch port gives each provider its own retries and lets
   a durable dispatcher run it. A failed dispatch is logged, and the rest continue.
6. **A job handler sweeps one provider.** `RemoteKeySweepJobHandler` lists the provider's keys created at or before now
   minus `SweepGracePeriod`, only then reads every stored reference of that provider, and destroys each listed key that
   no reference names through `IRemoteSecretDestroyer.DestroyKeyAsync(RemoteKeyReference)`. Listing before reading
   means a key recorded during the pass is never swept, and the grace period means a key whose creation is in flight is
   never swept. A failed list or read throws so the dispatcher retries, and a repeated sweep is harmless.
7. **The options.** `SecretProtection:RemoteKeys` binds strictly to `RemoteKeyOptions`.

   | Option | Default | Bounds |
   | --- | --- | --- |
   | `Deployment` | none | required with a provider that creates keys; the rule 2 format |
   | `SweepEnabled` | `true` | |
   | `SweepInterval` | one day | at least one minute, at most the timer maximum |
   | `SweepGracePeriod` | one day | between one hour and 365 days |

8. **A destruction is audited.** Each sweep destruction emits `RemoteKeyAuditEvent`: event `remote_key.destroyed`,
   category `key`, actor `system`, resource `remote_key` whose identifier is the reference text, in the control-plane
   tenant, because the KMS key identifier never reaches a tenant's audit trail
   ([ADR-1476](1476-the-management-api-creates-a-tenants-remote-key-and-binds-one-only-after-proving-it.md)). The
   details are `reason` (`orphaned`), `provider`, `key_id`, `group`, `owner`, `secret_id` and `key_created_when`.
9. **Every destruction stays recoverable for the service's window.** AWS schedules a pending deletion, Azure disables
   the version and Google schedules the destruction.
10. **Permissions.** AWS needs `kms:ListKeys`, `kms:ListResourceTags` and `kms:DescribeKey`, with
    `kms:ScheduleKeyDeletion` conditioned on the `ncode-identity:deployment` resource tag and `kms:CreateKey` and
    `kms:TagResource` on the request tag; `kms:ListKeys` names no resource, so it takes no condition, and a bound key
    carries no deployment tag, so the deletion grant does not reach it. Azure takes a vault of its own and a custom
    role without delete or purge, because a role assignment cannot be conditioned on a key's tags. Google takes a key ring of its own with
    `cloudkms.cryptoKeys.list` and `cloudkms.cryptoKeyVersions.list`, because a grant on a key ring covers every crypto
    key in it.

## Options considered

- **Sweeping inside the rollover pass.** Rejected: a tenant pass never sees the keys of a deleted tenant.
- **Relying on a dedicated account, vault or key ring alone.** Rejected: shared scopes are common, and a sweep that
  trusts the scope would destroy another deployment's keys.
- **Destroying without a grace period.** Rejected: it races an in-flight creation, whose key exists before its secret
  is committed.
- **Running the sweep inline in the hosted service rather than through the dispatcher.** Rejected: there would be no
  per-provider retry and no durable backend.
- **A background job through the dispatcher, with a deployment tag and a grace period.** Chosen: it reclaims every
  orphan, never touches another deployment's keys and never races a creation.

## Consequences

- Keys left by a lost commit race or a failed destruction are reclaimed.
- A deployment names itself before it registers a provider that creates keys.
- Two deployments that share a scope and a name sweep each other's keys, so a name is unique to its database within a
  scope.
- Each sweep costs list calls proportional to the keys in the scope.
- A destroyed key stays recoverable for the service's window.
- A key whose tags cannot be read is never swept, so a provider that strips tags leaves its keys in place.
- A BFF's key-ring keys are never swept
  ([ADR-1479](1479-a-protocol-clients-and-a-bffs-keys-can-be-held-in-a-key-management-service.md)).

## References

- [ADR-1471](1471-a-tenants-signing-and-encryption-keys-can-be-held-in-a-key-management-service.md)
- [ADR-1474](1474-a-remote-key-provider-is-described-by-data-and-reads-signs-and-decrypts-by-key-identifier.md)
- [ADR-1476](1476-the-management-api-creates-a-tenants-remote-key-and-binds-one-only-after-proving-it.md)
- [ADR-1351](1351-work-that-follows-a-committed-change-runs-on-its-own-bounded-token.md)
- [ADR-1479](1479-a-protocol-clients-and-a-bffs-keys-can-be-held-in-a-key-management-service.md)
