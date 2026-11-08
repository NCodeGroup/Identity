# 1415. A key wrapper can wrap under a key the caller names

- **Status:** Accepted (amended by [ADR-1429](1429-a-tenant-root-key-can-be-reached-through-an-identity-the-tenant-grants.md))
- **Date:** 2026-11-05
- **Deciders:** NCode Group
- **Amends:** [ADR-1279](1279-key-wrapping-is-a-secrets-abstraction-shared-by-the-root-and-recovery-wrappers.md),
  decision 1's contract (a wrapper may also implement `IReferencedKeyWrapper`; decision 5's root role stays selection by
  name); [ADR-1260](1260-azure-key-vault-and-google-cloud-kms-are-root-key-providers.md), the Azure Key Vault and Google
  Cloud KMS adapters (they wrap under a named key as well as the configured one)

## Context

A key wrapper is bound to the one key its options name. A tenant root key
([ADR-1414](1414-a-tenant-can-wrap-its-master-keys-under-a-root-key-it-controls.md)) needs the same provider to wrap
under a key chosen at run time, while unwrapping already addresses a key through `WrappedKey.KeyId` alone, independent of
configuration.

## Decision

1. **`IReferencedKeyWrapper : IKeyWrapper` is the contract.** It lives in `NCode.Identity.Secrets.Abstractions`, namespace
   `NCode.Identity.Secrets.KeyWrapping`, and has two members. `TryValidateKeyReference(string keyReference, out string?
   failure)` checks the syntax of a reference only and returns a phrase that follows the field name when it fails.
   `WrapAsync(string keyReference, key, context, cancellationToken)` wraps under the named key with the same binding,
   checks and error mapping as the configured wrap, and throws `ArgumentException` (parameter `keyReference`) for a
   reference that fails the syntax check. The reported `WrappedKey.KeyId` is the identifier that `IKeyWrapper.UnwrapAsync`
   needs to address the key without configuration, so unwrapping needs no new member.
2. **Three adapters implement it.**
   - `AwsKmsKeyWrapper`: the reference is a KMS key or alias ARN (`AwsKmsKeyArn.IsValid`); the region comes from the ARN
     and `KeyId` is the key ARN the service reports.
   - `GoogleCloudKmsKeyWrapper`: the reference is a crypto key name without a version
     (`GoogleCloudKmsKeyName.IsCryptoKeyName`); the version the service reports has to be a version of the referenced
     crypto key, otherwise the wrap throws `IOException`.
   - `AzureKeyVaultKeyWrapper`: the reference is a key id with or without a version (`AzureKeyVaultKeyId.IsKeyId`); the
     version the service reports has to be a version of the referenced key, otherwise the wrap throws
     `InvalidOperationException`, and the 3072-bit floor applies.

   Each adapter's configured `WrapAsync` and its referenced `WrapAsync` call one private `WrapUnderAsync`, so the two
   cannot drift.
3. **Process and deployment secrets do not implement it.** `EphemeralKeyWrapper`, `LocalKeyWrapper` and
   `RecoveryKeyWrapper` hold a key that is a process secret or a deployment secret, never a key a third party controls.
4. **The catalog resolves them.** `IKeyWrapperCatalog.TryGetReferenced` and `GetReferenced` return the registered wrapper
   that implements the interface; `GetReferenced` names the registered referenced wrappers when it finds none. The
   adapters stay registered as `IKeyWrapper`, and the catalog tests `is IReferencedKeyWrapper`.

## Options considered

- **A reference parameter on `IKeyWrapper.WrapAsync`.** Ephemeral, local and recovery wrappers give it no meaning and would
  have to throw.
- **A wrapper instance per reference, created by a factory.** It duplicates the client caches and validators the adapters
  hold.
- **A marker interface without members.** The capability is the members, as
  [ADR-1279](1279-key-wrapping-is-a-secrets-abstraction-shared-by-the-root-and-recovery-wrappers.md) concludes for a
  marker.

## Consequences

- A third-party wrapper becomes a candidate for tenant root keys by implementing two members.
- The configured and the referenced wrap share one code path.
- Unwrapping needs no change.

## References

- [ADR-1279](1279-key-wrapping-is-a-secrets-abstraction-shared-by-the-root-and-recovery-wrappers.md),
  [ADR-1260](1260-azure-key-vault-and-google-cloud-kms-are-root-key-providers.md),
  [ADR-1414](1414-a-tenant-can-wrap-its-master-keys-under-a-root-key-it-controls.md)
- `src/NCode.Identity.Secrets.Abstractions/KeyWrapping/IReferencedKeyWrapper.cs`,
  `src/NCode.Identity.Secrets.AwsKms/AwsKmsKeyWrapper.cs`,
  `src/NCode.Identity.Secrets.GoogleCloudKms/GoogleCloudKmsKeyWrapper.cs`,
  `src/NCode.Identity.Secrets.AzureKeyVault/AzureKeyVaultKeyWrapper.cs`
- Issue [#174](https://github.com/NCodeGroup/Identity/issues/174)
