# 260. Azure Key Vault and Google Cloud KMS are root key providers

- **Status:** Accepted (amended by
  [ADR-0278](0278-key-wrapping-is-a-secrets-abstraction-shared-by-the-root-and-recovery-wrappers.md): the root key
  provider contract is the shared `IKeyWrapper`, and the adapters are `NCode.Identity.Secrets.KeyWrapping.<Vendor>`)
- **Date:** 2026-10-24
- **Deciders:** NCode Group

## Context

[ADR-0235](0235-stored-secrets-are-protected-by-a-key-hierarchy-under-an-external-root-key.md) encrypts every stored
secret under a key hierarchy whose root key is held by a root key provider: an open vocabulary of `IRootKeyProvider`
strategies, selected by `SecretProtection:RootKeyProvider`, that only wrap and unwrap master keys. It defines
`ephemeral`, `local` and `aws-kms`, and leaves the other key management services as follow-up providers (issue
[#173](https://github.com/NCodeGroup/Identity/issues/173)). A deployment on Azure or Google Cloud keeps its root key in its own cloud's key management service, audited by
that cloud's logs and reached through the same keyless workload identity as its other resources, rather than in an AWS
account that exists for one key.

The two services differ in what a wrap call can bind and check. A Cloud KMS symmetric key encrypts with additional
authenticated data, and Google documents CRC32C checksums on every request and response as the end-to-end integrity
check. A Key Vault vault, Standard or Premium, holds no symmetric keys: it wraps keys with RSA keys, and its RSA-OAEP
takes no associated data; only a Managed HSM adds AES keys. Their keyless credentials differ too: Microsoft Entra
workload identity federation fetches the cluster issuer's discovery document and signing keys itself, so the issuer must
be publicly discoverable, while a Google workload identity pool provider can hold the cluster's uploaded JWKS, so its
issuer need not be public.

## Decision

Google Cloud KMS and Azure Key Vault are root key providers, each in its own opt-in package shaped like the AWS KMS
adapter. Each binds and checks everything its service allows, and takes its credentials from its SDK's keyless chain,
never from configuration.

1. **Packages.** `NCode.Identity.Secrets.Persistence.GoogleCloudKms` (on `Google.Cloud.Kms.V1`) and
   `NCode.Identity.Secrets.Persistence.AzureKeyVault` (on `Azure.Security.KeyVault.Keys` and `Azure.Identity`), one per
   vendor, each mirroring `NCode.Identity.Secrets.Persistence.AwsKms` exactly: an options class bound strictly from its
   section, a start-up validator, an internal client-factory facade, the provider, and a `DefaultRegistration`
   extension on the secret-persistence builder — `AddGoogleCloudKmsRootKeyProvider` and
   `AddAzureKeyVaultRootKeyProvider`. A registered provider stays inert until `SecretProtection:RootKeyProvider`
   selects it: its validator skips otherwise, and no client or credential is created before the first wrap or unwrap.
2. **Names.** `google-cloud-kms` and `azure-key-vault` join `RootKeyProviderNames` beside `ephemeral`, `local` and
   `aws-kms`. The constants name the providers the family ships, and the vocabulary stays open
   ([ADR-0150](0150-vocabularies-are-open-unless-provably-closed.md)): providers are found by their `ProviderName`, so
   another package adds one by registering it. The Production safety check names both among the providers whose key
   outlives the process.
3. **Options.** `SecretProtection:RootKeyProviders:GoogleCloudKms:KeyName` is the resource name of a crypto key,
   without a version (`projects/<p>/locations/<l>/keyRings/<r>/cryptoKeys/<k>`).
   `SecretProtection:RootKeyProviders:AzureKeyVault:KeyId` is a key identifier, `https://<vault host>/keys/<name>`,
   optionally followed by `/<32-character version>`; without a version, the key's current version wraps new master
   keys. Neither section holds a credential.
4. **Google Cloud KMS wraps with a symmetric key and binds the context.** Encrypt names the configured crypto key, so
   the service encrypts with its primary version, and sends the root context as `AdditionalAuthenticatedData`, so the
   service refuses to decrypt under any other context. `RootWrappedKey.KeyId` is the crypto key version name that the
   response reports, which must be a version of the configured crypto key: Google advises checking that field to verify
   that the intended resource was used. Decrypt names the crypto key cut from that version name, and the service finds
   the version in the ciphertext, so a wrapped key is never decrypted under another crypto key and rotating the primary
   version leaves older master keys unwrapping. Every request and response carries CRC32C checksums, computed with
   `System.Numerics.BitOperations.Crc32C`, so no dependency is added. A response that fails its check, or an Encrypt
   response naming a version of another key, is an `IOException`: the call can be retried. Cloud KMS reports a request
   that fails its check with `InvalidArgument`, the same status as a wrong ciphertext or associated data, so Decrypt is
   attempted at most twice with the same request, and a second `InvalidArgument` becomes a `CryptographicException`.
   Every other `RpcException` propagates, so an outage is never mistaken for a wrong key, which matters because callers
   prune keys on `CryptographicException` in Development. The decrypted plaintext buffer is zeroed after it is copied.
5. **Google credentials** come from Application Default Credentials through `KeyManagementServiceClient.CreateAsync`:
   the GKE metadata server under Workload Identity Federation for GKE, or an `external_account` credential
   configuration named by `GOOGLE_APPLICATION_CREDENTIALS`, which exchanges a projected service-account token with
   Google STS and, when one is named, impersonates a service account. One client is created on first use and shared;
   a failed creation is not cached (`Volatile.Read` and `Interlocked.CompareExchange`), so the next call tries again.
6. **Azure Key Vault wraps with an RSA key using `RSA-OAEP-256`.** `WrapKey` and `UnwrapKey` use
   `KeyWrapAlgorithm.RsaOaep256`, the RSA wrap algorithm that every Key Vault tier (Standard, Premium and Managed HSM)
   supports and the only one Microsoft recommends. Key Vault's RSA-OAEP takes no associated data, so the context is not
   sent: the master key's key-check value already binds it to its owner and version, so a wrapped key moved to another
   owner fails that check when it is loaded. `RootWrappedKey.KeyId` is the versioned key identifier that the result
   reports, and a result without a version is refused; unwrap names that version, so rotating the key leaves older
   master keys unwrapping under their own version. A wrapped key shorter than 384 bytes, which an RSA modulus under
   3072 bits produces, is refused at wrap time: the root protects master keys for as long as their data lives, so it
   needs the 128-bit security that RSA reaches at 3072 bits, and a wrapped key is as long as the modulus, so the floor
   needs no extra call. A `RequestFailedException` with HTTP status 400 on unwrap becomes a `CryptographicException`;
   everything else propagates. The SDK demands a plaintext array of exactly the key's length, so the key is copied into
   a pinned array that is zeroed after the wrap call, and the array that unwrap returns is zeroed after it is copied.
7. **Azure credentials** come from `DefaultAzureCredential`, created once on first use and shared by every
   `CryptographyClient`. A client downloads its key on first use and then wraps locally with that version, so a key
   version identifier keeps one client while a versionless identifier gets a new client for each wrap, which therefore
   always uses the key's current version. The chart sets `AZURE_TOKEN_CREDENTIALS=WorkloadIdentityCredential` with a
   projected service-account token, so a Production pod tries exactly one credential, which Microsoft Entra ID exchanges
   for a token of a managed identity or app registration. That identity needs only the role "Key Vault Crypto Service
   Encryption User" on the key, or "Managed HSM Crypto Service Encryption User" at the key's scope in a Managed HSM.
8. **Chart.** `secretProtection.azureKeyVault` (`keyId`, `workloadIdentity.{tenantId, clientId, audience,
   tokenExpirationSeconds}`) and `secretProtection.googleCloudKms` (`keyName`, `workloadIdentityFederation.{provider,
   serviceAccountEmail, tokenExpirationSeconds}`) configure the providers. Credentials are mounted only for the selected
   provider, so a pod that uses another one carries no Azure or Google credentials, and empty workload identity values
   leave the credentials to the cluster. Google's `external_account` configuration is a ConfigMap rendered from the
   values and projected with the token into one volume, and its checksum is a pod annotation, so a change restarts the
   pods. A malformed key identifier, key name or workload identity pool provider is refused before the release renders
   ([ADR-0179](0179-the-host-chart-references-its-secrets-and-refuses-a-release-the-host-would-refuse.md)), and
   Production accepts either provider as a root key held outside the pod.

## Options considered

- **An Azure hybrid: an RSA-wrapped AES key, and local AES-GCM to bind the context.** It would bind the context on
  Azure as the other cloud providers do, but it is a bespoke wrapped-key format for a binding that the master key's
  key-check value already gives, and one that Key Vault would never see or log. Rejected.
- **A Managed HSM AES key as the Azure default.** Symmetric like the other cloud roots, but a Managed HSM costs about
  $2.3k per month, far more than a key in a vault. Rejected as the default; a Managed HSM RSA key works with this
  provider, and AES keys are not part of this decision.
- **Skipping the CRC32C checksums.** Simpler calls, but a plaintext corrupted on its way to the service is wrapped and
  stored as is, and its master key fails its key check the first time it is loaded, after data has been protected
  under it; a corrupted ciphertext surfaces only at unwrap, as a wrong key. The checksums cost one CRC per buffer and no
  dependency. Rejected.
- **Credentials from configuration** (a client secret, certificate or service-account key in the options). A stored,
  long-lived credential is what [ADR-0134](0134-cloud-native-operating-principles.md) rules out, and both SDK chains
  resolve keyless workload identity already, so the options name the key and nothing else. Rejected.

## Consequences

- A deployment on Azure or Google Cloud keeps its root key in its own cloud, where that cloud's audit logs can record
  every unwrap.
- An Azure root key is RSA, so unlike the symmetric `aws-kms`, `google-cloud-kms` and `local` roots it is not
  quantum-resistant: master keys wrapped under it rely on RSA, as the recovery wrap does (issue [#178](https://github.com/NCodeGroup/Identity/issues/178)).
- Azure neither binds nor logs the context: Key Vault cannot refuse a wrapped key presented for another owner, which
  only the key-check value catches after the unwrap, and its audit log records the key, not the owner it was used for.
- Rotating either key in its service needs no configuration change while the Azure key identifier carries no version;
  master keys keep unwrapping under their recorded version as long as that version stays enabled.
- Azure needs a publicly discoverable service-account issuer; Google does not, but its pool provider trusts only the
  JWKS uploaded to it, so the cluster's signing keys must be uploaded again whenever they change.
- The reference host registers all three cloud providers, so its image carries the AWS, Azure and Google SDKs and serves
  a deployment on any of the three clouds; a host built on the library references only the package it uses.
- Infisical KMS, OpenBao/Vault Transit and PKCS#11 providers remain follow-ups (issue [#173](https://github.com/NCodeGroup/Identity/issues/173)).

## References

- [ADR-0235](0235-stored-secrets-are-protected-by-a-key-hierarchy-under-an-external-root-key.md) — the key hierarchy
  and the root key provider vocabulary that this extends.
- [ADR-0134](0134-cloud-native-operating-principles.md) — no long-lived credentials.
- [ADR-0150](0150-vocabularies-are-open-unless-provably-closed.md) — open vocabularies (root key providers).
- [ADR-0179](0179-the-host-chart-references-its-secrets-and-refuses-a-release-the-host-would-refuse.md) — the chart
  refuses a release that the host would refuse.
- [`GoogleCloudKmsKeyWrapper`](../../src/NCode.Identity.Secrets.KeyWrapping.GoogleCloudKms/GoogleCloudKmsKeyWrapper.cs)
  and
  [`AzureKeyVaultKeyWrapper`](../../src/NCode.Identity.Secrets.KeyWrapping.AzureKeyVault/AzureKeyVaultKeyWrapper.cs)
  — the providers.
- [`deployment.yaml`](../../charts/ncode-identity-host/templates/deployment.yaml) — the chart's credential wiring.
- Google Cloud, "Verifying end-to-end data integrity" (CRC32C checksums on Cloud KMS requests and responses).
- Microsoft, "Key types, algorithms, and operations" (Key Vault and Managed HSM wrap algorithms).
