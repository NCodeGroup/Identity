# 278. Key wrapping is a secrets abstraction shared by the root and recovery wrappers

- **Status:** Accepted
- **Date:** 2026-10-28
- **Deciders:** NCode Group

## Context

[ADR-0235](0235-stored-secrets-are-protected-by-a-key-hierarchy-under-an-external-root-key.md) wraps every master key
under an external root key, and [ADR-0260](0260-azure-key-vault-and-google-cloud-kms-are-root-key-providers.md) added the
Azure Key Vault and Google Cloud KMS adapters beside AWS KMS. Three things in that design were misplaced:

- **The contract lived in the wrong package.** `IRootKeyProvider`, its DTO `RootWrappedKey` and the name constants
  `RootKeyProviderNames` sat in `NCode.Identity.Secrets.Persistence.Abstractions`, a package for persisted-secret data
  contracts and stores, although wrapping a key under a key encryption key held elsewhere has nothing to do with
  persistence.
- **A second, private contract did the same job.** The offline recovery wrapper wrapped a master key under an RSA public
  key through its own internal `IRecoveryKeyWrapper`, so the factory handled two shapes of the same operation.
- **The adapters were named as persistence packages.** `NCode.Identity.Secrets.Persistence.AwsKms`, `.AzureKeyVault` and
  `.GoogleCloudKms` are key wrappers, not persistence.

The family also wants a remote JOSE key to build on the same contract (issue #165), and further wrappers (issues #172,
#174 and #178) need a home that does not drag in persistence.

## Decision

**Wrapping a key is one contract, `IKeyWrapper`, in the secrets abstractions. Every wrapper implements it, and one
vocabulary names it everywhere.**

1. **Contract and home.** `IKeyWrapper` (`Name`, `WrapAsync`, `UnwrapAsync`), the DTO `WrappedKey` (`WrapperName`,
   `KeyId`, `Ciphertext`) and the name constants `KeyWrapperNames` live in `NCode.Identity.Secrets.Abstractions`, folder
   `KeyWrapping/`, namespace `NCode.Identity.Secrets.KeyWrapping`. That package is the key-material vocabulary that
   `NCode.Identity.Jose.Abstractions` already depends on, the types need only the BCL, and a remote JOSE key can build on
   them without a new dependency. A new package for three small types is not worth its packaging cost.
2. **One vocabulary.** The contract is a key wrapper; implementations are `<Kind>KeyWrapper`, their options
   `<Kind>KeyWrapperOptions`, their registration `Add<Kind>KeyWrapper`, the name constants `KeyWrapperNames` and the
   catalog `IKeyWrapperCatalog`. The in-memory AES-GCM helper becomes `AesGcmKeyWrap`, matching JOSE's `IAesKeyWrap` and
   `DefaultAesKeyWrap`, because it is a primitive, not an `IKeyWrapper`.
3. **Adapter packages** are `NCode.Identity.Secrets.KeyWrapping.<Vendor>`: the family names an adapter
   `<contract area>.<vendor>` (`NCode.Identity.SharedState.Valkey`), and the contract area is
   `NCode.Identity.Secrets.KeyWrapping`. They keep their reference to `NCode.Identity.Secrets.Persistence.Abstractions` and
   still register on the `SecretPersistenceLibrary` builder, because the root role that selects them, and the
   `SecretProtectionOptions` their validators read to stay inert until selected, live there.
4. **"Root key provider" names only the role.** It stays in the `SecretProtection:RootKeyProvider` setting, the
   `SecretProtection:RootKeyProviders:<Vendor>` sections, the `MasterKeys` columns and operator-facing messages. Changing
   those would change configuration and stored data, which a refactor must not; the role is real, so the names stay
   accurate.
5. **The root role is selection by name.** Every registered `IKeyWrapper` is a candidate, and `SecretProtection:RootKeyProvider`
   picks one through the catalog. There is no marker interface `IRootKeyProvider : IKeyWrapper`.
6. **The recovery wrapper implements `IKeyWrapper`** with the name `recovery-rsa-oaep-256` (a constant on the class,
   since it is never persisted or configured). It ignores the context (RSA-OAEP in .NET takes no label, exactly like the
   Azure wrapper) and throws `NotSupportedException` from `UnwrapAsync`, because its private key is offline. Its internal
   role interface is `IRecoveryKeyWrapper : IKeyWrapper` with one member, `bool IsConfigured`, which the factory and the
   master key ring's recovery warning use. It is not registered as an `IKeyWrapper` service, so it can never be selected
   as the root. `GetWrappedLength()` and `Wrap<TWriter>` stay public members of the class, the ADR-0116 sizing method
   beside the writer-based producer.
7. **JOSE stays separate.** The JOSE `KeyManagementAlgorithm` family is not merged with `IKeyWrapper`: it is a
   synchronous algorithm over local `SecretKey` material whose formats RFC 7518 fixes and which writes `iv`, `tag`, `epk`
   and `p2s` into the JWE header, while a key wrapper holds or reaches its key, is asynchronous because a KMS call is, and
   returns self-contained storable text whose format the stored master keys fix. They share only BCL one-liners (AES-GCM
   with a 96-bit nonce and 128-bit tag; RSA-OAEP-SHA-256), so no code is extracted.

Public names, old to new:

| Old | New |
| --- | --- |
| `NCode.Identity.Secrets.Persistence.Protection.IRootKeyProvider` | `NCode.Identity.Secrets.KeyWrapping.IKeyWrapper` |
| `IRootKeyProvider.ProviderName` (and each implementation's `ProviderName`) | `IKeyWrapper.Name` |
| `NCode.Identity.Secrets.Persistence.Protection.RootWrappedKey` (the type) | `NCode.Identity.Secrets.KeyWrapping.WrappedKey` |
| `RootWrappedKey.ProviderName` | `WrappedKey.WrapperName` |
| `NCode.Identity.Secrets.Persistence.Protection.RootKeyProviderNames` | `NCode.Identity.Secrets.KeyWrapping.KeyWrapperNames` (same constants and values) |
| `EphemeralRootKeyProvider`, `LocalRootKeyProvider` | `EphemeralKeyWrapper`, `LocalKeyWrapper` |
| `AesGcmRootKeyWrapper` | `AesGcmKeyWrap` |
| `IRootKeyProviderCatalog`, `DefaultRootKeyProviderCatalog` | `IKeyWrapperCatalog`, `DefaultKeyWrapperCatalog` |
| `LocalRootKeyOptions` | `LocalKeyWrapperOptions` |
| `NCode.Identity.Secrets.Persistence.<Vendor>` (package, project, assembly, namespace) | `NCode.Identity.Secrets.KeyWrapping.<Vendor>`, for `<Vendor>` = `AwsKms`, `AzureKeyVault`, `GoogleCloudKms` |
| `<Vendor>RootKeyOptionsValidator`, `<Vendor>RootKeyOptions`, `<Vendor>RootKeyProvider` | `<Vendor>KeyWrapperOptionsValidator`, `<Vendor>KeyWrapperOptions`, `<Vendor>KeyWrapper` |
| `Add<Vendor>RootKeyProvider(IConfiguration)` | `Add<Vendor>KeyWrapper(IConfiguration)` |

## Options considered

- **Keep the types in the persistence abstractions.** Rejected: wrapping a key has nothing to do with persisted-secret
  data contracts, and a remote JOSE key or another consumer would have to depend on persistence to use it.
- **A new package for the contract.** Rejected: three small types that need only the BCL do not justify a package, and
  `NCode.Identity.Secrets.Abstractions` is already the key-material vocabulary that JOSE depends on.
- **The JOSE `KeyManagementAlgorithm` as the contract.** Rejected: it is a synchronous algorithm over local `SecretKey`
  material with formats fixed by RFC 7518 that writes into the JWE header, whereas a key wrapper is asynchronous,
  reaches a key it does not hold, and returns self-contained storable text.
- **A marker interface `IRootKeyProvider : IKeyWrapper`.** Rejected: the root role is chosen by name through
  configuration, so any registered key wrapper can serve it; a marker would add a type without adding a rule, and would
  keep the old name alive.
- **Renaming the configuration sections and columns too.** Rejected: it would change configuration and stored data, which
  a refactor must not. "Root key provider" stays accurate for the role.

## Consequences

- Implementers and callers move to the new names and packages in the table above. This is a breaking rename, accepted
  under the pre-release posture ([ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md)), with a clean cutover and
  no forwarding members.
- Stored master keys, configuration keys and sections, chart values and wrapper names are unchanged, and so is every
  runtime behavior.
- The recovery wrapper and the root wrappers share one contract, so the master key factory wraps under the root and then
  under the recovery key through `IKeyWrapper`.
- A new wrapper, such as a remote JOSE key or another KMS, implements `IKeyWrapper` and depends only on
  `NCode.Identity.Secrets.Abstractions`; the vocabulary stays open ([ADR-0150](0150-vocabularies-are-open-unless-provably-closed.md)).

## References

- [ADR-0235](0235-stored-secrets-are-protected-by-a-key-hierarchy-under-an-external-root-key.md) — the key hierarchy
  and the root key provider role that this amends.
- [ADR-0260](0260-azure-key-vault-and-google-cloud-kms-are-root-key-providers.md) — the Azure Key Vault and Google Cloud
  KMS adapters that this renames.
- [ADR-0150](0150-vocabularies-are-open-unless-provably-closed.md) — open vocabularies.
- [ADR-0116](0116-memory-allocations-and-buffer-operations.md) — the sizing method beside the writer-based producer.
