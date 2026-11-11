# 1429. A tenant root key can be reached through an identity the tenant grants

- **Status:** Accepted
- **Date:** 2026-11-05
- **Deciders:** NCode Group
- **Amends:** [ADR-1414](1414-a-tenant-can-wrap-its-master-keys-under-a-root-key-it-controls.md), decision 2 (the server
  reaches a tenant's key with an identity the tenant grants as well as with the deployment's own identity), decision 3
  (the record and each master key row hold the access identity), decision 4 (the wrap names the key and the access),
  decision 5 (a change of the access identity alone rewraps without rotating), decision 9 (the audit events carry
  `access_identity`) and decision 12 (`accessIdentity` and `rootKeyAccessBinding` in the management API);
  [ADR-1415](1415-a-key-wrapper-can-wrap-under-a-key-the-caller-names.md), decision 1 (the contract's members)

## Context

A tenant root key ([ADR-1414](1414-a-tenant-can-wrap-its-master-keys-under-a-root-key-it-controls.md)) is reached with the
deployment's own workload identity. A tenant whose key policy cannot grant that identity, such as a key in an account,
project or directory the tenant does not share with the deployment, has no way to bring a key. Each provider's recommended
cross-account pattern is delegation: the tenant grants access to an identity it controls, and the caller acts through it
without a stored secret. AWS offers `sts:AssumeRole` into a role the tenant owns with an `ExternalId`, Google Cloud offers
service account impersonation, and Microsoft Entra ID offers a multi-tenant application that the tenant consents to in its
directory.

## Decision

1. **The tenant names an access identity, and the server never stores a secret.** The access identity is a role ARN
   (AWS), a service account email (Google Cloud) or a Microsoft Entra directory GUID (Azure). No access key, client secret
   or service account key file exists anywhere in the server's configuration or in the database. An owner root key that
   names no access identity is reached with the deployment's own identity.
2. **The server derives an access binding from the owner.** The binding is `SecretOwner.Normalize().ToString()`, the
   owner's normalized `type:id` such as `tenant:acme`. The AWS adapter sends it as the `ExternalId` of the assume-role
   call, so one tenant cannot make the server assume a role that another tenant trusts: the other tenant's trust policy
   requires its own binding. The Google Cloud and Azure adapters ignore the binding. `GET` on the encryption keys reports
   it as `rootKeyAccessBinding`, so a tenant writes its trust policy before it sets the key.
3. **The contract carries the access.** `KeyAccess(string Identity, string Binding)` and `ReferencedKey(string
   KeyReference, KeyAccess? Access)` are records in `NCode.Identity.Secrets.Abstractions`, namespace
   `NCode.Identity.Secrets.KeyWrapping`. `IReferencedKeyWrapper` has four members: `TryValidateKeyReference`,
   `TryValidateAccessIdentity(string, out string? failure)` (a syntax check, like the reference check),
   `WrapAsync(ReferencedKey, key, context, cancellationToken)` and `UnwrapAsync(WrappedKey, KeyAccess, context,
   destination, cancellationToken)`. A `null` access means the deployment's own identity and is the existing
   `IKeyWrapper.UnwrapAsync`.
4. **Each provider reaches the key through its own delegation.**
   - AWS: `AwsKmsKeyWrapper` caches one KMS client per region, role ARN and binding. Its credentials are an
     `AssumeRoleAWSCredentials` over the default credential chain, with the session name `ncode-identity-root-key` and the
     binding as the external ID. The role ARN has the form `arn:aws…:iam::<account>:role/<name>`.
   - Google Cloud: `GoogleCloudKmsKeyWrapper` caches one client per service account. Its credential is
     `GoogleCredential.GetApplicationDefaultAsync()` impersonating the service account with the KMS scopes. The email has
     the form `…@….iam.gserviceaccount.com`.
   - Azure: `AzureKeyVaultKeyWrapper` caches one `DefaultAzureCredential` per directory, whose `TenantId` is the
     directory, and one key client per directory and key id. The deployment's application is multi-tenant and consented in
     the tenant's directory. The identity is a non-empty GUID.
5. **Each row records its access identity.** `PersistedOwnerRootKey.AccessIdentity` (nullable, at most 1024 characters)
   holds the owner's current identity, and `PersistedMasterKey.RootKeyAccessIdentity` holds the identity the row's key is
   reached through, because unwrapping is driven by the row, as `RootKeyReference` is. A row wrapped under an earlier
   identity stays readable while the rewrap engine moves it. A row conforms to its owner's root only when its access
   identity matches the owner's as well as its wrapper and reference.
6. **A change of the access identity alone rewraps without rotating.** `IOwnerRootKeyManager.SetAsync(owner,
   OwnerRootKeyRequest, cancellationToken)` takes the provider, the reference and the access identity. When only the
   access identity differs, the manager probes the key through it, stores it and rewraps the nonconforming rows without
   rotating, because the key is the same and no trust boundary is crossed, and it skips the rotation spacing check. A
   change of the provider or the reference rotates, as in ADR-1414 decision 5. `IOwnerRootKeyManager` also exposes
   `TryValidateAccessIdentity` and `GetAccessBinding`.
7. **Audit and the management API report the identity.** The `root_key.*` events carry `access_identity`, and the
   `master_key.*` events carry `root_key_access_identity`. `PUT` accepts `accessIdentity` in its body and the root key
   resource reports it, each master key reports `rootKeyAccessIdentity`, and `GET` reports `rootKeyAccessBinding`. The
   console's tenant encryption keys tab has an optional "Access identity" field.
8. **A client reached through an access identity lives in a cache that keeps only identities that work.**
   `KeyAccessClientCache<TClient>` in `NCode.Identity.Secrets.Abstractions` holds one entry per key. The client is
   created once per entry, and a failed creation is not cached. The first successful call through an entry confirms it,
   and the entry is kept from then on. A call that fails before any call succeeded, other than by cancellation, removes
   the entry and disposes its client once the calls in flight through it end. An entry unused for one day expires and
   its client is disposed, and disposing the factory disposes the rest. Identities that a tenant names and that never
   work leave nothing behind, and a working identity is not churned by a transient failure. The AWS, Google Cloud and
   Azure factories each run their access-bound calls through the cache with `ExecuteAsync`, and a third-party
   referenced wrapper uses the same cache.

## Options considered

- **Stored credentials.** An access key, client secret or key file is a long-lived secret that the server stores, the
  tenant has to rotate and an attacker who reads the database can leak.
- **The identity only on the owner record.** A row under an earlier identity could not be read while the rewrap engine
  moves it, because the row would be unwrapped through the identity of the record instead of its own.
- **A server-generated random external ID.** It has to be shown to the tenant before the key is set anyway, and the
  owner-derived binding is unique to the owner and cannot be chosen by another tenant.

## Consequences

- A tenant brings a key from an account, project or directory it does not share with the deployment, by granting an
  identity it controls, and the server stores no secret for it.
- The AWS adapter makes one STS call per role for the lifetime of the credentials it obtains.
- A tenant that names a role it does not trust to the binding of its own owner is refused by AWS, so a role trusted by one
  tenant cannot be assumed on behalf of another.
- A Google Cloud or Azure tenant can name an identity that another tenant granted to the deployment. The owner-bound
  context keeps that identity from unwrapping the other tenant's rows, as the consequences of
  [ADR-1414](1414-a-tenant-can-wrap-its-master-keys-under-a-root-key-it-controls.md) say for keys.
- Changing the access identity of the same key creates no master key material.

## References

- [ADR-1414](1414-a-tenant-can-wrap-its-master-keys-under-a-root-key-it-controls.md),
  [ADR-1415](1415-a-key-wrapper-can-wrap-under-a-key-the-caller-names.md),
  [ADR-1260](1260-azure-key-vault-and-google-cloud-kms-are-root-key-providers.md)
- `src/NCode.Identity.Secrets.Abstractions/KeyWrapping/KeyAccess.cs`,
  `src/NCode.Identity.Secrets.Abstractions/KeyWrapping/ReferencedKey.cs`,
  `src/NCode.Identity.Secrets.Abstractions/KeyWrapping/IReferencedKeyWrapper.cs`,
  `src/NCode.Identity.Secrets.Persistence/Protection/DefaultOwnerRootKeyManager.cs`
- Issue [#574](https://github.com/NCodeGroup/identity/issues/574)
