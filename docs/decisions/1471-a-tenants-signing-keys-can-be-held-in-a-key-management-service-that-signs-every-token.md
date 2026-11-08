# 1471. A tenant's signing keys can be held in a key management service that signs every token

- **Status:** Accepted (amends [ADR-1404](1404-a-tenants-provisioned-keys-roll-over-ahead-of-retirement-and-expire-after-a-grace.md); amended by [ADR-1474](1474-a-remote-key-provider-is-described-by-data-creates-a-key-once-and-signs-into-the-callers-buffer.md): providers are described by data, create a key once and sign into the caller's buffer; amended by [ADR-1475](1475-the-management-api-creates-a-tenants-key-in-a-key-management-service-and-binds-one-that-proves-it-signs.md): the management API creates and binds a tenant's remote keys; amended by [ADR-1477](1477-a-tenants-encryption-keys-can-be-held-in-a-key-management-service-that-decrypts-every-content-key.md))
- **Date:** 2026-11-08
- **Deciders:** NCode Group

## Context

Every tenant is provisioned with a persisted key for each algorithm its settings use
([ADR-1169](1169-every-tenant-is-provisioned-with-persisted-keys.md)), and those keys roll over on a schedule
([ADR-1404](1404-a-tenants-provisioned-keys-roll-over-ahead-of-retirement-and-expire-after-a-grace.md)). A stored
private key is protected under a master key ([ADR-1235](1235-stored-secrets-are-protected-by-a-key-hierarchy-under-an-external-root-key.md)),
but a running replica holds every key it loaded in memory, so a process that is read holds every private key its
tenants sign with. A key management service (AWS KMS, Azure Key Vault or Managed HSM, Google Cloud KMS) narrows that
exposure: the private half never leaves the service, and the replica asks the service to sign while it runs.

Asking a service for every signature has forces that a local key does not:

- **Latency and fee.** Each token costs a network round trip and, at the services that bill by request, a fee.
- **Throttling and outages.** A service throttles a caller that signs too often, and a service that is down signs
  nothing.
- **The JWKS must not call the service.** A verifier fetches a tenant's public keys at a rate the server does not
  control, so publishing them cannot cost a call to the service per fetch.
- **Signature encodings differ.** AWS KMS and Google Cloud KMS return an ECDSA signature as a DER `SEQUENCE` of two
  integers, where JOSE and COSE carry the IEEE P1363 concatenation `r‖s`.
- **RSA-PSS salts.** Every listed service signs with a salt as long as the hash, which is what
  `RSASignaturePadding.Pss` verifies.
- **Rollover across replicas.** A successor key must exist in the store, and so in the published JWKS, before the
  first token it signs, whichever replica generated it.

A remote key also has to fit the signing seam without changing it for local keys
([ADR-1472](1472-signing-is-asynchronous-from-the-signature-algorithm-to-every-caller.md)).

## Decision

**A tenant opts in, through the open settings vocabulary, to signing keys whose private half stays in a key management
service. The server stores each key's public half and a reference to it, signs every token through the service,
verifies every signature it returns with the stored public key before a token carries it, and publishes the stored
public keys without calling the service.**

1. **A remote key is a family key class.** `RemoteRsaSecretKey : RsaSecretKey, IRemoteSecretKey` and
   `RemoteEccSecretKey : EccSecretKey, IRemoteSecretKey` hold the SubjectPublicKeyInfo exactly as `PublicRsaSecretKey`
   and `PublicEccSecretKey` do, so every algorithm's key-type and curve check, JWK export and verification apply
   unchanged. `IRemoteSecretKey` exposes the key's `RemoteKeyBinding` and
   `ValueTask<int> SignAsync(string algorithm, ReadOnlyMemory<byte> signingInput, Memory<byte> signature, CancellationToken cancellationToken)`.
2. **The remote vocabulary is data and a provider contract.** In `NCode.Identity.Secrets.RemoteKeys`:
   `RemoteKeyReference` (`provider` and `keyId`, written `provider:keyId`, parsed at the first `:` because an AWS ARN
   holds more); `RemoteKeyProviderNames` (`aws-kms`, `azure-key-vault`, `google-cloud-kms`, the `KeyWrapperNames`
   values, so one name selects one service); `RemoteSigningAlgorithm`, one row of the open mapping vocabulary (the
   JOSE `alg`, the secret type, size and curve, the service's key spec and signing algorithm names, the digest the
   service signs or `null` when it receives the whole signing input, and whether it returns ECDSA as DER);
   `IRemoteSigningAlgorithmSource`, which a package registers so anyone adds rows; `IRemoteKeyProvider` with
   `CreateKeyAsync`, `SignAsync` (the service's bytes as returned) and `DestroyKeyAsync` (which completes for a key
   already destroyed, disabled, scheduled for deletion or missing); and `RemoteKeyBinding` (provider, reference and
   row). `RemoteKeyCreationRequest.GroupName` is `"ncid-"` and the lowercase hex of the first 16 bytes of the SHA-256
   of the owner, provider, algorithm, key spec, size and curve: the stable service key whose versions are one owner's
   successive keys for one algorithm and spec, 37 characters, valid as an Azure key name and a Google crypto key id.
   `IRemoteKeyProviderCatalog` resolves a provider by name and a row by `RemoteSigningAlgorithmSelector`, ordinally.
   `IRemoteSecretKeyTypeDescriptor` is implemented beside `ISecretKeyTypeDescriptor` by a key type a service can
   hold; RSA and ECC do, and a contributed key type opts in the same way.
3. **Storage uses the `remote` encoding and the existing columns.** A remote `PersistedSecret` has `SecretType`
   `rsa` or `ecc`, `SecretEncoding` `remote` (`SecretEncodings.Remote`), `EncodedValue` the reference, `PublicKey` the
   base64url SubjectPublicKeyInfo read from the service at creation, `KeySizeBits`, a required `Algorithm` and `Origin`
   `generated`. `SecretEncodings.IsUnprotected` is true for a public or remote secret, and the callers that mean "not
   under a master key" ask it; `IsPublic` keeps meaning public material only. The reference is not a secret, and a
   remote secret needs no master key: its integrity comes from verifying every signature locally (rule 6).
4. **A remote secret is read fail-closed, when its tenant loads.** `DefaultSecretSerializer` parses the reference,
   resolves the provider, requires the public key and the algorithm, requires an `IRemoteSecretKeyTypeDescriptor`,
   reads the stored public key to learn its size and curve, resolves the mapping row and builds the key. Each failure
   is an `InvalidOperationException` that names the secret, and a tenant with a remote key it cannot serve does not
   load.
5. **The factory checks the key against its row.** `ISecretKeyFactory.CreateRsaRemote` and `CreateEccRemote` throw an
   `ArgumentException` unless the row's secret type is the key's, the public key's size equals the row's `KeySizeBits`
   and, for ECC, its curve equals the row's `Curve`.
6. **One signing helper hashes, calls the provider and normalizes.** `RemoteKeySigning.SignAsync` hashes the signing
   input with the row's digest when one is set, calls `IRemoteKeyProvider.SignAsync`, converts a DER ECDSA signature
   to `r‖s` with halves of `(KeySizeBits + 7) / 8` bytes (`EcdsaSignatureConverter`, which refuses a negative integer,
   an oversize integer, trailing data and a short destination with a `CryptographicException`), and returns the
   bytes written. Both key classes refuse an algorithm other than their binding's. Nothing is logged. The signature
   algorithm then verifies what came back with the stored public key
   ([ADR-1472](1472-signing-is-asynchronous-from-the-signature-algorithm-to-every-caller.md)), so a service that
   returns a wrong signature fails the token before any caller sees it.
7. **The mapping catalog fails at construction.** `DefaultRemoteKeyProviderCatalog` throws for an invalid provider
   name, a name registered twice and two rows with the same selector; rows of an unregistered provider are kept.
   `AddSecretsLibrary` registers it.
8. **Generation.** `ISecretGenerator.GenerateRemoteSecretAsync(GenerateRemoteSecretRequest)` resolves the lifecycle,
   requires a remote-capable descriptor, a registered provider and a row (each an `InvalidOperationException` before
   any call to the service), creates the key, reads the returned public key and, when it cannot be read as the row's
   key type or its size or curve differs from the row, hands the record it would return to `IRemoteSecretDestroyer`,
   which destroys the key it created, and fails; otherwise it returns the remote record of rule 3.
9. **A key the server created is destroyed after the commit that removes its record.** `IRemoteSecretDestroyer`
   takes the removed secrets and takes no token ([ADR-1468](1468-a-security-effect-that-follows-a-committed-change-runs-on-its-own-bounded-token.md)).
   `DefaultRemoteSecretDestroyer` skips every secret that is not `remote` or whose `Origin` is not `generated`, runs
   each `DestroyKeyAsync` through `PostCommitWork.RunAsync`, never throws, and logs a malformed reference, an
   unregistered provider or a failure with the secret id and provider name only. The seed, the rollover pass, the
   management secret delete and the management tenant delete call it after their commit and after the tenant is
   announced. The server destroys only keys it created.
10. **The opt-in is the tenant setting `tenant_signing_key_provider`.** `OpenIdSettingKeys.TenantSigningKeyProvider`
    (`OpenIdSettingNames.TenantSigningKeyProvider`) applies to a tenant, so a platform or team document carries it to
    its tenants. Its rule is empty or one of the registered provider names; its default is empty (local keys); it is
    not discoverable; it is environment-bound, because each environment chooses its own custody; it merges by
    replacement. Encryption keys are always local.
11. **A registered provider holds every provisioned signature key, or the host does not start.**
    `DefaultTenantKeyPolicy.ResolveSpecifications` reports, for each registered provider and each signature
    specification with no mapping row, that the provider cannot hold the provisioned algorithm, key type, size and
    curve. The provisioning validator and the seed surface the error.
12. **Custody is part of a provisioned key.** `TenantKeySpecification.RemoteKeyProvider` is `null` for a local key.
    `ITenantKeyPolicy.ForTenant` sets it on each signature specification when the setting is not empty.
    `IsSatisfiedBy` requires the custody: a remote specification is satisfied only by a `remote` secret whose reference
    parses and names the provider, and a local specification only by a secret that is not `remote`.
13. **A custody change hands over after the activation delay.** Held keys that are managed, bound to the
    specification's algorithm, serving and not satisfying the specification are superseded. The successor, generated
    in the custody the specification names, activates at the hand-over instant `H`: with a serving key, `Max(now, the earliest
    ActivatesWhen of the serving keys)`; otherwise, with an active superseded key, `Min(now + ActivationDelay, the
    latest RetiresWhen of the active superseded keys)`; otherwise now. An upcoming serving key is activated at once
    only when no serving key and no superseded key is active. Every superseded key then retires with the reason
    `custody_changed` (`TenantKeyChangeReasons.CustodyChanged`): an active one at `H` and an upcoming one at once, each
    expiring one grace later. `TenantKeyRetirement` carries its `Reason`. A verifier that cached the JWKS holds the
    successor for the whole delay before it signs, as with any successor, and an operator's key keeps its lifecycle.
14. **The rollover generates and reports remote keys and destroys nothing.** `DefaultTenantKeyRollover` generates a
    specification with a provider through `GenerateRemoteSecretAsync` and loads the master-key protector only for a
    local key. `TenantKeyRolloverResult.RemovedSecrets` carries the removed records to the destroyer of rule 9.
    `TenantKeyAuditEvent` writes the detail `remote_key_provider` from the secret's reference. A successor of a remote
    key is a further version of its group: a Google crypto key version, an Azure key version or, because AWS KMS has no
    versions of an asymmetric key, one AWS key tagged with the group.
15. **Reconciliation sees a custody change.** `TenantKeyReconciliationHandler` applies `ForTenant` to the used
    specifications, and its short-circuit for a settings change returns `false` only when every used algorithm was
    used before and the specifications for the previous and the current settings are equal element by element.
16. **Retry with jittered back-off is the SDK's, configured explicitly, in one layer.** AWS: `AwsKmsClientConfiguration.Create`
    sets `RequestRetryMode.Standard` and `MaxErrorRetry = 3`. Azure: `AzureKeyVaultClientOptions.Configure` sets
    exponential retry with three retries, a 500 ms delay and an 8 s ceiling on every `CryptographyClient` and
    `KeyClient`. Google: `GoogleCloudKmsCallSettings.Retrying` retries `ResourceExhausted`, `Unavailable` and
    `DeadlineExceeded` up to four attempts with exponential back-off and jitter for signing and reading, and
    `Creating` retries `ResourceExhausted` only, up to three attempts, for creation and destruction.
17. **Providers live in the existing KMS packages, beside each key wrapper.** `AddAwsKmsRemoteKeyProvider`,
    `AddAzureKeyVaultRemoteKeyProvider` and `AddGoogleCloudKmsRemoteKeyProvider` extend
    `IServiceBuilder<SecretPersistenceLibrary>`. Each binds options strictly from
    `SecretProtection:RemoteKeyProviders:<Vendor>` with `ValidateOnStart` and reuses the wrapper's client factory with
    `access: null`, the deployment's identity. Each package's `Add<Vendor>RemoteKeyProviderIfConfigured` method registers
    its provider only when its section exists, and the reference host calls it.
    - **AWS (`aws-kms`).** A region and a deletion window of 7 to 30 days. Creation creates a `SIGN_VERIFY` key with the
      row's key spec and the tags `ncode-identity:owner`, `:secret`, `:algorithm` and `:group`, reads its public key,
      requires the key usage, key spec and signing algorithm to match the row and the key id to be an ARN, and uses the
      ARN as the key id. Signing calls `Sign` in the ARN's region with `MessageType` `DIGEST` or `RAW` as the row says
      and requires the response's key id and signing algorithm to match. Destruction schedules key deletion, and
      completes for a missing key or one already pending deletion.
    - **Azure (`azure-key-vault`).** A vault URI and `HardwareProtected` (default true). `IAzureKeyVaultClientFactory`
      yields one `KeyClient` per vault. Creation names the key `{GroupName}-h` (hardware) or `-s`, so an existing name
      gains a version, and uses the versioned key id. Signing requires the result's key id and algorithm to match.
      Key Vault deletes whole keys only, so destruction disables the version, and completes for a missing key.
    - **Google (`google-cloud-kms`).** A key ring and `HardwareProtected` (default true). Creation creates a
      crypto key version under `{KeyRing}/cryptoKeys/{GroupName}-{h|s}`, creating the crypto key (purpose
      `AsymmetricSign`, labelled with the group and algorithm) when it does not exist, polls until the version is
      enabled within 60 seconds, and requires the public key's name, `PemCrc32C` and algorithm to match. Signing sends
      a digest or the data with its CRC32C and requires the verified CRC32C, the signature CRC32C and the key version
      name to match, else an `IOException`, as the wrapper does. Destruction destroys the key version, and completes
      for a missing key or one already scheduled.
18. **The provider mapping rows are:**

    | Provider | `alg` | Key | `KeySpec` | `SigningAlgorithm` | Digest | DER |
    | --- | --- | --- | --- | --- | --- | --- |
    | aws-kms | RS256/384/512 | RSA 2048, 3072, 4096 each | `RSA_{n}` | `RSASSA_PKCS1_V1_5_SHA_{256,384,512}` | SHA-256/384/512 | no |
    | aws-kms | PS256/384/512 | RSA 2048, 3072, 4096 each | `RSA_{n}` | `RSASSA_PSS_SHA_{256,384,512}` | SHA-256/384/512 | no |
    | aws-kms | ES256 / ES384 / ES512 | P-256 / P-384 / P-521 | `ECC_NIST_P256/P384/P521` | `ECDSA_SHA_256/384/512` | SHA-256/384/512 | yes |
    | google-cloud-kms | RS256 | RSA 2048, 3072, 4096 | `RSA_SIGN_PKCS1_{n}_SHA256` | = KeySpec | SHA-256 | no |
    | google-cloud-kms | RS512 / PS512 | RSA 4096 | `RSA_SIGN_PKCS1_4096_SHA512` / `RSA_SIGN_PSS_4096_SHA512` | = KeySpec | SHA-512 | no |
    | google-cloud-kms | PS256 | RSA 2048, 3072, 4096 | `RSA_SIGN_PSS_{n}_SHA256` | = KeySpec | SHA-256 | no |
    | google-cloud-kms | ES256 / ES384 | P-256 / P-384 | `EC_SIGN_P256_SHA256` / `EC_SIGN_P384_SHA384` | = KeySpec | SHA-256/384 | yes |
    | azure-key-vault | RS256/384/512, PS256/384/512 | RSA 2048, 3072, 4096 each | `RSA` | the `alg` | SHA-256/384/512 | no |
    | azure-key-vault | ES256 / ES384 / ES512 | P-256 / P-384 / P-521 | `EC` | the `alg` | SHA-256/384/512 | no |

    RSA rows have no curve. The provisioned defaults, RS256 and PS256 at 3072 bits and ES256 on P-256, are held by all
    three providers.
19. **Security posture.** The private key never leaves the service. Logs carry identifiers only, never an input, a
    digest or a signature. Every remote signature is verified locally. A remote key is bound to one algorithm. Keys
    are sign-only, and hardware-protected by default on Azure and Google. A key created by a seed that then loses its
    commit stays in the service unused, tagged with its group. The permissions the deployment's identity needs: AWS
    `kms:CreateKey`, `kms:TagResource`, `kms:GetPublicKey`, `kms:Sign`, `kms:ScheduleKeyDeletion` and
    `kms:DescribeKey`; Azure the `Key Vault Crypto Officer` role on the vault; Google `cloudkms.cryptoKeys.create`
    and `cloudkms.cryptoKeyVersions.create`, `.get`, `.viewPublicKey`, `.useToSign` and `.destroy` on the key ring.

## Options considered

- **A `RemoteAsymmetricSecretKey` base.** It would sit beside the RSA and ECC key classes and fail every algorithm's
  key-type check, so every algorithm would need a remote branch. A remote key that is an RSA or ECC key class needs
  none.
- **Fetching the public keys from the service per request.** It adds latency to every JWKS and every verification
  and stops verification, including of tokens already issued, when the service is out. The stored public key serves
  both.
- **Trusting the service's signature unverified.** A wrong signature, from a fault, a mismatched key or a hostile
  endpoint, would reach a relying party as a token that fails there. Verifying with the stored public key costs one
  local verification per token and catches it before the token leaves.
- **A retry layer of the family's over the SDK's.** Two layers multiply attempts and delay a throttled caller. Each
  SDK's retry is configured explicitly, once.
- **Separate provider packages.** The KMS packages already hold each service's client, credentials and wrapper, so the
  providers extend them.
- **A host-only switch, or a setting per algorithm.** A host-wide switch cannot give one tenant another custody, and a
  setting per algorithm multiplies the vocabulary for a choice that is one per tenant.
- **Switching custody at once.** A relying party that cached the JWKS refuses a token whose `kid` it has not seen, so
  the successor is published for the activation delay before it signs.
- **Remote encryption keys.** Encryption keys stay local, and decryption stays out of this decision.
- **Deleting an Azure key per rollover.** Key Vault deletes whole keys only, so a rollover adds a version to the
  group's key, and removing a record disables its version.
- **Protecting the reference under the master key.** The reference is not secret, and integrity comes from verifying
  every signature with the stored public key, so protection would tie a record that holds no secret to the master key.

## Consequences

- Every token a remote tenant signs costs a round trip to the service and, where the service bills by request, a fee.
- A service outage stops issuance for the tenants whose keys it holds. Verification, the JWKS and every tenant with
  local keys continue.
- A Google crypto key is permanent: its versions are destroyed, the crypto key stays. Azure versions accumulate
  disabled, and AWS creates one key per generation, each scheduled for deletion when its record is removed.
- A key created by a seed or a rollover that loses its commit race stays in the service, unused and tagged with its
  group.
- The host refuses to start when a registered provider cannot hold a provisioned signature key, so a deployment adds
  a mapping row, or removes the provider, before it runs.
- A deployment grants its identity the permissions of rule 19, and the settings document of a team or the platform
  can choose custody for every tenant beneath it.
- A change of custody takes effect after the activation delay, not at once.

## References

- [ADR-1472](1472-signing-is-asynchronous-from-the-signature-algorithm-to-every-caller.md): the asynchronous signing
  seam a remote key signs through.
- [ADR-1404](1404-a-tenants-provisioned-keys-roll-over-ahead-of-retirement-and-expire-after-a-grace.md): the rollover
  a custody change reuses.
- [ADR-1169](1169-every-tenant-is-provisioned-with-persisted-keys.md),
  [ADR-1403](1403-a-stored-secret-has-an-active-window-inside-its-validity-window-and-every-use-of-a-key-honors-them.md),
  [ADR-1235](1235-stored-secrets-are-protected-by-a-key-hierarchy-under-an-external-root-key.md),
  [ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md),
  [ADR-1468](1468-a-security-effect-that-follows-a-committed-change-runs-on-its-own-bounded-token.md).
- [#165](https://github.com/NCodeGroup/Identity/issues/165)
