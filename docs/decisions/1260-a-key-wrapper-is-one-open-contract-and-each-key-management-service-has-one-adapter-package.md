# 1260. A key wrapper is one open contract, and each key management service has one adapter package

- **Status:** Accepted
- **Date:** 2026-11-08
- **Deciders:** NCode Group

## Context

[ADR-1235](1235-stored-secrets-and-grant-payloads-are-protected-by-a-key-hierarchy-under-an-external-root-key.md) wraps
every master key under an external root key, and the recovery key wraps it a second time. Several wrappers do the same
job: an ephemeral one for development, a local one, an offline recovery one, and one adapter for each key management
service (AWS KMS, Azure Key Vault, Google Cloud KMS). Wrapping a key under a key encryption key held elsewhere has
nothing to do with persisted-secret data, so the contract belongs with the key material vocabulary that
`NCode.Identity.Jose.Abstractions` already depends on, where a remote JOSE key can build on it without a new dependency.

A deployment on Azure or Google Cloud keeps its root key in its own cloud's key management service, audited by that
cloud's logs and reached through the same keyless workload identity as its other resources, rather than in an AWS
account that exists for one key. The services differ in what a wrap call can bind and check. A Cloud KMS symmetric key
encrypts with additional authenticated data, and Google documents CRC32C checksums on every request and response as the
end-to-end integrity check. A Key Vault vault, Standard or Premium, holds no symmetric keys: it wraps keys with RSA
keys, and its RSA-OAEP takes no associated data; only a Managed HSM adds AES keys. Their keyless credentials differ too:
Microsoft Entra workload identity federation fetches the cluster issuer's discovery document and signing keys itself, so
the issuer must be publicly discoverable, while a Google workload identity pool provider can hold the cluster's
uploaded JWKS, so its issuer need not be public.

One package per service holds both of the service's roles: its key wrapper and its remote key provider
([ADR-1471](1471-a-tenants-signing-keys-can-be-held-in-a-key-management-service-that-signs-every-token.md)). A package
named for one role would be named for half of what it does.

## Decision

**A key wrapper is one open contract, `IKeyWrapper`, in the secrets abstractions, and each key management service has
one adapter package named for the service, `NCode.Identity.Secrets.<Vendor>`.**

1. **Contract and home.** `IKeyWrapper` (`Name`, `WrapAsync`, `UnwrapAsync`), the DTO `WrappedKey` (`WrapperName`,
   `KeyId`, `Ciphertext`), the name constants `KeyWrapperNames` and the decoder `WrappedKeyCiphertext` live in
   `NCode.Identity.Secrets.Abstractions`, folder `KeyWrapping/`, namespace `NCode.Identity.Secrets.KeyWrapping`. The
   types need only the BCL. `UnwrapAsync` writes the key into a caller-supplied `IBufferWriter<byte>`, because a caller
   cannot know an unwrapped key's length in advance; it advances the writer only on success and zeroes what it wrote on
   failure ([ADR-1266](1266-binary-data-uses-bufferfactory-rentals-with-explicit-sensitivity-and-text-only-strict-encodings.md)).
2. **One vocabulary.** Implementations are `<Kind>KeyWrapper`, their options `<Kind>KeyWrapperOptions`, their
   registration `Add<Kind>KeyWrapper`, and the internal catalog is `IKeyWrapperCatalog`. The in-memory AES-GCM helper is
   `AesGcmKeyWrap`, matching JOSE's `IAesKeyWrap` and `DefaultAesKeyWrap`, because it is a primitive, not an
   `IKeyWrapper`. The vocabulary is open ([ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md)): a
   third-party wrapper implements `IKeyWrapper`, depends only on `NCode.Identity.Secrets.Abstractions`, and registers.
3. **The root role is selection by name.** `SecretProtection:RootKeyProvider` picks the root among all registered
   wrappers through the catalog; there is no marker interface. "Root key provider" names only that role: it stays in the
   setting, the `SecretProtection:RootKeyProviders:<Vendor>` sections, the `MasterKeys` columns and operator-facing
   messages.
4. **Built-in wrappers.** `EphemeralKeyWrapper` and `LocalKeyWrapper` are built into `NCode.Identity.Secrets.Persistence`
   over the AES-GCM primitive `AesGcmKeyWrap`. `WrappedKeyCiphertext` (`GetByteCount`, `Decode`) is the one decoder of a
   stored ciphertext that a wrapper writes as unpadded base64url, into a caller-supplied span or `IBufferWriter<byte>`.
   It uses the strict `NCode.Base64Url`, which refuses padding and illegal characters, so the AES-GCM primitive, the
   three adapters and third-party wrappers share one decoder and one set of messages: empty or invalid text throws
   `CryptographicException`, and a destination span that is too short throws `ArgumentException`. A format's own
   structural checks, such as AES-GCM's minimum length, and the encoding stay in each wrapper.
5. **The recovery wrapper only wraps.** `RecoveryKeyWrapper` (`recovery-rsa-oaep-256`) implements the internal
   `IRecoveryKeyWrapper : IKeyWrapper`, whose one extra member is `bool IsConfigured`. It ignores the context (RSA-OAEP
   in .NET takes no label) and throws `NotSupportedException` from `UnwrapAsync`, because its private key is offline. It
   is not registered as an `IKeyWrapper`, so it is never selected as the root.
6. **JOSE stays separate.** The JOSE `KeyManagementAlgorithm` family is a synchronous algorithm over local key material
   whose formats RFC 7518 fixes and which writes `iv`, `tag`, `epk` and `p2s` into the JWE header. A key wrapper holds or
   reaches its key, is asynchronous because a KMS call is, and returns self-contained storable text whose format the
   stored master keys fix. They share only BCL one-liners, so no code is extracted.
7. **One package per service.** The adapters are `NCode.Identity.Secrets.AwsKms`, `NCode.Identity.Secrets.AzureKeyVault`
   and `NCode.Identity.Secrets.GoogleCloudKms` (project, assembly, root namespace and test project). Each holds both
   roles of its service: the key wrapper registers on the `SecretPersistenceLibrary` builder, and the remote key
   provider ([ADR-1474](1474-a-remote-key-provider-is-described-by-data-creates-a-key-once-and-signs-into-the-callers-buffer.md))
   registers on the `SecretsLibrary` builder. A role is a namespace of the contracts, not a package segment, and a
   package that adds another service names itself the same way.
8. **An adapter is inert until selected.** A registered adapter is used only when the root, an owner's root provider or
   a remote key provider selects it: its validator skips otherwise, no client or credential is created before the first
   wrap or unwrap, and credentials come only from the SDK's keyless chain, never from configuration.
9. **AWS KMS** (`aws-kms`): the key is an ARN, and the region comes from it.
10. **Google Cloud KMS** (`google-cloud-kms`; `Google.Cloud.Kms.V1`). `KeyName` is a crypto key resource name without a
    version. Encrypt names that key, so the service encrypts with its primary version, and sends the context as
    `AdditionalAuthenticatedData`, so the service refuses to decrypt under any other context. `WrappedKey.KeyId` is the
    crypto key version name that the response reports, which must belong to the configured key. Decrypt names the crypto
    key cut from that version, so rotating the primary version leaves older master keys unwrapping. Every request and
    response carries CRC32C checksums computed with `BitOperations.Crc32C`. A failed check, or a response naming a
    version of another key, is an `IOException` and can be retried. Cloud KMS reports a request that fails its check
    with `InvalidArgument`, the status of a wrong ciphertext or context, so Decrypt is tried at most twice with the same
    request and a second `InvalidArgument` becomes a `CryptographicException`; every other `RpcException` propagates, so
    an outage is never mistaken for a wrong key. The decrypted plaintext buffer is zeroed after it is copied.
    Application Default Credentials back one client, created on first use and shared; a failed creation is not cached.
11. **Azure Key Vault** (`azure-key-vault`; `Azure.Security.KeyVault.Keys`, `Azure.Identity`). `KeyId` is
    `https://<vault>/keys/<name>[/<version>]`. Wraps use `KeyWrapAlgorithm.RsaOaep256`, which every Key Vault tier
    supports, without associated data: the master key's key-check value binds the key to its owner and version. The
    reported versioned key identifier is stored, and a result without a version is refused; unwrap names that version.
    A wrap under 384 bytes, which an RSA modulus under 3072 bits produces, is refused, because the root protects master
    keys for as long as their data lives. An unwrap that fails with HTTP 400 is a `CryptographicException`; everything
    else propagates. Plaintext arrays are pinned and zeroed after the call. `DefaultAzureCredential` is created once on
    first use; the chart sets `AZURE_TOKEN_CREDENTIALS=WorkloadIdentityCredential`, so a Production pod tries one
    credential. The identity needs only the role "Key Vault Crypto Service Encryption User" on the key, or "Managed HSM
    Crypto Service Encryption User" in a Managed HSM.
12. **Chart.** `secretProtection.azureKeyVault` and `secretProtection.googleCloudKms` configure the two adapters. A pod
    mounts a vendor's credentials only when that vendor is the root, a listed owner-root provider or an enabled remote
    provider, and a malformed key identifier, key name or workload identity pool provider is refused before the release
    renders ([ADR-1458](1458-the-host-chart-deploys-the-server-and-an-opt-in-management-console-and-refuses-unsafe-releases.md)).

## Options considered

- **The contract in the persistence abstractions.** A remote JOSE key or another consumer would have to depend on
  persistence to use a contract that has nothing to do with persisted-secret data. Rejected.
- **A new package for the contract.** Three small types that need only the BCL do not justify a package, and
  `NCode.Identity.Secrets.Abstractions` is already the key-material vocabulary that JOSE depends on. Rejected.
- **JOSE's `KeyManagementAlgorithm` as the contract.** It is synchronous over local key material with formats fixed by
  RFC 7518, whereas a wrapper is asynchronous, reaches a key it does not hold and returns storable text. Rejected.
- **A marker interface for the root role.** The root is chosen by name through configuration, so any registered wrapper
  can serve it; a marker adds a type without adding a rule. Rejected.
- **Renaming the configuration sections and columns after the contract.** "Root key provider" stays accurate for the
  role, and renaming would change configuration and stored data. Rejected.
- **An Azure hybrid: an RSA-wrapped AES key, and local AES-GCM to bind the context.** It binds the context as the other
  clouds do, but it is a bespoke wrapped-key format for a binding the key-check value already gives, and one that Key
  Vault would never see or log. Rejected.
- **A Managed HSM AES key as the Azure default.** Symmetric like the other cloud roots, but a Managed HSM costs about
  $2.3k per month, far more than a key in a vault. Rejected as the default; a Managed HSM RSA key works with the adapter.
- **Skipping the CRC32C checksums.** A plaintext corrupted on its way to the service would be wrapped and stored as is,
  and its master key would fail its key check only when loaded, after data has been protected under it. The checksums
  cost one CRC per buffer and no dependency. Rejected.
- **Credentials in the options** (a client secret, certificate or service-account key). A stored, long-lived credential
  is what [ADR-1134](1134-the-platform-and-the-host-follow-written-cloud-native-operating-principles.md) rules out, and
  both SDK chains resolve keyless workload identity already. Rejected.
- **A package per role** (`Secrets.KeyWrapping.<Vendor>` and `Secrets.RemoteKeys.<Vendor>`) splits a service's client,
  credentials and options across two packages that configure the same service. Rejected.
- **`Secrets.Kms.<Vendor>`** adds a segment that no other adapter uses. Rejected.
- **One open contract and one package per service named `NCode.Identity.Secrets.<Vendor>`.** Chosen.

## Consequences

- A new wrapper, such as another KMS or a remote JOSE key, implements `IKeyWrapper` and depends only on
  `NCode.Identity.Secrets.Abstractions`. The master key factory wraps under the root and then under the recovery key
  through the same contract.
- Every wrapper that stores base64url ciphertext refuses malformed ciphertext with the same exception and message.
- A host references one package per service, whichever roles it uses, and a new role for a service is a new namespace in
  its package, not a new package.
- A deployment on Azure or Google Cloud keeps its root key in its own cloud, where that cloud's audit logs record every
  unwrap. Rotating either key needs no configuration change while the Azure key identifier carries no version; master
  keys keep unwrapping under their recorded version as long as it stays enabled.
- An Azure root key is RSA, so unlike the symmetric `aws-kms`, `google-cloud-kms` and `local` roots it is not
  quantum-resistant. Azure neither binds nor logs the owner: Key Vault cannot refuse a wrapped key presented for another
  owner, which only the key-check value catches after the unwrap.
- Azure needs a publicly discoverable service-account issuer; Google does not, but its pool provider trusts only the
  JWKS uploaded to it, so the cluster's signing keys must be uploaded again whenever they change.
- The reference host registers all three adapters, so its image carries the AWS, Azure and Google SDKs; a host built on
  the library references only the package it uses.
- The adapters' unit tests cover the checksum, version and error mappings with faked clients, and the chart's tests
  cover the credential wiring.

## References

- [ADR-1235](1235-stored-secrets-and-grant-payloads-are-protected-by-a-key-hierarchy-under-an-external-root-key.md) — the
  key hierarchy and the root role.
- [ADR-1471](1471-a-tenants-signing-keys-can-be-held-in-a-key-management-service-that-signs-every-token.md) and
  [ADR-1474](1474-a-remote-key-provider-is-described-by-data-creates-a-key-once-and-signs-into-the-callers-buffer.md) —
  the remote key provider that shares each adapter package.
- [ADR-1479](1479-a-protocol-clients-and-a-bffs-keys-can-be-held-in-a-key-management-service-read-and-proved-at-start.md) —
  the remote key providers that clients and BFFs select.
- [ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md) — open vocabularies.
- [ADR-1266](1266-binary-data-uses-bufferfactory-rentals-with-explicit-sensitivity-and-text-only-strict-encodings.md) —
  the writer-based producer and its sizing method.
- [ADR-1134](1134-the-platform-and-the-host-follow-written-cloud-native-operating-principles.md) — no long-lived
  credentials.
- [ADR-1458](1458-the-host-chart-deploys-the-server-and-an-opt-in-management-console-and-refuses-unsafe-releases.md) — the
  chart refuses a release that the host would refuse.
- [`AzureKeyVaultKeyWrapper`](../../src/NCode.Identity.Secrets.AzureKeyVault/AzureKeyVaultKeyWrapper.cs),
  [`GoogleCloudKmsKeyWrapper`](../../src/NCode.Identity.Secrets.GoogleCloudKms/GoogleCloudKmsKeyWrapper.cs) and
  [`AwsKmsKeyWrapper`](../../src/NCode.Identity.Secrets.AwsKms/AwsKmsKeyWrapper.cs) — the adapters.
- Issue [#652](https://github.com/NCodeGroup/identity/issues/652).
- Google Cloud, "Verifying end-to-end data integrity" (CRC32C checksums on Cloud KMS requests and responses).
- Microsoft, "Key types, algorithms, and operations" (Key Vault and Managed HSM wrap algorithms).
